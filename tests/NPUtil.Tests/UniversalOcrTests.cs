using NPUtil.Core.OCR;
using NPUtil.Core.Translation;
using NPUtil.Modules.UniversalOcr.Core;

namespace NPUtil.Tests;

public class UniversalOcrTests
{
    private readonly LocalTranslationService _translationService = new();
    private readonly WindowsOcrService _ocrService = new();
    private readonly UniversalOcrEngine _engine;

    public UniversalOcrTests()
    {
        _engine = new UniversalOcrEngine(_ocrService, _translationService);
    }

    [Fact]
    public void TranslationService_DetectsCzechLanguageCorrectly()
    {
        var text = "Toto je faktura za provedené práce a konzultace v oblasti informačních technologií.";
        var lang = _translationService.DetectLanguage(text);
        Assert.Equal("cs", lang);
    }

    [Fact]
    public void TranslationService_DetectsEnglishLanguageCorrectly()
    {
        var text = "This is an invoice for software development services and technical consulting.";
        var lang = _translationService.DetectLanguage(text);
        Assert.Equal("en", lang);
    }

    [Fact]
    public async Task TranslationService_TranslatesCzechToEnglishAccurately()
    {
        var cs = "faktura\ndatum splatnosti\ncelkem k úhradě\ndodavatel";
        var en = await _translationService.TranslateAsync(cs, "cs", "en");

        Assert.Contains("invoice", en.ToLowerInvariant());
        Assert.Contains("due date", en.ToLowerInvariant());
        Assert.Contains("total amount due", en.ToLowerInvariant());
        Assert.Contains("supplier", en.ToLowerInvariant());
    }

    [Fact]
    public async Task TranslationService_TranslatesEnglishToCzechAccurately()
    {
        var en = "invoice\ntotal\ndue date\nvendor";
        var cs = await _translationService.TranslateAsync(en, "en", "cs");

        Assert.Contains("faktura", cs.ToLowerInvariant());
        Assert.Contains("celkem", cs.ToLowerInvariant());
        Assert.Contains("splatnosti", cs.ToLowerInvariant());
    }

    [Fact]
    public void OcrService_ReportsAvailableRecognizerLanguages()
    {
        var langs = _ocrService.GetAvailableLanguages();
        Assert.NotEmpty(langs);
    }

    [Fact]
    public async Task OcrService_RecognizesTextOnDarkBackground()
    {
        // Render crisp white text on a dark background (simulating Reddit / VS Code dark mode)
        using var bmp = new System.Drawing.Bitmap(400, 120);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.FromArgb(24, 24, 24));
            using var font = new System.Drawing.Font("Arial", 28, System.Drawing.FontStyle.Bold);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            g.DrawString("Snapdragon Elite", font, brush, new System.Drawing.PointF(20, 35));
        }

        using var ms = new System.IO.MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        var bytes = ms.ToArray();

        var result = await _ocrService.RecognizeBytesAsync(bytes, "auto");
        Assert.True(result.Success);
        Assert.Contains("Snapdragon", result.FullText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OcrService_RecognizesSmallUiTextOnDarkBackground()
    {
        using var bmp = new System.Drawing.Bitmap(250, 180);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.FromArgb(14, 17, 19));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Regular);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(215, 218, 220));
            using var iconBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(128, 128, 128));

            string[] items = ["Home", "Popular", "News", "Explore"];
            for (int i = 0; i < items.Length; i++)
            {
                int y = 15 + i * 35;
                g.FillEllipse(iconBrush, 12, y + 2, 12, 12);
                g.DrawString(items[i], font, brush, 32, y);
            }
        }

        using var ms = new System.IO.MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        var bytes = ms.ToArray();

        var result = await _ocrService.RecognizeBytesAsync(bytes, "auto");
        Assert.True(result.Success);
        Assert.Contains("Home", result.FullText);
        Assert.Contains("Popular", result.FullText);
    }
}
