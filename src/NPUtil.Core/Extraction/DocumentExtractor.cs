using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace NPUtil.Core.Extraction;

public class DocumentExtractor : IDocumentExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".csv", ".txt", ".md", ".log", ".json", ".xml",
        ".png", ".jpg", ".jpeg", ".webp"
    };

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
    }

    public async Task<ExtractedDocument> ExtractAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            return new ExtractedDocument
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Format = "Unknown",
                ErrorMessage = $"File does not exist: {filePath}"
            };
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var fileName = Path.GetFileName(filePath);

        try
        {
            return ext switch
            {
                ".pdf" => await Task.Run(() => ExtractPdf(filePath, fileName), ct),
                ".docx" => await Task.Run(() => ExtractDocx(filePath, fileName), ct),
                ".xlsx" => await Task.Run(() => ExtractXlsx(filePath, fileName), ct),
                ".pptx" => await Task.Run(() => ExtractPptx(filePath, fileName), ct),
                ".csv" => await Task.Run(() => ExtractCsv(filePath, fileName), ct),
                ".txt" or ".md" or ".log" or ".json" or ".xml" => await ExtractTextFileAsync(filePath, fileName, ext, ct),
                ".png" or ".jpg" or ".jpeg" or ".webp" => ExtractImageMetadata(filePath, fileName, ext),
                _ => new ExtractedDocument
                {
                    FilePath = filePath,
                    FileName = fileName,
                    Format = ext.TrimStart('.').ToUpperInvariant(),
                    ErrorMessage = $"Unsupported extension: {ext}"
                }
            };
        }
        catch (Exception ex)
        {
            return new ExtractedDocument
            {
                FilePath = filePath,
                FileName = fileName,
                Format = ext.TrimStart('.').ToUpperInvariant(),
                ErrorMessage = $"Extraction failed: {ex.Message}"
            };
        }
    }

    private static ExtractedDocument ExtractPdf(string filePath, string fileName)
    {
        var sections = new List<DocumentSection>();
        var tables = new List<ExtractedTable>();
        var fullTextBuilder = new StringBuilder();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var document = PdfDocument.Open(filePath);
        metadata["Pages"] = document.NumberOfPages.ToString();
        if (document.Information.Title != null) metadata["Title"] = document.Information.Title;
        if (document.Information.Author != null) metadata["Author"] = document.Information.Author;
        if (document.Information.CreationDate != null) metadata["CreationDate"] = document.Information.CreationDate;

        for (int i = 1; i <= document.NumberOfPages; i++)
        {
            var page = document.GetPage(i);
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                fullTextBuilder.AppendLine($"--- Page {i} ---");
                fullTextBuilder.AppendLine(pageText);

                sections.Add(new DocumentSection
                {
                    Title = $"Page {i}",
                    Content = pageText.Trim(),
                    SectionType = "Page",
                    PageNumber = i
                });

                // Heuristic table extraction from lines with tab/spaced columns
                var lines = pageText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var candidateTableRows = new List<List<string>>();

                foreach (var line in lines)
                {
                    // If line has multiple column separators (spaces >= 3, tabs, pipes)
                    var columns = Regex.Split(line, @"\t+|\s{3,}|\|")
                        .Select(c => c.Trim())
                        .Where(c => !string.IsNullOrEmpty(c))
                        .ToList();

                    if (columns.Count >= 2)
                    {
                        candidateTableRows.Add(columns);
                    }
                }

                if (candidateTableRows.Count >= 2)
                {
                    var maxCols = candidateTableRows.Max(r => r.Count);
                    var header = candidateTableRows[0];
                    while (header.Count < maxCols) header.Add($"Col {header.Count + 1}");

                    var dataRows = candidateTableRows.Skip(1).Select(r =>
                    {
                        var row = new List<string>(r);
                        while (row.Count < maxCols) row.Add(string.Empty);
                        return (IReadOnlyList<string>)row;
                    }).ToList();

                    tables.Add(new ExtractedTable
                    {
                        Name = $"Page {i} Table",
                        PageOrSheetNumber = i,
                        Headers = header,
                        Rows = dataRows
                    });
                }
            }
        }

        return new ExtractedDocument
        {
            FilePath = filePath,
            FileName = fileName,
            Format = "PDF",
            FullText = fullTextBuilder.ToString(),
            Sections = sections,
            Tables = tables,
            Metadata = metadata
        };
    }

    private static ExtractedDocument ExtractDocx(string filePath, string fileName)
    {
        var sections = new List<DocumentSection>();
        var tables = new List<ExtractedTable>();
        var fullTextBuilder = new StringBuilder();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var zip = ZipFile.OpenRead(filePath);
        var docEntry = zip.GetEntry("word/document.xml");
        if (docEntry == null)
        {
            throw new InvalidOperationException("Invalid DOCX file (missing word/document.xml)");
        }

        using var stream = docEntry.Open();
        var doc = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        // Read tables first
        int tableIndex = 1;
        foreach (var tbl in doc.Descendants(w + "tbl"))
        {
            var rows = new List<IReadOnlyList<string>>();
            foreach (var tr in tbl.Elements(w + "tr"))
            {
                var rowCells = new List<string>();
                foreach (var tc in tr.Elements(w + "tc"))
                {
                    var cellText = string.Join(" ", tc.Descendants(w + "t").Select(t => t.Value)).Trim();
                    rowCells.Add(cellText);
                }
                if (rowCells.Count > 0) rows.Add(rowCells);
            }

            if (rows.Count > 0)
            {
                var headers = rows[0];
                var data = rows.Skip(1).ToList();
                tables.Add(new ExtractedTable
                {
                    Name = $"DOCX Table {tableIndex++}",
                    PageOrSheetNumber = 1,
                    Headers = headers,
                    Rows = data
                });
            }
        }

        // Read Paragraphs and Headings
        foreach (var p in doc.Descendants(w + "p"))
        {
            var text = string.Join("", p.Descendants(w + "t").Select(t => t.Value)).Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            fullTextBuilder.AppendLine(text);

            var style = p.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val")?.Value ?? "Normal";
            var isHeading = style.Contains("Heading", StringComparison.OrdinalIgnoreCase) || style.Contains("Title", StringComparison.OrdinalIgnoreCase);

            sections.Add(new DocumentSection
            {
                Title = isHeading ? text : string.Empty,
                Content = text,
                SectionType = isHeading ? "Heading" : "Paragraph",
                PageNumber = 1
            });
        }

        metadata["TablesCount"] = tables.Count.ToString();
        metadata["ParagraphsCount"] = sections.Count.ToString();

        return new ExtractedDocument
        {
            FilePath = filePath,
            FileName = fileName,
            Format = "DOCX",
            FullText = fullTextBuilder.ToString(),
            Sections = sections,
            Tables = tables,
            Metadata = metadata
        };
    }

    private static ExtractedDocument ExtractXlsx(string filePath, string fileName)
    {
        var tables = new List<ExtractedTable>();
        var fullTextBuilder = new StringBuilder();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var zip = ZipFile.OpenRead(filePath);

        // Read shared strings
        var sharedStrings = new List<string>();
        var stringEntry = zip.GetEntry("xl/sharedStrings.xml");
        if (stringEntry != null)
        {
            using var sStream = stringEntry.Open();
            var sDoc = XDocument.Load(sStream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var si in sDoc.Descendants(ns + "si"))
            {
                var val = string.Join("", si.Descendants(ns + "t").Select(t => t.Value));
                sharedStrings.Add(val);
            }
        }

        // Read sheet names
        var sheetNames = new List<string>();
        var wbEntry = zip.GetEntry("xl/workbook.xml");
        if (wbEntry != null)
        {
            using var wbStream = wbEntry.Open();
            var wbDoc = XDocument.Load(wbStream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var sheet in wbDoc.Descendants(ns + "sheet"))
            {
                var name = sheet.Attribute("name")?.Value ?? $"Sheet {sheetNames.Count + 1}";
                sheetNames.Add(name);
            }
        }

        int sheetIdx = 1;
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml")))
        {
            var sheetTitle = sheetIdx <= sheetNames.Count ? sheetNames[sheetIdx - 1] : $"Sheet{sheetIdx}";
            using var sheetStream = entry.Open();
            var sheetDoc = XDocument.Load(sheetStream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            var rows = new List<List<string>>();
            foreach (var rowEl in sheetDoc.Descendants(ns + "row"))
            {
                var cellList = new List<string>();
                foreach (var c in rowEl.Elements(ns + "c"))
                {
                    var type = c.Attribute("t")?.Value;
                    var v = c.Element(ns + "v")?.Value ?? string.Empty;

                    string cellVal = v;
                    if (type == "s" && int.TryParse(v, out var strIdx) && strIdx >= 0 && strIdx < sharedStrings.Count)
                    {
                        cellVal = sharedStrings[strIdx];
                    }
                    cellList.Add(cellVal);
                }
                if (cellList.Any(s => !string.IsNullOrEmpty(s)))
                {
                    rows.Add(cellList);
                }
            }

            if (rows.Count > 0)
            {
                var maxCols = rows.Max(r => r.Count);
                var header = rows[0];
                while (header.Count < maxCols) header.Add($"Column {header.Count + 1}");

                var dataRows = rows.Skip(1).Select(r =>
                {
                    var cr = new List<string>(r);
                    while (cr.Count < maxCols) cr.Add(string.Empty);
                    return (IReadOnlyList<string>)cr;
                }).ToList();

                tables.Add(new ExtractedTable
                {
                    Name = sheetTitle,
                    PageOrSheetNumber = sheetIdx,
                    Headers = header,
                    Rows = dataRows
                });

                fullTextBuilder.AppendLine($"=== Sheet: {sheetTitle} ===");
                fullTextBuilder.AppendLine(string.Join(" | ", header));
                foreach (var row in dataRows.Take(20))
                {
                    fullTextBuilder.AppendLine(string.Join(" | ", row));
                }
            }

            sheetIdx++;
        }

        metadata["SheetsCount"] = tables.Count.ToString();

        return new ExtractedDocument
        {
            FilePath = filePath,
            FileName = fileName,
            Format = "XLSX",
            FullText = fullTextBuilder.ToString(),
            Tables = tables,
            Metadata = metadata
        };
    }

    private static ExtractedDocument ExtractPptx(string filePath, string fileName)
    {
        var sections = new List<DocumentSection>();
        var tables = new List<ExtractedTable>();
        var fullTextBuilder = new StringBuilder();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var zip = ZipFile.OpenRead(filePath);
        int slideNumber = 1;

        foreach (var entry in zip.Entries.Where(e => Regex.IsMatch(e.FullName, @"^ppt/slides/slide\d+\.xml$")).OrderBy(e => e.FullName))
        {
            using var sStream = entry.Open();
            var doc = XDocument.Load(sStream);
            XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";

            var slideTexts = doc.Descendants(a + "p")
                .Select(p => string.Join("", p.Descendants(a + "t").Select(t => t.Value)).Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            if (slideTexts.Count > 0)
            {
                var title = slideTexts[0];
                var body = string.Join("\n", slideTexts.Skip(1));
                fullTextBuilder.AppendLine($"--- Slide {slideNumber}: {title} ---");
                if (!string.IsNullOrEmpty(body)) fullTextBuilder.AppendLine(body);

                sections.Add(new DocumentSection
                {
                    Title = title,
                    Content = body,
                    SectionType = "Slide",
                    PageNumber = slideNumber
                });
            }

            // Tables in PPTX
            foreach (var tbl in doc.Descendants(a + "tbl"))
            {
                var rows = new List<IReadOnlyList<string>>();
                foreach (var tr in tbl.Elements(a + "tr"))
                {
                    var cells = tr.Elements(a + "tc")
                        .Select(tc => string.Join(" ", tc.Descendants(a + "t").Select(t => t.Value)).Trim())
                        .ToList();
                    if (cells.Count > 0) rows.Add(cells);
                }

                if (rows.Count > 0)
                {
                    tables.Add(new ExtractedTable
                    {
                        Name = $"Slide {slideNumber} Table",
                        PageOrSheetNumber = slideNumber,
                        Headers = rows[0],
                        Rows = rows.Skip(1).ToList()
                    });
                }
            }

            slideNumber++;
        }

        metadata["SlidesCount"] = (slideNumber - 1).ToString();

        return new ExtractedDocument
        {
            FilePath = filePath,
            FileName = fileName,
            Format = "PPTX",
            FullText = fullTextBuilder.ToString(),
            Sections = sections,
            Tables = tables,
            Metadata = metadata
        };
    }

    private static ExtractedDocument ExtractCsv(string filePath, string fileName)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length == 0)
        {
            return new ExtractedDocument { FilePath = filePath, FileName = fileName, Format = "CSV" };
        }

        // Detect delimiter: comma, semicolon, tab
        char delimiter = ',';
        var firstLine = lines[0];
        if (firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',')) delimiter = ';';
        else if (firstLine.Count(c => c == '\t') > firstLine.Count(c => c == ',')) delimiter = '\t';

        var rows = new List<IReadOnlyList<string>>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = ParseCsvLine(line, delimiter);
            rows.Add(parts);
        }

        var headers = rows.Count > 0 ? rows[0] : [];
        var dataRows = rows.Skip(1).ToList();

        var table = new ExtractedTable
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            PageOrSheetNumber = 1,
            Headers = headers,
            Rows = dataRows
        };

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(" | ", headers));
        foreach (var r in dataRows.Take(30))
        {
            sb.AppendLine(string.Join(" | ", r));
        }

        var metadata = new Dictionary<string, string>
        {
            ["RowCount"] = dataRows.Count.ToString(),
            ["ColumnCount"] = headers.Count.ToString(),
            ["Delimiter"] = delimiter == '\t' ? "Tab" : delimiter.ToString()
        };

        return new ExtractedDocument
        {
            FilePath = filePath,
            FileName = fileName,
            Format = "CSV",
            FullText = sb.ToString(),
            Tables = [table],
            Metadata = metadata
        };
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var list = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                list.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        list.Add(current.ToString().Trim());
        return list;
    }

    private static async Task<ExtractedDocument> ExtractTextFileAsync(string filePath, string fileName, string ext, CancellationToken ct)
    {
        var text = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var sections = new List<DocumentSection>();
        foreach (var line in lines)
        {
            var isHeading = line.StartsWith('#') || (line.Length > 0 && line.All(c => char.IsUpper(c) || char.IsWhiteSpace(c)) && line.Length < 60);
            sections.Add(new DocumentSection
            {
                Title = isHeading ? line.TrimStart('#', ' ') : string.Empty,
                Content = line,
                SectionType = isHeading ? "Heading" : "Paragraph",
                PageNumber = 1
            });
        }

        return new ExtractedDocument
        {
            FilePath = filePath,
            FileName = fileName,
            Format = ext.TrimStart('.').ToUpperInvariant(),
            FullText = text,
            Sections = sections,
            Metadata = new Dictionary<string, string>
            {
                ["LineCount"] = lines.Length.ToString(),
                ["CharacterCount"] = text.Length.ToString()
            }
        };
    }

    private static ExtractedDocument ExtractImageMetadata(string filePath, string fileName, string ext)
    {
        var fileInfo = new FileInfo(filePath);
        return new ExtractedDocument
        {
            FilePath = filePath,
            FileName = fileName,
            Format = ext.TrimStart('.').ToUpperInvariant(),
            FullText = $"[Image File: {fileName}, Size: {fileInfo.Length / 1024} KB. Use OCR to extract text.]",
            HasOcrContent = true,
            Metadata = new Dictionary<string, string>
            {
                ["SizeBytes"] = fileInfo.Length.ToString(),
                ["RequiresOcr"] = "True"
            }
        };
    }
}
