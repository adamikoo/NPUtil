using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NPUtil.Modules.FileOrganizer.Models;
using UglyToad.PdfPig;

namespace NPUtil.Modules.FileOrganizer.Extraction;

public class FileContentExtractor
{
    private const int MaxTextCharacters = 16_000; // Cap to 16k chars for instant memory-efficient semantic embedding
    private static readonly Regex WordSplitRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    public async Task<ExtractedMetadata> ExtractAsync(string filePath, CancellationToken ct = default)
    {
        var fileInfo = new FileInfo(filePath);
        var ext = fileInfo.Extension.ToLowerInvariant();
        var mime = GetMimeType(ext);

        var additionalProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FileSizeBytes"] = fileInfo.Length.ToString(),
            ["Extension"] = ext,
            ["Created"] = fileInfo.CreationTime.ToString("o"),
            ["Modified"] = fileInfo.LastWriteTime.ToString("o")
        };

        var extractedText = string.Empty;

        try
        {
            switch (ext)
            {
                case ".pdf":
                    extractedText = await Task.Run(() => ExtractPdf(filePath, additionalProps), ct);
                    break;

                case ".docx":
                    extractedText = await Task.Run(() => ExtractDocx(filePath, additionalProps), ct);
                    break;

                case ".xlsx":
                    extractedText = await Task.Run(() => ExtractXlsx(filePath, additionalProps), ct);
                    break;

                case ".pptx":
                    extractedText = await Task.Run(() => ExtractPptx(filePath, additionalProps), ct);
                    break;

                case ".txt":
                case ".md":
                case ".csv":
                case ".tsv":
                case ".log":
                case ".json":
                case ".xml":
                case ".yaml":
                case ".yml":
                case ".ini":
                case ".config":
                    extractedText = await ExtractPlainTextAsync(filePath, ct);
                    break;

                case ".cs":
                case ".py":
                case ".js":
                case ".ts":
                case ".jsx":
                case ".tsx":
                case ".cpp":
                case ".c":
                case ".h":
                case ".hpp":
                case ".rs":
                case ".go":
                case ".java":
                case ".html":
                case ".css":
                case ".sql":
                case ".sh":
                case ".bat":
                case ".ps1":
                case ".mq5":
                case ".mq4":
                    extractedText = await ExtractCodeAsync(filePath, ct);
                    break;

                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".webp":
                case ".bmp":
                case ".gif":
                    extractedText = await Task.Run(() => ExtractImageMetadata(filePath, additionalProps), ct);
                    break;

                case ".mp3":
                case ".wav":
                case ".flac":
                case ".mp4":
                case ".mkv":
                case ".mov":
                case ".avi":
                    extractedText = await Task.Run(() => ExtractMediaMetadata(filePath, additionalProps), ct);
                    break;

                default:
                    // For unsupported binaries/archives, use filename and metadata
                    extractedText = Path.GetFileNameWithoutExtension(filePath);
                    break;
            }
        }
        catch (Exception ex)
        {
            additionalProps["ExtractionError"] = ex.Message;
            extractedText = Path.GetFileNameWithoutExtension(filePath);
        }

        if (extractedText.Length > MaxTextCharacters)
        {
            extractedText = extractedText[..MaxTextCharacters];
        }

        var keywords = ExtractKeywords(Path.GetFileNameWithoutExtension(filePath) + " " + extractedText);

