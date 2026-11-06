using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using NPUtil.Core.Logging;

namespace NPUtil.Core.OCR;

public class WindowsOcrService : IOcrService
{
    private readonly ILogger? _logger;
    private readonly Dictionary<string, OcrEngine> _engines = new(StringComparer.OrdinalIgnoreCase);

    public WindowsOcrService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> GetAvailableLanguages()
    {
        try
        {
            return OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToList();
        }
        catch (Exception ex)
        {
            _logger?.Warning("OCR", $"Failed to query OCR languages: {ex.Message}", ex);
            return ["cs", "en-US"];
        }
    }

    public bool IsLanguageSupported(string languageTag)
    {
        try
        {
            var lang = new Language(languageTag);
            return OcrEngine.IsLanguageSupported(lang);
        }
        catch
        {
            return false;
        }
    }

    private OcrEngine GetEngine(string languageTag)
    {
        if (_engines.TryGetValue(languageTag, out var existing))
        {
            return existing;
        }

        OcrEngine engine;
        try
        {
            var lang = new Language(languageTag);
            if (OcrEngine.IsLanguageSupported(lang))
            {
                engine = OcrEngine.TryCreateFromLanguage(lang);
            }
            else
            {
                engine = OcrEngine.TryCreateFromUserProfileLanguages();
            }
        }
        catch
        {
            engine = OcrEngine.TryCreateFromUserProfileLanguages();
        }

        _engines[languageTag] = engine;
        return engine;
    }

    public async Task<OcrResultData> RecognizeAsync(string imagePath, string languageTag = "cs", CancellationToken ct = default)
    {
        if (!File.Exists(imagePath))
        {
            return new OcrResultData
            {
                FullText = string.Empty,
                Success = false,
                ErrorMessage = $"Image file not found: {imagePath}"
            };
        }

        var bytes = await File.ReadAllBytesAsync(imagePath, ct);
        return await RecognizeBytesAsync(bytes, languageTag, ct);
    }

    public async Task<OcrResultData> RecognizeBytesAsync(byte[] imageBytes, string languageTag = "cs", CancellationToken ct = default)
    {
        try
        {
            var engine = GetEngine(languageTag);

            using var memoryStream = new InMemoryRandomAccessStream();
            await memoryStream.WriteAsync(imageBytes.AsBuffer());
            memoryStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(memoryStream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            // Convert to supported format if needed (Bgra8 with Premultiplied or Nv12)
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            var ocrResult = await engine.RecognizeAsync(softwareBitmap);

            var lines = new List<OcrLineInfo>();
            var fullText = new StringBuilder();

            foreach (var line in ocrResult.Lines)
            {
                var lineText = line.Text;
                fullText.AppendLine(lineText);

                lines.Add(new OcrLineInfo
                {
                    Text = lineText,
                    X = line.Words.FirstOrDefault()?.BoundingRect.X ?? 0,
                    Y = line.Words.FirstOrDefault()?.BoundingRect.Y ?? 0,
                    Width = line.Words.LastOrDefault()?.BoundingRect.Right - (line.Words.FirstOrDefault()?.BoundingRect.Left ?? 0) ?? 0,
                    Height = line.Words.FirstOrDefault()?.BoundingRect.Height ?? 0
                });
            }

            return new OcrResultData
            {
                FullText = fullText.ToString().Trim(),
                Lines = lines,
                Language = ocrResult.TextAngle.HasValue ? languageTag : languageTag,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger?.Error("OCR", $"OCR Recognition failed: {ex.Message}", ex);
            return new OcrResultData
            {
                FullText = string.Empty,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
