namespace NPUtil.Core.OCR;

public record OcrLineInfo
{
    public required string Text { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}

public record OcrResultData
{
    public required string FullText { get; init; }
    public IReadOnlyList<OcrLineInfo> Lines { get; init; } = [];
    public int WordCount { get; init; }
    public string Language { get; init; } = "cs";
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IOcrService
{
    bool IsLanguageSupported(string languageTag);
    IReadOnlyList<string> GetAvailableLanguages();
    Task<OcrResultData> RecognizeAsync(string imagePath, string languageTag = "auto", CancellationToken ct = default);
    Task<OcrResultData> RecognizeBytesAsync(byte[] imageBytes, string languageTag = "auto", CancellationToken ct = default);
}
