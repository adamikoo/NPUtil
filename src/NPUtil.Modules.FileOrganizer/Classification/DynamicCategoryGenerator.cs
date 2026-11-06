using System.Text.RegularExpressions;
using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.Core;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.Modules.FileOrganizer.Classification;

public class DynamicCategoryGenerator
{
    private static readonly Regex CleanNameRegex = new(@"[_\-\.\s\d\(\)]+", RegexOptions.Compiled);
    private readonly TaxonomyManager _taxonomyManager;
    private readonly ILogger _logger;

    public DynamicCategoryGenerator(TaxonomyManager taxonomyManager, ILogger logger)
    {
        _taxonomyManager = taxonomyManager;
        _logger = logger;
    }

    public CategoryDefinition GenerateCategoryFor(ExtractedMetadata metadata)
    {
        var rawName = Path.GetFileNameWithoutExtension(metadata.FileName);
        var text = (rawName + " " + metadata.ExtractedText).ToLowerInvariant();

        // 1. Detect Domain & Topic
        string parent;
        string subcategory;
        string description;

        if (text.Contains("zpravodaj") || text.Contains("vestnik") || text.Contains("věstník") ||
            text.Contains("bulletin") || text.Contains("newsletter") || text.Contains("oběžník"))
        {
            parent = "Work";
            subcategory = "Newsletters";
            description = "Periodika, zpravodaje, věstníky, firemní a obecní bulletiny, newsletters";
        }
        else if (text.Contains("smlouva") || text.Contains("contract") || text.Contains("dohoda") || text.Contains("plná moc"))
        {
            parent = "Work";
            subcategory = "Contracts";
            description = "Právní smlouvy, dohody, dodatky, plné moci a ujednání";
        }
        else if (text.Contains("zápis") || text.Contains("minutes") || text.Contains("jednání") || text.Contains("meeting"))
        {
            parent = "Work";
            subcategory = "Meeting Minutes";
            description = "Zápisy z porad, meeting notes, protokoly z jednání";
        }
        else if (text.Contains("letenka") || text.Contains("jízdenka") || text.Contains("boarding pass") ||
                 text.Contains("rezervace") || text.Contains("ticket") || text.Contains("booking"))
        {
            parent = "Personal";
            subcategory = "Travel & Tickets";
            description = "Cestovní doklady, letenky, jízdenky, rezervace ubytování";
        }
        else if (text.Contains("lékař") || text.Contains("zpráva z vyšetření") || text.Contains("recept") || text.Contains("pacient"))
        {
            parent = "Personal";
            subcategory = "Health & Medical";
            description = "Zdravotní dokumentace, lékařské zprávy, laboratorní výsledky";
        }
        else
        {
            // Derive topic from top significant keyword or cleaned filename
            var candidateTopic = string.Empty;
            if (metadata.Keywords.Count > 0)
            {
                candidateTopic = metadata.Keywords.FirstOrDefault(k => k.Length >= 4 && !k.All(char.IsDigit)) ?? string.Empty;
            }

            if (string.IsNullOrEmpty(candidateTopic))
            {
                var cleanTokens = CleanNameRegex.Split(rawName)
                    .Where(t => t.Length >= 3 && !t.All(char.IsDigit))
                    .ToList();
                candidateTopic = cleanTokens.Count > 0 ? cleanTokens[0] : "General";
            }

            candidateTopic = char.ToUpperInvariant(candidateTopic[0]) + candidateTopic[1..].ToLowerInvariant();

            // Infer parent by file type
            if (metadata.Extension is ".pdf" or ".docx" or ".doc" or ".odt" or ".rtf" or ".txt")
            {
                parent = "Documents";
            }
            else if (metadata.Extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".svg")
            {
                parent = "Media\\Images";
            }
            else if (metadata.Extension is ".zip" or ".7z" or ".rar" or ".tar" or ".gz")
            {
                parent = "Archives";
            }
            else
            {
                parent = "Other";
            }

            subcategory = candidateTopic;
            description = $"Dynamicky vytvořená kategorie pro soubory s tématem '{candidateTopic}' ({metadata.Extension})";
        }

        var relativePath = $@"{parent}\{subcategory}";

        // Check if category already exists
        var existing = _taxonomyManager.FindByRelativePath(relativePath);
        if (existing != null)
        {
            return existing;
        }

        var newCat = new CategoryDefinition(
            relativePath: relativePath,
            name: subcategory,
            description: description,
            keywords: metadata.Keywords.Take(6).ToArray(),
            extensions: [metadata.Extension],
            priority: 8
        );

        _taxonomyManager.AddOrUpdateCategory(newCat);
        _logger.Info("DynamicCategoryGenerator", $"Dynamically created new category: '{relativePath}' ({newCat.Name})");

        return newCat;
    }
}
