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
        var tag = string.IsNullOrWhiteSpace(languageTag) || languageTag.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? "auto"
            : languageTag;

        if (_engines.TryGetValue(tag, out var existing))
        {
            return existing;
        }

        OcrEngine? engine = null;
        try
        {
            if (tag != "auto")
            {
                var lang = new Language(tag);
                if (OcrEngine.IsLanguageSupported(lang))
                {
                    engine = OcrEngine.TryCreateFromLanguage(lang);
                }
            }

            if (engine == null)
            {
                // Prioritize user profile languages matching system display & keyboard
                engine = OcrEngine.TryCreateFromUserProfileLanguages();
            }

            if (engine == null && OcrEngine.AvailableRecognizerLanguages.Count > 0)
            {
                engine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning("OCR", $"Failed to initialize OCR engine for '{tag}': {ex.Message}");
            engine = OcrEngine.TryCreateFromUserProfileLanguages();
        }

        engine ??= OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine != null)
        {
            _engines[tag] = engine;
        }

        return engine!;
    }

    public async Task<OcrResultData> RecognizeAsync(string imagePath, string languageTag = "auto", CancellationToken ct = default)
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

    public async Task<OcrResultData> RecognizeBytesAsync(byte[] imageBytes, string languageTag = "auto", CancellationToken ct = default)
    {
        try
        {
            var engine = GetEngine(languageTag);
            var result = await ExecuteEngineAsync(engine, imageBytes);

            // If the image is dark or recognized few words, perform dual-pass inverted OCR
            bool isDark = IsPredominantlyDark(imageBytes);
            if (isDark || result.WordCount < 3)
            {
                try
                {
                    var invertedBytes = InvertImageBytes(imageBytes);
                    var invertedResult = await ExecuteEngineAsync(engine, invertedBytes);

                    if (invertedResult.WordCount > result.WordCount ||
                        (isDark && invertedResult.WordCount >= result.WordCount && invertedResult.FullText.Length > result.FullText.Length))
                    {
                        return invertedResult;
                    }
                }
                catch
                {
                    // Fall back to primary pass if inversion pipeline encounters an anomaly
                }
            }

            return result;
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

    private static async Task<OcrResultData> ExecuteEngineAsync(OcrEngine engine, byte[] imageBytes)
    {
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
            WordCount = lines.Sum(l => l.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length),
            Language = engine.RecognizerLanguage?.LanguageTag ?? "unknown",
            Success = true
        };
    }

    private static bool IsPredominantlyDark(byte[] imageBytes)
    {
        try
        {
            using var ms = new MemoryStream(imageBytes);
            using var bmp = new System.Drawing.Bitmap(ms);
            int stepX = Math.Max(1, bmp.Width / 20);
            int stepY = Math.Max(1, bmp.Height / 20);
            long totalLuma = 0;
            int count = 0;

            for (int x = 0; x < bmp.Width; x += stepX)
            {
                for (int y = 0; y < bmp.Height; y += stepY)
                {
                    var c = bmp.GetPixel(x, y);
                    totalLuma += (long)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    count++;
                }
            }

            return count > 0 && (totalLuma / count) < 115;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] InvertImageBytes(byte[] imageBytes)
    {
        using var msIn = new MemoryStream(imageBytes);
        using var bmp = new System.Drawing.Bitmap(msIn);
        using var inverted = new System.Drawing.Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (var g = System.Drawing.Graphics.FromImage(inverted))
        {
            var colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
            {
                [-1,  0,  0,  0, 0],
                [ 0, -1,  0,  0, 0],
                [ 0,  0, -1,  0, 0],
                [ 0,  0,  0,  1, 0],
                [ 1,  1,  1,  0, 1]
            });

            using var attr = new System.Drawing.Imaging.ImageAttributes();
            attr.SetColorMatrix(colorMatrix);
            g.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                0, 0, bmp.Width, bmp.Height, System.Drawing.GraphicsUnit.Pixel, attr);
        }

        using var msOut = new MemoryStream();
        inverted.Save(msOut, System.Drawing.Imaging.ImageFormat.Png);
        return msOut.ToArray();
    }
}
