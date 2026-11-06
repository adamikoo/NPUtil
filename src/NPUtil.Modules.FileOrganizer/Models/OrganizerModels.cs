namespace NPUtil.Modules.FileOrganizer.Models;

public enum OrganizerMode
{
    ReviewEverything,   // Default: Never move without manual approval
    IntelligentAuto,    // Automatically move high-confidence files; queue ambiguous ones
    FullyAutomatic      // Automatically move based on established categories & preferences
}

public enum ItemStatus
{
    PendingReview,
    Approved,
    Rejected,
    Organized,
    Undone,
    Error
}

public record ExtractedMetadata(
    string FilePath,
    string FileName,
    string Extension,
    long FileSizeBytes,
    DateTime CreatedTime,
    DateTime LastModifiedTime,
    string MimeType,
    string ExtractedText,
    IReadOnlyList<string> Keywords,
    IReadOnlyDictionary<string, string> AdditionalProperties
);

public record DestinationSuggestion(
    string RelativePath,
    double Confidence,
    double RawSimilarity,
    string Reason,
    IReadOnlyList<string> MatchedKeywords
);

public record ClassificationResult(
    string TargetRelativePath,
    double Confidence,
    double RawSimilarityScore,
    string Explanation,
    IReadOnlyList<string> ExtractedKeywords,
    IReadOnlyList<DestinationSuggestion> AlternativeDestinations,
    string ClassifierSource // "Rule", "SemanticEmbedding", "UserPreference", "Hybrid"
);

public record OrganizeOperationRecord(
    long Id,
    string BatchId,
    string OriginalPath,
    string DestinationPath,
    string FileName,
    long FileSizeBytes,
    string? FileHash,
    string Category,
    double Confidence,
    string Explanation,
    DateTime Timestamp,
    bool IsUndone,
    DateTime? UndoTimestamp = null
);
