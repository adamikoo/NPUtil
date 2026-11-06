using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NPUtil.Core.AI;
using NPUtil.Core.Logging;
using NPUtil.Core.OCR;
using NPUtil.Core.Translation;

namespace NPUtil.Modules.UniversalOcr.Core;

public record OcrTranslationResult
{
    public required string OriginalText { get; init; }
    public required string TranslatedText { get; init; }
    public string DetectedSourceLang { get; init; } = "cs";
    public string TargetLang { get; init; } = "en";
    public IReadOnlyList<OcrLineInfo> Lines { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> ExtractedTable { get; init; } = [];
    public TimeSpan ElapsedTime { get; init; }
    public bool Success { get; init; } = true;
    public string? ErrorMessage { get; init; }
}

public class UniversalOcrEngine
{
    private readonly IOcrService _ocrService;
    private readonly ITranslationService _translationService;
    private readonly IInferenceEngine? _inferenceEngine;
    private readonly ILogger? _logger;

    public IOcrService OcrService => _ocrService;
    public ITranslationService TranslationService => _translationService;
    public IInferenceEngine? InferenceEngine => _inferenceEngine;

    public UniversalOcrEngine(
        IOcrService ocrService,
        ITranslationService translationService,
        IInferenceEngine? inferenceEngine = null,
        ILogger? logger = null)
    {
        _ocrService = ocrService;
        _translationService = translationService;
        _inferenceEngine = inferenceEngine;
        _logger = logger;
    }

    public async Task<OcrTranslationResult> ProcessImageBytesAsync(
        byte[] imageBytes,
        string targetLang = "en",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // 1. OCR Recognition (use auto language detection / user profile OCR recognizer)
            var ocr = await _ocrService.RecognizeBytesAsync(imageBytes, "auto", ct);
            if (!ocr.Success)
            {
                return new OcrTranslationResult
                {
                    OriginalText = string.Empty,
                    TranslatedText = string.Empty,
                    Success = false,
                    ErrorMessage = ocr.ErrorMessage,
                    ElapsedTime = sw.Elapsed
                };
            }

            var text = ocr.FullText;
            var sourceLang = _translationService.DetectLanguage(text);

            // Determine target language if set to auto
            if (targetLang.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                targetLang = sourceLang == "cs" ? "en" : "cs";
            }

            // 2. Translation
            var translated = await _translationService.TranslateAsync(text, sourceLang, targetLang, ct);

            // 3. Table / List heuristic
            var table = ExtractTableFromLines(ocr.Lines);

            sw.Stop();
            return new OcrTranslationResult
            {
                OriginalText = text,
                TranslatedText = translated,
                DetectedSourceLang = sourceLang,
                TargetLang = targetLang,
                Lines = ocr.Lines,
                ExtractedTable = table,
                ElapsedTime = sw.Elapsed,
                Success = true
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.Error("UniversalOcr", $"OCR processing failed: {ex.Message}", ex);
            return new OcrTranslationResult
            {
                OriginalText = string.Empty,
                TranslatedText = string.Empty,
                Success = false,
                ErrorMessage = ex.Message,
                ElapsedTime = sw.Elapsed
            };
        }
    }

    public async Task<IReadOnlyList<OcrTranslationResult>> ProcessBatchAsync(
        IEnumerable<string> filePaths,
        string targetLang = "en",
        CancellationToken ct = default)
    {
        var list = new List<OcrTranslationResult>();
        foreach (var path in filePaths)
        {
            if (File.Exists(path))
            {
                var bytes = await File.ReadAllBytesAsync(path, ct);
                var res = await ProcessImageBytesAsync(bytes, targetLang, ct);
                list.Add(res);
            }
        }
        return list;
    }

    private static List<IReadOnlyList<string>> ExtractTableFromLines(IReadOnlyList<OcrLineInfo> lines)
    {
        var table = new List<IReadOnlyList<string>>();
        foreach (var line in lines)
        {
            var parts = Regex.Split(line.Text, @"\s{3,}|\t|\|")
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            if (parts.Count >= 2)
            {
                table.Add(parts);
            }
        }
        return table;
    }
}
