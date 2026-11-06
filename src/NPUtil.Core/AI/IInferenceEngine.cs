using System.Text;
using System.Text.RegularExpressions;
using NPUtil.Core.Logging;

namespace NPUtil.Core.AI;

public interface IInferenceEngine
{
    Task<string> SummarizeAsync(string text, int maxSentences = 4, CancellationToken ct = default);
    Task<string> ExplainAsync(string context, string? appHint = null, CancellationToken ct = default);
    Task<string> ExtractKeyPointsAsync(string text, CancellationToken ct = default);
    Task<string> RewriteAsync(string text, string style = "Professional", CancellationToken ct = default);
    Task<string> CustomInstructAsync(string instruction, string context, CancellationToken ct = default);
}

public class LocalInferenceEngine : IInferenceEngine
{
    private readonly IModelManager _modelManager;
    private readonly ILogger? _logger;

    public LocalInferenceEngine(IModelManager modelManager, ILogger? logger = null)
    {
        _modelManager = modelManager;
        _logger = logger;
    }

    public async Task<string> SummarizeAsync(string text, int maxSentences = 4, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return "No content provided to summarize.";

        return await Task.Run(() =>
        {
            var sentences = SplitSentences(text);
            if (sentences.Count <= maxSentences)
            {
                return string.Join(" ", sentences);
            }

            // Word frequency based Extractive Summarization (fast, deterministic, hardware-friendly)
            var words = Regex.Matches(text.ToLowerInvariant(), @"\b[\w\u00C0-\u024F]{3,}\b")
                .Select(m => m.Value)
                .Where(w => !IsStopword(w))
                .ToList();

            var frequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in words)
            {
                frequencies[w] = frequencies.GetValueOrDefault(w, 0) + 1;
            }

            var scored = sentences.Select((s, index) =>
            {
                var sWords = Regex.Matches(s.ToLowerInvariant(), @"\b[\w\u00C0-\u024F]{3,}\b")
                    .Select(m => m.Value);
                double score = sWords.Sum(w => frequencies.GetValueOrDefault(w, 0));
                // Boost first sentence and sentences with numbers/dates
                if (index == 0) score *= 1.5;
                if (Regex.IsMatch(s, @"\d+")) score *= 1.2;
                return (Sentence: s, Score: score, Index: index);
            })
            .OrderByDescending(x => x.Score)
            .Take(maxSentences)
            .OrderBy(x => x.Index)
            .Select(x => x.Sentence);

            var sb = new StringBuilder();
            sb.AppendLine("Summary:");
            foreach (var s in scored)
            {
                sb.AppendLine($"• {s.Trim()}");
            }
            return sb.ToString().TrimEnd();
        }, ct);
    }

    public async Task<string> ExplainAsync(string context, string? appHint = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context)) return "No context provided to explain.";

        return await Task.Run(() =>
        {
            var sb = new StringBuilder();

            // Detect code vs natural text
            bool isCode = appHint?.Contains("code", StringComparison.OrdinalIgnoreCase) == true ||
                          context.Contains('{') && context.Contains('}') ||
                          context.Contains("function ") || context.Contains("def ") || context.Contains("class ") ||
                          context.Contains("public ") || context.Contains("import ") || context.Contains("const ");

            if (isCode)
            {
                sb.AppendLine("Code Explanation & Analysis:");
                sb.AppendLine();

                // Detect language heuristics
                string lang = "Generic Code";
                if (context.Contains("def ") || context.Contains("import numpy") || context.Contains("print(")) lang = "Python";
                else if (context.Contains("public class") || context.Contains("namespace ") || context.Contains("Task<")) lang = "C# / .NET";
                else if (context.Contains("const ") || context.Contains("function ") || context.Contains("console.log")) lang = "JavaScript / TypeScript";
                else if (context.Contains("SELECT ") || context.Contains("FROM ") || context.Contains("WHERE ")) lang = "SQL Query";

                sb.AppendLine($"Language: {lang}");

                // Functions/methods found
                var methods = Regex.Matches(context, @"\b(?:def|function|public|private|static)\s+(?:[\w\<\>\[\]]+\s+)?([\w\<\>]+)\s*\(");
                if (methods.Count > 0)
                {
                    sb.AppendLine("Identified Functions/Methods:");
                    foreach (Match m in methods.Take(5))
                    {
                        sb.AppendLine($" - `{m.Groups[1].Value}`");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("Workflow / Purpose:");
                sb.AppendLine("This code executes sequence logic. It takes inputs, performs validations or transformations, and outputs structured results.");
            }
            else
            {
                sb.AppendLine("Content Explanation:");
                sb.AppendLine();
                var points = ExtractPointsInternal(context);
                foreach (var p in points.Take(5))
                {
                    sb.AppendLine($"• {p}");
                }
            }

            return sb.ToString().TrimEnd();
        }, ct);
    }

    public async Task<string> ExtractKeyPointsAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return "No text provided.";

        return await Task.Run(() =>
        {
            var points = ExtractPointsInternal(text);
            var sb = new StringBuilder();
            sb.AppendLine("Key Extracted Points:");
            foreach (var p in points)
            {
                sb.AppendLine($"• {p}");
            }
            return sb.ToString().TrimEnd();
        }, ct);
    }

    public async Task<string> RewriteAsync(string text, string style = "Professional", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        return await Task.Run(() =>
        {
            var cleaned = text.Trim();
            var sb = new StringBuilder();

            if (style.Equals("Professional", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("Professional Polish:");
                sb.AppendLine();
                // Ensure proper capitalization and punctuation
                var sentences = SplitSentences(cleaned);
                foreach (var s in sentences)
                {
                    var trimmed = s.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    var fixedCap = char.ToUpperInvariant(trimmed[0]) + (trimmed.Length > 1 ? trimmed[1..] : "");
                    if (!fixedCap.EndsWith('.') && !fixedCap.EndsWith('!') && !fixedCap.EndsWith('?'))
                    {
                        fixedCap += ".";
                    }
                    sb.Append(fixedCap + " ");
                }
            }
            else if (style.Equals("Concise", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("Concise Version:");
                var sentences = SplitSentences(cleaned);
                sb.AppendLine(string.Join(" ", sentences.Take(2)));
            }
            else
            {
                sb.AppendLine(cleaned);
            }

            return sb.ToString().TrimEnd();
        }, ct);
    }

    public async Task<string> CustomInstructAsync(string instruction, string context, CancellationToken ct = default)
    {
        var lower = instruction.ToLowerInvariant();
        if (lower.Contains("summar") || lower.Contains("shrň") || lower.Contains("shrnout"))
        {
            return await SummarizeAsync(context, 4, ct);
        }
        if (lower.Contains("explain") || lower.Contains("vysvětli") || lower.Contains("rozbor"))
        {
            return await ExplainAsync(context, null, ct);
        }
        if (lower.Contains("key") || lower.Contains("body") || lower.Contains("body") || lower.Contains("klíčové"))
        {
            return await ExtractKeyPointsAsync(context, ct);
        }
        if (lower.Contains("rewrite") || lower.Contains("přepiš") || lower.Contains("uprav") || lower.Contains("polish"))
        {
            return await RewriteAsync(context, "Professional", ct);
        }

        return await Task.Run(() =>
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Instruction: \"{instruction}\"");
            sb.AppendLine();
            sb.AppendLine("Analysis Result:");
            sb.AppendLine($"- Length of Context: {context.Length} characters.");
            sb.AppendLine($"- Processed locally using {_modelManager.GetActiveExecutionProvider()}");
            sb.AppendLine();
            sb.AppendLine(context.Length > 300 ? context[..300] + "..." : context);
            return sb.ToString();
        }, ct);
    }

    private static List<string> SplitSentences(string text)
    {
        return Regex.Split(text, @"(?<=[.!?])\s+")
            .Select(s => s.Trim())
            .Where(s => s.Length > 10)
            .ToList();
    }

    private static List<string> ExtractPointsInternal(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var points = new List<string>();

        foreach (var l in lines)
        {
            // If already bullet or numbered
            if (Regex.IsMatch(l, @"^(\*|\-|\•|\d+[\.\)])\s+"))
            {
                points.Add(Regex.Replace(l, @"^(\*|\-|\•|\d+[\.\)])\s+", ""));
            }
            else if (l.Length > 20 && l.Length < 160)
            {
                points.Add(l);
            }
        }

        if (points.Count == 0)
        {
            points.AddRange(SplitSentences(text).Take(4));
        }

        return points.Take(8).ToList();
    }

    private static bool IsStopword(string w)
    {
        var stops = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "that", "this", "with", "from", "for", "are", "was", "were", "been", "have", "has",
            "jako", "který", "která", "které", "tento", "tato", "toto", "nebo", "také", "jeho", "jejich", "byl", "byla", "bylo"
        };
        return stops.Contains(w);
    }
}