        return new ExtractedMetadata(
            FilePath: filePath,
            FileName: fileInfo.Name,
            Extension: ext,
            FileSizeBytes: fileInfo.Length,
            CreatedTime: fileInfo.CreationTime,
            LastModifiedTime: fileInfo.LastWriteTime,
            MimeType: mime,
            ExtractedText: extractedText,
            Keywords: keywords,
            AdditionalProperties: additionalProps
        );
    }

    private static string ExtractPdf(string filePath, Dictionary<string, string> props)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(filePath);
        
        if (doc.Information != null)
        {
            if (!string.IsNullOrWhiteSpace(doc.Information.Title))
            {
                props["Title"] = doc.Information.Title;
                sb.AppendLine(doc.Information.Title);
            }
            if (!string.IsNullOrWhiteSpace(doc.Information.Author))
            {
                props["Author"] = doc.Information.Author;
            }
            if (!string.IsNullOrWhiteSpace(doc.Information.Subject))
            {
                props["Subject"] = doc.Information.Subject;
                sb.AppendLine(doc.Information.Subject);
            }
            if (!string.IsNullOrWhiteSpace(doc.Information.Keywords))
            {
                props["Keywords"] = doc.Information.Keywords;
                sb.AppendLine(doc.Information.Keywords);
            }
        }

        props["PageCount"] = doc.NumberOfPages.ToString();

        // Read up to first 5 pages for rapid semantic extraction
        var maxPages = Math.Min(5, doc.NumberOfPages);
        for (var i = 1; i <= maxPages; i++)
        {
            var page = doc.GetPage(i);
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
            if (sb.Length >= MaxTextCharacters) break;
        }

        return sb.ToString();
    }

    private static string ExtractDocx(string filePath, Dictionary<string, string> props)
    {
        using var zip = ZipFile.OpenRead(filePath);
        var entry = zip.GetEntry("word/document.xml");
        if (entry == null) return string.Empty;

        using var stream = entry.Open();
        var xdoc = XDocument.Load(stream);
        var texts = xdoc.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value);
        return string.Join(" ", texts);
    }

    private static string ExtractXlsx(string filePath, Dictionary<string, string> props)
    {
        using var zip = ZipFile.OpenRead(filePath);
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return string.Empty;

        using var stream = entry.Open();
        var xdoc = XDocument.Load(stream);
        var texts = xdoc.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value);
        return string.Join(" ", texts);
    }

    private static string ExtractPptx(string filePath, Dictionary<string, string> props)
    {
        using var zip = ZipFile.OpenRead(filePath);
        var sb = new StringBuilder();

        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml")))
        {
            using var stream = entry.Open();
            var xdoc = XDocument.Load(stream);
            var texts = xdoc.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value);
            sb.AppendLine(string.Join(" ", texts));
            if (sb.Length >= MaxTextCharacters) break;
        }

        return sb.ToString();
    }

    private static async Task<string> ExtractPlainTextAsync(string filePath, CancellationToken ct)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var buffer = new char[MaxTextCharacters];
        var charsRead = await reader.ReadBlockAsync(buffer, 0, MaxTextCharacters);
        return new string(buffer, 0, charsRead);
    }

    private static async Task<string> ExtractCodeAsync(string filePath, CancellationToken ct)
    {
        var raw = await ExtractPlainTextAsync(filePath, ct);
        // Normalize whitespace
        return Regex.Replace(raw, @"\s+", " ");
    }

    private static string ExtractImageMetadata(string filePath, Dictionary<string, string> props)
    {
        var fi = new FileInfo(filePath);
        var sb = new StringBuilder();
        sb.Append(Path.GetFileNameWithoutExtension(filePath));

        // Read basic image dimensions header
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[32];
            var read = fs.Read(buffer, 0, buffer.Length);
            if (read > 24)
            {
                // PNG dimension parsing
                if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                {
                    var width = (buffer[16] << 24) | (buffer[17] << 16) | (buffer[18] << 8) | buffer[19];
                    var height = (buffer[20] << 24) | (buffer[21] << 16) | (buffer[22] << 8) | buffer[23];
                    props["Dimensions"] = $"{width}x{height}";
                    if (width >= 2560 || height >= 1440)
                    {
                        sb.Append(" 4K high resolution wallpaper");
                    }
                }
            }
        }
        catch { /* ignore */ }

        return sb.ToString();
    }

    private static string ExtractMediaMetadata(string filePath, Dictionary<string, string> props)
    {
        return Path.GetFileNameWithoutExtension(filePath);
    }

    public static IReadOnlyList<string> ExtractKeywords(string text, int maxKeywords = 12)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "with", "that", "this", "from", "are", "have", "has", "not", "but", "all", "you",
            "a", "an", "in", "on", "at", "to", "by", "is", "was", "be", "of", "or", "as", "it",
            // Czech stopwords
            "a", "i", "že", "se", "na", "v", "ve", "do", "k", "ke", "o", "po", "za", "ze", "z", "od",
            "pro", "je", "jsou", "byl", "byla", "bylo", "to", "ten", "ta", "ty", "jak", "tak", "kde", "kdo", "co"
        };

        var matches = WordSplitRegex.Matches(text);
        var frequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            var word = match.Value.ToLowerInvariant();
            if (word.Length < 3 || stopWords.Contains(word)) continue;
            frequencies[word] = frequencies.GetValueOrDefault(word) + 1;
        }

        return frequencies
            .OrderByDescending(kv => kv.Value)
            .Take(maxKeywords)
            .Select(kv => kv.Key)
            .ToList();
    }

    private static string GetMimeType(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".txt" => "text/plain",
        ".md" => "text/markdown",
        ".csv" => "text/csv",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".zip" => "application/zip",
        ".7z" => "application/x-7z-compressed",
        ".rar" => "application/vnd.rar",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".exe" => "application/vnd.microsoft.portable-executable",
        ".msi" => "application/x-msi",
        ".cs" => "text/x-csharp",
        ".py" => "text/x-python",
        ".js" => "text/javascript",
        ".ts" => "text/typescript",
        ".mp3" => "audio/mpeg",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream"
    };
}
