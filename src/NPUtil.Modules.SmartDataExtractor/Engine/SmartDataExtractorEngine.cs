using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NPUtil.Core.AI;
using NPUtil.Core.Extraction;
using NPUtil.Core.Logging;
using NPUtil.Modules.SmartDataExtractor.Models;

namespace NPUtil.Modules.SmartDataExtractor.Engine;

public class SmartDataExtractorEngine
{
    private readonly IDocumentExtractor _documentExtractor;
    private readonly IInferenceEngine _inferenceEngine;
    private readonly ILogger? _logger;

    public SmartDataExtractorEngine(
        IDocumentExtractor documentExtractor,
        IInferenceEngine inferenceEngine,
        ILogger? logger = null)
    {
        _documentExtractor = documentExtractor;
        _inferenceEngine = inferenceEngine;
        _logger = logger;
    }

    public async Task<SmartExtractionResult> ExtractAsync(
        string filePath,
        ExtractionCategory category,
        string? customInstruction = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!File.Exists(filePath))
        {
            return new SmartExtractionResult
            {
                SourceFilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileFormat = "Unknown",
                Success = false,
                ErrorMessage = $"File not found: {filePath}"
            };
        }

        var fileName = Path.GetFileName(filePath);
        var ext = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();

        try
        {
            // 1. Deterministic Extraction
            var doc = await _documentExtractor.ExtractAsync(filePath, ct);
            if (!string.IsNullOrEmpty(doc.ErrorMessage))
            {
                return new SmartExtractionResult
                {
                    SourceFilePath = filePath,
                    FileName = fileName,
                    FileFormat = ext,
                    Success = false,
                    ErrorMessage = doc.ErrorMessage
                };
            }

            // 2. Category-Specific Processing
            var result = category switch
            {
                ExtractionCategory.TablesAndData => ProcessTables(doc),
                ExtractionCategory.FormattedText => ProcessFormattedText(doc),
                ExtractionCategory.InvoicesAndReceipts => ProcessInvoice(doc),
                ExtractionCategory.ListsAndRecords => ProcessLists(doc),
                ExtractionCategory.DatesAndDeadlines => ProcessDates(doc),
                ExtractionCategory.StructuredJson => ProcessJson(doc),
                ExtractionCategory.Custom => await ProcessCustomAsync(doc, customInstruction, ct),
                _ => ProcessFormattedText(doc)
            };

            sw.Stop();
            return result with
            {
                SourceFilePath = filePath,
                FileName = fileName,
                FileFormat = ext,
                Category = category,
                ElapsedTime = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.Error("SmartDataExtractor", $"Extraction failed for {fileName}: {ex.Message}", ex);
            return new SmartExtractionResult
            {
                SourceFilePath = filePath,
                FileName = fileName,
                FileFormat = ext,
                Category = category,
                Success = false,
                ErrorMessage = ex.Message,
                ElapsedTime = sw.Elapsed
            };
        }
    }

    private static SmartExtractionResult ProcessTables(ExtractedDocument doc)
    {
        var headers = new List<string>();
        var rows = new List<IReadOnlyList<string>>();
        var refs = new List<string>();
        var uncertainties = new List<string>();

        if (doc.Tables.Count > 0)
        {
            var primary = doc.Tables[0];
            headers.AddRange(primary.Headers);
            rows.AddRange(primary.Rows);
            refs.Add($"Primary table from {primary.Name} (Page/Sheet {primary.PageOrSheetNumber})");

            for (int i = 1; i < doc.Tables.Count; i++)
            {
                refs.Add($"Additional table {i + 1}: {doc.Tables[i].Name} ({doc.Tables[i].Rows.Count} rows)");
            }
        }
        else
        {
            uncertainties.Add("No explicit table grid found. Fallback to line columns.");
            headers.AddRange(["Line #", "Content"]);
            var lines = doc.FullText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int idx = 1;
            foreach (var l in lines.Take(50))
            {
                rows.Add([idx++.ToString(), l.Trim()]);
            }
        }

        var json = JsonSerializer.Serialize(new
        {
            headers,
            rowCount = rows.Count,
            rows
        }, new JsonSerializerOptions { WriteIndented = true });

        return new SmartExtractionResult
        {
            SourceFilePath = doc.FilePath,
            FileName = doc.FileName,
            FileFormat = doc.Format,
            Title = $"Tables & Data ({rows.Count} rows)",
            TableHeaders = headers,
            TableRows = rows,
            JsonOutput = json,
            FormattedText = doc.FullText,
            SourceReferences = refs,
            Uncertainties = uncertainties,
            Confidence = doc.Tables.Count > 0 ? 0.95 : 0.65
        };
    }

