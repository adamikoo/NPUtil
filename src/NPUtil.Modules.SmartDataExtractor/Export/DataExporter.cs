using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using NPUtil.Modules.SmartDataExtractor.Models;

namespace NPUtil.Modules.SmartDataExtractor.Export;

public class DataExporter
{
    public static void ExportToCsv(SmartExtractionResult result, string destinationPath)
    {
        var sb = new StringBuilder();
        if (result.TableHeaders.Count > 0)
        {
            sb.AppendLine(string.Join(",", result.TableHeaders.Select(EscapeCsv)));
            foreach (var row in result.TableRows)
            {
                sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            }
        }
        else if (result.DateRecords.Count > 0)
        {
            sb.AppendLine("Date,Type,Context,Page");
            foreach (var d in result.DateRecords)
            {
                sb.AppendLine($"{EscapeCsv(d.DateText)},{EscapeCsv(d.RecordType)},{EscapeCsv(d.Context)},{d.PageNumber}");
            }
        }
        else if (result.Invoice != null)
        {
            sb.AppendLine("Field,Value");
            sb.AppendLine($"Vendor,{EscapeCsv(result.Invoice.Vendor)}");
            sb.AppendLine($"InvoiceNumber,{EscapeCsv(result.Invoice.InvoiceNumber)}");
            sb.AppendLine($"Total,{EscapeCsv(result.Invoice.TotalAmount)} {result.Invoice.Currency}");
            sb.AppendLine($"DueDate,{EscapeCsv(result.Invoice.DueDate)}");
            sb.AppendLine();
            sb.AppendLine("Description,Quantity,UnitPrice,TotalPrice");
            foreach (var item in result.Invoice.Items)
            {
                sb.AppendLine($"{EscapeCsv(item.Description)},{EscapeCsv(item.Quantity)},{EscapeCsv(item.UnitPrice)},{EscapeCsv(item.TotalPrice)}");
            }
        }
        else
        {
            sb.AppendLine("ExtractedText");
            sb.AppendLine(EscapeCsv(result.FormattedText));
        }

        File.WriteAllText(destinationPath, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportToJson(SmartExtractionResult result, string destinationPath)
    {
        File.WriteAllText(destinationPath, result.JsonOutput, Encoding.UTF8);
    }

    public static void ExportToTxt(SmartExtractionResult result, string destinationPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== NPUtil Smart Data Extraction: {result.FileName} ===");
        sb.AppendLine($"Category: {result.Category}");
        sb.AppendLine($"Confidence: {result.Confidence:P0}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();
        sb.AppendLine(result.FormattedText);

        if (result.SourceReferences.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- Source References ---");
            foreach (var r in result.SourceReferences)
            {
                sb.AppendLine($"• {r}");
            }
        }

        File.WriteAllText(destinationPath, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportToXlsx(SmartExtractionResult result, string destinationPath)
    {
        // Generate valid XLSX by creating a standard OpenXml Zip package
        if (File.Exists(destinationPath)) File.Delete(destinationPath);

        using var zip = ZipFile.Open(destinationPath, ZipArchiveMode.Create);

        // [Content_Types].xml
        var ctEntry = zip.CreateEntry("[Content_Types].xml");
        using (var sw = new StreamWriter(ctEntry.Open(), Encoding.UTF8))
        {
            sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
    <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
    <Default Extension=""xml"" ContentType=""application/xml""/>
    <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
    <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
</Types>");
        }

        // _rels/.rels
        var relsEntry = zip.CreateEntry("_rels/.rels");
        using (var sw = new StreamWriter(relsEntry.Open(), Encoding.UTF8))
        {
            sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");
        }

        // xl/_rels/workbook.xml.rels
        var wbRelsEntry = zip.CreateEntry("xl/_rels/workbook.xml.rels");
        using (var sw = new StreamWriter(wbRelsEntry.Open(), Encoding.UTF8))
        {
            sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
</Relationships>");
        }

        // xl/workbook.xml
        var wbEntry = zip.CreateEntry("xl/workbook.xml");
        using (var sw = new StreamWriter(wbEntry.Open(), Encoding.UTF8))
        {
            sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
    <sheets>
        <sheet name=""ExtractedData"" sheetId=""1"" r:id=""rId1""/>
    </sheets>
</workbook>");
        }

        // xl/worksheets/sheet1.xml
        var sheetEntry = zip.CreateEntry("xl/worksheets/sheet1.xml");
        using (var sw = new StreamWriter(sheetEntry.Open(), Encoding.UTF8))
        {
            sw.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sw.WriteLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">");
            sw.WriteLine(@"<sheetData>");

            int rowIdx = 1;

            // Write Headers
            if (result.TableHeaders.Count > 0)
            {
                sw.WriteLine($@"<row r=""{rowIdx}"">");
                for (int c = 0; c < result.TableHeaders.Count; c++)
                {
                    var colRef = GetColRef(c) + rowIdx;
                    sw.WriteLine($@"<c r=""{colRef}"" t=""inlineStr""><is><t>{EscapeXml(result.TableHeaders[c])}</t></is></c>");
                }
                sw.WriteLine(@"</row>");
                rowIdx++;

                foreach (var row in result.TableRows)
                {
                    sw.WriteLine($@"<row r=""{rowIdx}"">");
                    for (int c = 0; c < row.Count; c++)
                    {
                        var colRef = GetColRef(c) + rowIdx;
                        sw.WriteLine($@"<c r=""{colRef}"" t=""inlineStr""><is><t>{EscapeXml(row[c])}</t></is></c>");
                    }
                    sw.WriteLine(@"</row>");
                    rowIdx++;
                }
            }
            else
            {
                sw.WriteLine($@"<row r=""{rowIdx}""><c r=""A{rowIdx}"" t=""inlineStr""><is><t>{EscapeXml(result.FormattedText)}</t></is></c></row>");
            }

            sw.WriteLine(@"</sheetData>");
            sw.WriteLine(@"</worksheet>");
        }
    }

    public static void ExportToDocx(SmartExtractionResult result, string destinationPath)
    {
        if (File.Exists(destinationPath)) File.Delete(destinationPath);

        using var zip = ZipFile.Open(destinationPath, ZipArchiveMode.Create);

        // [Content_Types].xml
        var ctEntry = zip.CreateEntry("[Content_Types].xml");
        using (var sw = new StreamWriter(ctEntry.Open(), Encoding.UTF8))
        {
            sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
    <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
    <Default Extension=""xml"" ContentType=""application/xml""/>
    <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
</Types>");
        }

        // _rels/.rels
        var relsEntry = zip.CreateEntry("_rels/.rels");
        using (var sw = new StreamWriter(relsEntry.Open(), Encoding.UTF8))
        {
            sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
    <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
</Relationships>");
        }

        // word/document.xml
        var docEntry = zip.CreateEntry("word/document.xml");
        using (var sw = new StreamWriter(docEntry.Open(), Encoding.UTF8))
        {
            sw.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sw.WriteLine(@"<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">");
            sw.WriteLine(@"<w:body>");

            // Heading
            sw.WriteLine($@"<w:p><w:pPr><w:pStyle w:val=""Heading1""/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val=""36""/></w:rPr><w:t>NPUtil Extraction: {EscapeXml(result.FileName)}</w:t></w:r></w:p>");

            // Content paragraphs
            var lines = result.FormattedText.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                sw.WriteLine($@"<w:p><w:r><w:t>{EscapeXml(line.Trim())}</w:t></w:r></w:p>");
            }

            sw.WriteLine(@"</w:body></w:document>");
        }
    }

    private static string GetColRef(int index)
    {
        string refStr = "";
        while (index >= 0)
        {
            refStr = (char)('A' + (index % 26)) + refStr;
            index = (index / 26) - 1;
        }
        return refStr;
    }

    private static string EscapeCsv(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        if (val.Contains(',') || val.Contains('"') || val.Contains('\n') || val.Contains('\r'))
        {
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }
        return val;
    }

    private static string EscapeXml(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("&", "&amp;")
                  .Replace("<", "&lt;")
                  .Replace(">", "&gt;")
                  .Replace("\"", "&quot;")
                  .Replace("'", "&apos;");
    }
}
