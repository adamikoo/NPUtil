using System.Security.Cryptography;
using System.Text;
using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.Core;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.Modules.FileOrganizer.Classification;

public class HybridClassifier
{
    private readonly IEmbeddingEngine _embeddingEngine;
    private readonly EmbeddingCacheDb _cacheDb;
    private readonly RuleClassifier _ruleClassifier;
    private readonly UserCorrectionMemory _userMemory;
    private readonly TaxonomyManager _taxonomyManager;
    private readonly ILogger _logger;

    public HybridClassifier(
        IEmbeddingEngine embeddingEngine,
        EmbeddingCacheDb cacheDb,
        RuleClassifier ruleClassifier,
        UserCorrectionMemory userMemory,
        TaxonomyManager taxonomyManager,
        ILogger logger)
    {
        _embeddingEngine = embeddingEngine;
        _cacheDb = cacheDb;
        _ruleClassifier = ruleClassifier;
        _userMemory = userMemory;
        _taxonomyManager = taxonomyManager;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(ExtractedMetadata metadata, CancellationToken ct = default)
    {
        var categories = _taxonomyManager.GetCategories().Where(c => !c.IsExcluded).ToList();
        if (categories.Count == 0)
        {
            return new ClassificationResult("To Review", 0.0, 0.0, "No active categories defined.", metadata.Keywords, Array.Empty<DestinationSuggestion>(), "Default");
        }

        // 1. Check User Learned Corrections Memory
        var patternKeys = new List<string> { metadata.Extension, metadata.FileName };
        patternKeys.AddRange(metadata.Keywords.Take(3));
        var userPreference = _userMemory.GetPreferredCategory(patternKeys);
        if (!string.IsNullOrEmpty(userPreference))
        {
            var matchedCat = categories.FirstOrDefault(c => c.RelativePath.Equals(userPreference, StringComparison.OrdinalIgnoreCase));
            if (matchedCat != null)
            {
                return new ClassificationResult(
                    TargetRelativePath: matchedCat.RelativePath,
                    Confidence: 0.96,
                    RawSimilarityScore: 0.96,
                    Explanation: $"Prioritized according to previously approved classification for this file type / pattern.",
                    ExtractedKeywords: metadata.Keywords,
                    AlternativeDestinations: Array.Empty<DestinationSuggestion>(),
                    ClassifierSource: "UserPreference"
                );
            }
        }

        // 2. Check Deterministic High-Confidence Rules
        var ruleResult = _ruleClassifier.TryClassify(metadata, categories);
        if (ruleResult != null && ruleResult.Confidence >= 0.85)
        {
            return ruleResult;
        }

        // 3. Multilingual Semantic Embedding Inference
        var contentForEmbedding = PrepareDocumentTextForEmbedding(metadata);
        var contentHash = ComputeSha256(contentForEmbedding);

        var docEmbedding = _cacheDb.GetFileEmbedding(contentHash);
        if (docEmbedding == null)
        {
            docEmbedding = await _embeddingEngine.GenerateEmbeddingAsync(contentForEmbedding, ct);
            _cacheDb.SaveFileEmbedding(contentHash, docEmbedding);
        }

        // Compute similarities against all category taxonomy embeddings
        var scoredList = new List<(CategoryDefinition Category, float RawSimilarity, double CalibratedConfidence, List<string> MatchedKeywords)>();

        foreach (var cat in categories)
        {
            var catText = $"{cat.Name}. {cat.Description}. {string.Join(" ", cat.Keywords)}";
            var catHash = ComputeSha256(catText);

            var catEmbedding = _cacheDb.GetCategoryEmbedding(cat.RelativePath, catHash);
            if (catEmbedding == null)
            {
                catEmbedding = await _embeddingEngine.GenerateEmbeddingAsync(catText, ct);
                _cacheDb.SaveCategoryEmbedding(cat.RelativePath, catHash, catEmbedding);
            }

            var sim = EmbeddingEngine.CosineSimilarity(docEmbedding, catEmbedding);

            // Keyword bonus
            var matchedKw = new List<string>();
            var keywordBonus = 0.0;
            var lowerText = (metadata.FileName + " " + metadata.ExtractedText).ToLowerInvariant();

            foreach (var kw in cat.Keywords)
            {
                if (lowerText.Contains(kw.ToLowerInvariant()))
                {
                    matchedKw.Add(kw);
                    keywordBonus += 0.05;
                }
            }

            // Extension match bonus
            if (cat.FileExtensions.Contains(metadata.Extension, StringComparer.OrdinalIgnoreCase))
            {
                keywordBonus += 0.08;
            }

            var calibrated = CalibrateScore(sim, keywordBonus);
            scoredList.Add((cat, sim, calibrated, matchedKw));
        }

        var ordered = scoredList.OrderByDescending(s => s.CalibratedConfidence).ToList();
        var top = ordered.First();

        var alternatives = ordered.Skip(1).Take(2).Select(s => new DestinationSuggestion(
            RelativePath: s.Category.RelativePath,
            Confidence: Math.Round(s.CalibratedConfidence, 2),
            RawSimilarity: Math.Round(s.RawSimilarity, 2),
            Reason: s.MatchedKeywords.Count > 0
                ? $"Matched keywords: {string.Join(", ", s.MatchedKeywords)}"
                : $"Semantic similarity ({s.RawSimilarity:F2})",
            MatchedKeywords: s.MatchedKeywords
        )).ToList();

        var explanation = BuildExplanation(top.Category, top.RawSimilarity, top.CalibratedConfidence, top.MatchedKeywords);

        return new ClassificationResult(
            TargetRelativePath: top.Category.RelativePath,
            Confidence: Math.Round(top.CalibratedConfidence, 2),
            RawSimilarityScore: Math.Round(top.RawSimilarity, 2),
            Explanation: explanation,
            ExtractedKeywords: metadata.Keywords,
            AlternativeDestinations: alternatives,
            ClassifierSource: _embeddingEngine.IsNpuAccelerated ? "SemanticEmbedding (NPU)" : "SemanticEmbedding (CPU)"
        );
    }

    private static string PrepareDocumentTextForEmbedding(ExtractedMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.Append("Filename: ").AppendLine(metadata.FileName);
        sb.Append("Extension: ").AppendLine(metadata.Extension);

        if (metadata.Keywords.Count > 0)
        {
            sb.Append("Keywords: ").AppendLine(string.Join(", ", metadata.Keywords));
        }

        if (!string.IsNullOrWhiteSpace(metadata.ExtractedText))
        {
            var snippet = metadata.ExtractedText.Length > 2000
                ? metadata.ExtractedText[..2000]
                : metadata.ExtractedText;
            sb.Append("Content: ").AppendLine(snippet);
        }

        return sb.ToString();
    }

    private static double CalibrateScore(float rawSim, double bonus)
    {
        // Cosine similarity in MiniLM/E5 typically lies between 0.20 and 0.85
        // We calibrate to standard probability range [0.0 - 1.0]
        double score;
        if (rawSim < 0.25f)
        {
            score = rawSim * 0.8;
        }
        else if (rawSim < 0.50f)
        {
            score = 0.20 + (rawSim - 0.25f) * 1.6;
        }
        else
        {
            score = 0.60 + (rawSim - 0.50f) * 1.0;
        }

        score += bonus;
        return Math.Clamp(score, 0.05, 0.99);
    }

    private static string BuildExplanation(CategoryDefinition cat, float rawSim, double confidence, List<string> matchedKw)
    {
        var sb = new StringBuilder();
        if (matchedKw.Count > 0)
        {
            sb.Append($"Matched relevant keywords: [{string.Join(", ", matchedKw)}]. ");
        }
        sb.Append($"High semantic relevance to category '{cat.Name}' (Cosine Similarity: {rawSim:F2}, Confidence: {confidence:P0}).");
        return sb.ToString();
    }

    private static string ComputeSha256(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