    private static SmartExtractionResult ProcessFormattedText(ExtractedDocument doc)
    {
        var sb = new StringBuilder();
        var refs = new List<string>();

        foreach (var sec in doc.Sections)
        {
            if (!string.IsNullOrEmpty(sec.Title))
            {
                sb.AppendLine($"\n### {sec.Title}");
            }
            sb.AppendLine(sec.Content);
            refs.Add($"{sec.SectionType} (Page {sec.PageNumber})");
        }

        var text = sb.Length > 0 ? sb.ToString() : doc.FullText;

        var json = JsonSerializer.Serialize(new
        {
            sectionsCount = doc.Sections.Count,
            textLength = text.Length,
            sections = doc.Sections
        }, new JsonSerializerOptions { WriteIndented = true });

        return new SmartExtractionResult
        {
            SourceFilePath = doc.FilePath,
            FileName = doc.FileName,
            FileFormat = doc.Format,
            Title = "Formatted Document Structure",
            FormattedText = text,
            JsonOutput = json,
            SourceReferences = refs.Take(10).ToList(),
            Confidence = 0.98
        };
    }

    private static SmartExtractionResult ProcessInvoice(ExtractedDocument doc)
    {
        var text = doc.FullText;
        var uncertainties = new List<string>();
        var refs = new List<string>();

        // Detect Vendor
        var vendorMatch = Regex.Match(text, @"(?:Dodavatel|Vendor|Supplier|From):\s*([^\r\n]+)", RegexOptions.IgnoreCase);
        var vendor = vendorMatch.Success ? vendorMatch.Groups[1].Value.Trim() : string.Empty;
        if (string.IsNullOrEmpty(vendor))
        {
            vendor = text.Split('\n').FirstOrDefault(l => l.Length > 3 && l.Length < 50)?.Trim() ?? "Vendor (Inferred)";
            uncertainties.Add("Vendor extracted by positional inference.");
        }
        else refs.Add("Vendor matched via keyword");

        // Detect Invoice Number
        var invNumMatch = Regex.Match(text, @"(?:Faktura\s*č(?:íslo)?\.?|Invoice\s*(?:No|#)?\.?|Daňový\s*doklad\s*č\.?)\s*[:]?\s*([A-Za-z0-9\-\/]+)", RegexOptions.IgnoreCase);
        var invoiceNum = invNumMatch.Success ? invNumMatch.Groups[1].Value.Trim() : "N/A";
        if (invNumMatch.Success) refs.Add($"Invoice number matched: {invoiceNum}");

        // Detect Dates
        var dateMatch = Regex.Match(text, @"(?:Datum\s*vystavení|Issue\s*Date|Date)\s*[:]?\s*(\d{1,2}[\.\/\-]\d{1,2}[\.\/\-]\d{2,4})", RegexOptions.IgnoreCase);
        var issueDate = dateMatch.Success ? dateMatch.Groups[1].Value.Trim() : DateTime.Now.ToString("dd.MM.yyyy");

        var dueMatch = Regex.Match(text, @"(?:Datum\s*splatnosti|Due\s*Date)\s*[:]?\s*(\d{1,2}[\.\/\-]\d{1,2}[\.\/\-]\d{2,4})", RegexOptions.IgnoreCase);
        var dueDate = dueMatch.Success ? dueMatch.Groups[1].Value.Trim() : "N/A";

        // Detect Total Amount & Currency
        var totalMatch = Regex.Match(text, @"(?:Celkem\s*(?:k\s*úhradě)?|Total\s*(?:Amount)?|Částka)\s*[:]?\s*([\d\s\.,]+)\s*(Kč|CZK|EUR|€|\$|USD)?", RegexOptions.IgnoreCase);
        var total = totalMatch.Success ? totalMatch.Groups[1].Value.Trim() : "0.00";
        var currency = totalMatch.Groups[2].Success && !string.IsNullOrEmpty(totalMatch.Groups[2].Value) ? totalMatch.Groups[2].Value.Trim() : "CZK";

        // Detect Items
        var items = new List<InvoiceItem>();
        if (doc.Tables.Count > 0 && doc.Tables[0].Rows.Count > 0)
        {
            foreach (var r in doc.Tables[0].Rows)
            {
                if (r.Count >= 2)
                {
                    items.Add(new InvoiceItem
                    {
                        Description = r[0],
                        Quantity = r.Count > 2 ? r[1] : "1",
                        UnitPrice = r.Count > 3 ? r[2] : "",
                        TotalPrice = r.Last()
                    });
                }
            }
            refs.Add($"Items extracted from document table ({items.Count} items)");
        }
        else
        {
            // Line items pattern
            var lineMatches = Regex.Matches(text, @"(?:^|\n)([\w\s\.\-]{4,40})\s+(\d+)\s+([\d\s\.,]+)\s+(?:Kč|CZK|EUR|€)");
            foreach (Match m in lineMatches)
            {
                items.Add(new InvoiceItem
                {
                    Description = m.Groups[1].Value.Trim(),
                    Quantity = m.Groups[2].Value.Trim(),
                    UnitPrice = m.Groups[3].Value.Trim(),
                    TotalPrice = m.Groups[3].Value.Trim()
                });
            }
        }

        var invoice = new InvoiceData
        {
            Vendor = vendor,
            InvoiceNumber = invoiceNum,
            IssueDate = issueDate,
            DueDate = dueDate,
            TotalAmount = total,
            Currency = currency,
            Items = items
        };

        var headers = new List<string> { "Description", "Quantity", "Unit Price", "Total Price" };
        var rows = items.Select(i => (IReadOnlyList<string>)[i.Description, i.Quantity, i.UnitPrice, i.TotalPrice]).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Vendor: {vendor}");
        sb.AppendLine($"Invoice #: {invoiceNum}");
        sb.AppendLine($"Issue Date: {issueDate} | Due Date: {dueDate}");
        sb.AppendLine($"Total Amount Due: {total} {currency}");
        sb.AppendLine();
        sb.AppendLine("Line Items:");
        foreach (var item in items)
        {
            sb.AppendLine($" • {item.Description} | Qty: {item.Quantity} | Total: {item.TotalPrice}");
        }

        return new SmartExtractionResult
        {
            SourceFilePath = doc.FilePath,
            FileName = doc.FileName,
            FileFormat = doc.Format,
            Title = $"Invoice {invoiceNum} ({vendor})",
            Invoice = invoice,
            TableHeaders = headers,
            TableRows = rows,
            FormattedText = sb.ToString(),
            JsonOutput = JsonSerializer.Serialize(invoice, new JsonSerializerOptions { WriteIndented = true }),
            SourceReferences = refs,
            Uncertainties = uncertainties,
            Confidence = 0.90
        };
    }

