using System.IO;
using NPUtil.Core.AI;
using NPUtil.Core.Context;
using NPUtil.Core.Extraction;
using NPUtil.Core.Logging;
using NPUtil.Core.Translation;

namespace NPUtil.Modules.ContextAssistant.Core;

public enum ContextActionType
{
    Explain,
    Summarize,
    Translate,
    ExtractPoints,
    Rewrite,
    Custom
}

public record AssistantExecutionResult
{
    public required string ActionTitle { get; init; }
    public required string OutputText { get; init; }
    public required string ContextUsed { get; init; }
    public string? ActiveApp { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public bool Success { get; init; } = true;
    public string? ErrorMessage { get; init; }
}

public class ContextAssistantEngine
{
    private readonly IContextCaptureService _captureService;
    private readonly IInferenceEngine _inferenceEngine;
    private readonly ITranslationService _translationService;
    private readonly IDocumentExtractor _documentExtractor;
    private readonly ContextAssistantHistoryDb _historyDb;
    private readonly ILogger? _logger;

    public ContextAssistantEngine(
        IContextCaptureService captureService,
        IInferenceEngine inferenceEngine,
        ITranslationService translationService,
        IDocumentExtractor documentExtractor,
        ContextAssistantHistoryDb historyDb,
        ILogger? logger = null)
    {
        _captureService = captureService;
        _inferenceEngine = inferenceEngine;
        _translationService = translationService;
        _documentExtractor = documentExtractor;
        _historyDb = historyDb;
        _logger = logger;
    }

    public ContextAssistantHistoryDb HistoryDb => _historyDb;
    public IContextCaptureService CaptureService => _captureService;

    public async Task<CapturedContext> CaptureContextAsync(CancellationToken ct = default)
    {
        return await _captureService.CaptureCurrentContextAsync(ct);
    }

    public async Task<string> ExtractFileContextAsync(string filePath, CancellationToken ct = default)
    {
        if (!_documentExtractor.CanHandle(filePath))
        {
            return $"[File {Path.GetFileName(filePath)}: unsupported format]";
        }

        var doc = await _documentExtractor.ExtractAsync(filePath, ct);
        if (!string.IsNullOrEmpty(doc.ErrorMessage))
        {
            return $"[Error reading {doc.FileName}: {doc.ErrorMessage}]";
        }

        return doc.FullText;
    }

    public async Task<AssistantExecutionResult> ExecuteActionAsync(
        ContextActionType action,
        string context,
        string? customInstruction = null,
        string? activeApp = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string output;
            string title;

            switch (action)
            {
                case ContextActionType.Explain:
                    title = "Explain Selection";
                    output = await _inferenceEngine.ExplainAsync(context, activeApp, ct);
                    break;

                case ContextActionType.Summarize:
                    title = "Summarize Content";
                    output = await _inferenceEngine.SummarizeAsync(context, 4, ct);
                    break;

                case ContextActionType.Translate:
                    title = "Translate Content";
                    var sourceLang = _translationService.DetectLanguage(context);
                    var targetLang = sourceLang == "cs" ? "en" : "cs";
                    output = await _translationService.TranslateAsync(context, sourceLang, targetLang, ct);
                    break;

                case ContextActionType.ExtractPoints:
                    title = "Extract Key Points";
                    output = await _inferenceEngine.ExtractKeyPointsAsync(context, ct);
                    break;

                case ContextActionType.Rewrite:
                    title = "Polish & Rewrite";
                    output = await _inferenceEngine.RewriteAsync(context, "Professional", ct);
                    break;

                case ContextActionType.Custom:
                default:
                    title = customInstruction ?? "Custom Query";
                    output = await _inferenceEngine.CustomInstructAsync(customInstruction ?? "Analyze context", context, ct);
                    break;
            }

            sw.Stop();

            // Store in local history
            _historyDb.AddEntry(activeApp ?? "Manual", title, context, output);

            return new AssistantExecutionResult
            {
                ActionTitle = title,
                OutputText = output,
                ContextUsed = context,
                ActiveApp = activeApp,
                ElapsedTime = sw.Elapsed,
                Success = true
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.Error("ContextAssistant", $"Action failed: {ex.Message}", ex);
            return new AssistantExecutionResult
            {
                ActionTitle = action.ToString(),
                OutputText = string.Empty,
                ContextUsed = context,
                ActiveApp = activeApp,
                ElapsedTime = sw.Elapsed,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
