namespace NPUtil.Modules.SmartDataExtractor.Models;

public enum ExtractionCategory
{
    TablesAndData,
    FormattedText,
    InvoicesAndReceipts,
    ListsAndRecords,
    DatesAndDeadlines,
    StructuredJson,
    Custom
}

public record InvoiceItem
{
    public string Description { get; init; } = string.Empty;
    public string Quantity { get; init; } = "1";
    public string UnitPrice { get; init; } = string.Empty;
    public string TotalPrice { get; init; } = string.Empty;
}

public record InvoiceData
{
    public string Vendor { get; init; } = string.Empty;
    public string Customer { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public string IssueDate { get; init; } = string.Empty;
    public string DueDate { get; init; } = string.Empty;
    public string TotalAmount { get; init; } = string.Empty;
    public string TaxAmount { get; init; } = string.Empty;
    public string Currency { get; init; } = "CZK";
    public IReadOnlyList<InvoiceItem> Items { get; init; } = [];
}

public record ExtractedDateRecord
{
    public string DateText { get; init; } = string.Empty;
    public string Context { get; init; } = string.Empty;
    public string RecordType { get; init; } = "Date"; // Deadline, Appointment, Issued, Due
    public int PageNumber { get; init; } = 1;
}

public record SmartExtractionResult
{
    public required string SourceFilePath { get; init; }
    public required string FileName { get; init; }
    public required string FileFormat { get; init; }
    public ExtractionCategory Category { get; init; }
    public string Title { get; init; } = string.Empty;
    public string FormattedText { get; init; } = string.Empty;
    public string JsonOutput { get; init; } = string.Empty;
    public IReadOnlyList<string> TableHeaders { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> TableRows { get; init; } = [];
    public IReadOnlyList<ExtractedDateRecord> DateRecords { get; init; } = [];
    public InvoiceData? Invoice { get; init; }
    public IReadOnlyList<string> SourceReferences { get; init; } = [];
    public IReadOnlyList<string> Uncertainties { get; init; } = [];
    public double Confidence { get; init; } = 1.0;
    public TimeSpan ElapsedTime { get; init; }
    public bool Success { get; init; } = true;
    public string? ErrorMessage { get; init; }
}