    private static SmartExtractionResult ProcessLists(ExtractedDocument doc)
    {
        var text = doc.FullText;
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var records = new List<IReadOnlyList<string>>();

        int index = 1;
        foreach (var l in lines)
        {
            if (Regex.IsMatch(l, @"^(\*|\-|\•|\d+[\.\)])\s+"))
            {
                var clean = Regex.Replace(l, @"^(\*|\-|\•|\d+[\.\)])\s+", "");
                records.Add([index++.ToString(), clean]);
            }
            else if (l.Contains(':') && l.Length < 80)
            {
                var parts = l.Split(':', 2);
                records.Add([parts[0].Trim(), parts[1].Trim()]);
            }
        }

        var headers = new List<string> { "Item / Key", "Value / Description" };

        var sb = new StringBuilder();
        sb.AppendLine($"Identified {records.Count} records / list entries:");
        foreach (var r in records)
        {
            sb.AppendLine($"• [{r[0]}]: {r[1]}");
        }

        return new SmartExtractionResult
        {
            SourceFilePath = doc.FilePath,
            FileName = doc.FileName,
            FileFormat = doc.Format,
            Title = $"Lists & Records ({records.Count} items)",
            TableHeaders = headers,
            TableRows = records,
            FormattedText = sb.ToString(),
            JsonOutput = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }),
            SourceReferences = [$"Extracted from {doc.FileName} lines"],
            Confidence = 0.92
        };
    }

    private static SmartExtractionResult ProcessDates(ExtractedDocument doc)
    {
        var text = doc.FullText;
        var dateMatches = Regex.Matches(text, @"\b(\d{1,2}[\.\/\-]\d{1,2}[\.\/\-]\d{2,4})\b");
        var records = new List<ExtractedDateRecord>();
        var rows = new List<IReadOnlyList<string>>();

        foreach (Match m in dateMatches)
        {
            var dateStr = m.Value;
            int start = Math.Max(0, m.Index - 40);
            int len = Math.Min(text.Length - start, 120);
            var snippet = text.Substring(start, len).Replace('\r', ' ').Replace('\n', ' ').Trim();

            string type = "Date";
            var lowerSnippet = snippet.ToLowerInvariant();
            if (lowerSnippet.Contains("deadline") || lowerSnippet.Contains("termín") || lowerSnippet.Contains("do ")) type = "Deadline";
            else if (lowerSnippet.Contains("splatnost") || lowerSnippet.Contains("due")) type = "Due Date";
            else if (lowerSnippet.Contains("schůzka") || lowerSnippet.Contains("meeting") || lowerSnippet.Contains("porada")) type = "Appointment";

            records.Add(new ExtractedDateRecord
            {
                DateText = dateStr,
                Context = snippet,
                RecordType = type
            });

            rows.Add([dateStr, type, snippet]);
        }

        var headers = new List<string> { "Date", "Type", "Surrounding Context" };

        var sb = new StringBuilder();
        sb.AppendLine($"Found {records.Count} Dates & Deadlines:");
        foreach (var r in records)
        {
            sb.AppendLine($"• [{r.RecordType}] {r.DateText} — Context: \"{r.Context}\"");
        }

        return new SmartExtractionResult
        {
            SourceFilePath = doc.FilePath,
            FileName = doc.FileName,
            FileFormat = doc.Format,
            Title = $"Dates & Deadlines ({records.Count} dates found)",
            DateRecords = records,
            TableHeaders = headers,
            TableRows = rows,
            FormattedText = sb.ToString(),
            JsonOutput = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }),
            SourceReferences = [$"Scanned {text.Length} characters across {doc.FileName}"],
            Confidence = 0.95
        };
    }

    private static SmartExtractionResult ProcessJson(ExtractedDocument doc)
    {
        var obj = new
        {
            fileName = doc.FileName,
            format = doc.Format,
            extractedAt = DateTime.UtcNow,
            metadata = doc.Metadata,
            sections = doc.Sections.Select(s => new { s.Title, s.SectionType, s.PageNumber, s.Content }),
            tables = doc.Tables.Select(t => new { t.Name, t.PageOrSheetNumber, t.Headers, t.Rows })
        };

        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });

        return new SmartExtractionResult
        {
            SourceFilePath = doc.FilePath,
            FileName = doc.FileName,
            FileFormat = doc.Format,
            Title = "Structured JSON Export",
            FormattedText = json,
            JsonOutput = json,
            SourceReferences = [$"Full structured serialization of {doc.FileName}"],
            Confidence = 1.0
        };
    }

    private async Task<SmartExtractionResult> ProcessCustomAsync(ExtractedDocument doc, string? customInstruction, CancellationToken ct)
    {
        var instruction = customInstruction ?? "Extract structured summary and data";
        var aiOutput = await _inferenceEngine.CustomInstructAsync(instruction, doc.FullText, ct);

        return new SmartExtractionResult
        {
            SourceFilePath = doc.FilePath,
            FileName = doc.FileName,
            FileFormat = doc.Format,
            Title = $"Custom Extraction: \"{instruction}\"",
            FormattedText = aiOutput,
            JsonOutput = JsonSerializer.Serialize(new { instruction, result = aiOutput }, new JsonSerializerOptions { WriteIndented = true }),
            SourceReferences = [$"Custom prompt executed locally on {doc.FileName}"],
            Confidence = 0.88
        };
    }
}
