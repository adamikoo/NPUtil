namespace NPUtil.Core.Extraction;

public record ExtractedTable
{
    public string Name { get; init; } = "Table";
    public int PageOrSheetNumber { get; init; } = 1;
    public IReadOnlyList<string> Headers { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
}

public record DocumentSection
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string SectionType { get; init; } = "Paragraph"; // Heading, Paragraph, List, TableRef
    public int PageNumber { get; init; } = 1;
}

public record ExtractedDocument
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string Format { get; init; }
    public string FullText { get; init; } = string.Empty;
    public IReadOnlyList<DocumentSection> Sections { get; init; } = [];
    public IReadOnlyList<ExtractedTable> Tables { get; init; } = [];
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public bool HasOcrContent { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IDocumentExtractor
{
    bool CanHandle(string filePath);
    Task<ExtractedDocument> ExtractAsync(string filePath, CancellationToken ct = default);
}
