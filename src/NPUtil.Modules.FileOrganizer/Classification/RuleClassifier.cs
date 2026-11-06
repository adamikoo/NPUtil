using System.Text.RegularExpressions;
using NPUtil.Modules.FileOrganizer.Core;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.Modules.FileOrganizer.Classification;

public class RuleClassifier
{
    private static readonly Regex YearRegex = new(@"\b(202[0-9])\b", RegexOptions.Compiled);
    private static readonly Regex InvoiceRegex = new(@"(faktura|faktury|daňový\s+doklad|vyúčtování|invoice|bill|billing|doklad\s+č|zálohová\s+faktura)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Mt5Regex = new(@"(mt5|metatrader|expert\s+advisor|mql5|ea\s+strategy|backtest)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CambridgeRegex = new(@"(cambridge|c1\s+advanced|cae|ielts|toefl|english\s+grammar|oxford)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CarRegex = new(@"(ferrari|porsche|bmw|audi|mercedes|lamborghini|automobile|supercar|auto\b|vozidlo)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WallpaperRegex = new(@"(wallpaper|tapeta|4k|uhd|3840x2160|2560x1440|background)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ClassificationResult? TryClassify(ExtractedMetadata metadata, IReadOnlyList<CategoryDefinition> categories)
    {
        var text = (metadata.FileName + " " + metadata.ExtractedText).ToLowerInvariant();
        var ext = metadata.Extension.ToLowerInvariant();
        var currentYear = DateTime.Now.Year.ToString();

        // 1. Trading & MT5
        if (ext is ".mq5" or ".mq4" or ".ex5" || Mt5Regex.IsMatch(text))
        {
            var cat = categories.FirstOrDefault(c => c.RelativePath.Contains("Trading", StringComparison.OrdinalIgnoreCase));
            if (cat != null)
            {
                return new ClassificationResult(
                    TargetRelativePath: cat.RelativePath,
                    Confidence: 0.95,
                    RawSimilarityScore: 0.95,
                    Explanation: "Matched algorithmic trading / MT5 strategy pattern in file or contents.",
                    ExtractedKeywords: ["mt5", "trading", "strategy"],
                    AlternativeDestinations: Array.Empty<DestinationSuggestion>(),
                    ClassifierSource: "Rule"
                );
            }
        }

        // 2. Invoices & Billing
        if (InvoiceRegex.IsMatch(text) && (ext is ".pdf" or ".xml" or ".docx" or ".xlsx"))
        {
            var yearMatch = YearRegex.Match(text);
            var year = yearMatch.Success ? yearMatch.Value : currentYear;
            var targetPath = $@"Finance\Invoices\{year}";

            var cat = categories.FirstOrDefault(c => c.RelativePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                   ?? categories.FirstOrDefault(c => c.RelativePath.Equals(@"Finance\Invoices", StringComparison.OrdinalIgnoreCase));

            if (cat != null)
            {
                return new ClassificationResult(
                    TargetRelativePath: cat.RelativePath,
                    Confidence: 0.96,
                    RawSimilarityScore: 0.96,
                    Explanation: $"Identified financial invoice / daňový doklad with year context {year}.",
                    ExtractedKeywords: ["faktura", "invoice", "doklad", year],
                    AlternativeDestinations: Array.Empty<DestinationSuggestion>(),
                    ClassifierSource: "Rule"
                );
            }
        }

        // 3. Cambridge English / Language Materials
        if (CambridgeRegex.IsMatch(text) && (ext is ".pdf" or ".epub" or ".docx" or ".mobi"))
        {
            var cat = categories.FirstOrDefault(c => c.RelativePath.Contains(@"Languages\English", StringComparison.OrdinalIgnoreCase));
            if (cat != null)
            {
                return new ClassificationResult(
                    TargetRelativePath: cat.RelativePath,
                    Confidence: 0.94,
                    RawSimilarityScore: 0.94,
                    Explanation: "Identified Cambridge / C1 Advanced language learning textbook or study material.",
                    ExtractedKeywords: ["cambridge", "c1", "english", "learning"],
                    AlternativeDestinations: Array.Empty<DestinationSuggestion>(),
                    ClassifierSource: "Rule"
                );
            }
        }

        // 4. Car Photography
        if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" && CarRegex.IsMatch(text))
        {
            var cat = categories.FirstOrDefault(c => c.RelativePath.Contains(@"Images\Cars", StringComparison.OrdinalIgnoreCase));
            if (cat != null)
            {
                return new ClassificationResult(
                    TargetRelativePath: cat.RelativePath,
                    Confidence: 0.92,
                    RawSimilarityScore: 0.92,
                    Explanation: "Identified automobile / vehicle photograph.",
                    ExtractedKeywords: ["car", "vehicle", "photo"],
                    AlternativeDestinations: Array.Empty<DestinationSuggestion>(),
                    ClassifierSource: "Rule"
                );
            }
        }

        // 5. Wallpapers
        if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" && WallpaperRegex.IsMatch(text))
        {
            var cat = categories.FirstOrDefault(c => c.RelativePath.Contains(@"Images\Wallpapers", StringComparison.OrdinalIgnoreCase));
            if (cat != null)
            {
                return new ClassificationResult(
                    TargetRelativePath: cat.RelativePath,
                    Confidence: 0.91,
                    RawSimilarityScore: 0.91,
                    Explanation: "Identified high-resolution desktop wallpaper.",
                    ExtractedKeywords: ["wallpaper", "desktop", "image"],
                    AlternativeDestinations: Array.Empty<DestinationSuggestion>(),
                    ClassifierSource: "Rule"
                );
            }
        }

        // 6. Software Installers
        if (ext is ".exe" or ".msi" && (text.Contains("setup") || text.Contains("install")))
        {
            var cat = categories.FirstOrDefault(c => c.RelativePath.Contains(@"Software\Installers", StringComparison.OrdinalIgnoreCase));
            if (cat != null)
            {
                return new ClassificationResult(
                    TargetRelativePath: cat.RelativePath,
                    Confidence: 0.95,
                    RawSimilarityScore: 0.95,
                    Explanation: "Identified executable installer or setup package.",
                    ExtractedKeywords: ["setup", "installer", "software"],
                    AlternativeDestinations: Array.Empty<DestinationSuggestion>(),
                    ClassifierSource: "Rule"
                );
            }
        }

        // 7. Visual Studio Solutions / Projects
        if (ext is ".sln" or ".csproj")
        {
            var cat = categories.FirstOrDefault(c => c.RelativePath.Contains(@"Programming\Projects", StringComparison.OrdinalIgnoreCase));
            if (cat != null)
            {
                return new ClassificationResult(
                    TargetRelativePath: cat.RelativePath,
                    Confidence: 0.94,
                    RawSimilarityScore: 0.94,
                    Explanation: "Identified software solution / project descriptor.",
                    ExtractedKeywords: ["project", "solution", "code"],
                    AlternativeDestinations: Array.Empty<DestinationSuggestion>(),
                    ClassifierSource: "Rule"
                );
            }
        }

        return null;
    }
}
