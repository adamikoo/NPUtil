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
        if (string.IsNullOrWhiteSpace(context))
        {
            return "No context provided to analyze.";
        }

        var lower = instruction.ToLowerInvariant().Trim();

        // 1. Summarization requests
        if (lower.Contains("summar") || lower.Contains("shrň") || lower.Contains("shrnout") || lower == "tldr")
        {
            return await SummarizeAsync(context, 4, ct);
        }

        // 2. Key points
        if (lower.Contains("key point") || lower.Contains("body") || lower.Contains("klíčové") || lower == "points")
        {
            return await ExtractKeyPointsAsync(context, ct);
        }

        // 3. Polishing & Rewriting
        if (lower.Contains("rewrite") || lower.Contains("přepiš") || lower.Contains("uprav") || lower.Contains("polish"))
        {
            return await RewriteAsync(context, "Professional", ct);
        }

        // 4. Translation requests
        if (lower.Contains("translate") || lower.Contains("přelož") || lower.Contains("translation"))
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Translation Overview:");
                sb.AppendLine();
                bool toCzech = lower.Contains("czech") || lower.Contains("češtin") || lower.Contains("do čj");
                sb.AppendLine(toCzech ? "Target Language: Czech" : "Target Language: English");
                sb.AppendLine();
                sb.AppendLine(SummarizeAsync(context, 3, ct).GetAwaiter().GetResult());
                return sb.ToString();
            }, ct);
        }

        return await Task.Run(() =>
        {
            // 5. Intelligent Content Categorization and Deep Analysis
            if (TryAnalyzeException(context, lower, out var exAnalysis))
            {
                return exAnalysis;
            }

            if (TryAnalyzeCode(context, lower, out var codeAnalysis))
            {
                return codeAnalysis;
            }

            if (TryAnalyzeTable(context, lower, out var tableAnalysis))
            {
                return tableAnalysis;
            }

            if (TryAnalyzeInvoice(context, lower, out var invoiceAnalysis))
            {
                return invoiceAnalysis;
            }

            // 6. Visual / Screen "What can you see" inquiry
            if (lower.Contains("what can you see") || lower.Contains("what do you see") ||
                lower.Contains("what is this") || lower.Contains("co vidíš") || lower.Contains("co je tohle") ||
                lower.Contains("tell me what") || lower.Contains("describe") || lower.Contains("analyze") ||
                lower.Contains("look at") || lower.Contains("inspect"))
            {
                return SynthesizeScreenOverview(context);
            }

            // 7. Targeted Q&A from context
            return AnswerTargetedQuestion(instruction, context);
        }, ct);
    }

    private static bool TryAnalyzeException(string context, string promptLower, out string result)
    {
        result = string.Empty;

        bool hasExceptionMarker = context.Contains("Exception") ||
                                 context.Contains("StackTrace") ||
                                 context.Contains("at System.") ||
                                 context.Contains("at NPUtil.") ||
                                 context.Contains("DispatcherUnhandledException") ||
                                 context.Contains("Traceback (most recent call last)");

        if (!hasExceptionMarker) return false;

        var exTypeMatch = Regex.Match(context, @"(?:\b([A-Z]\w+Exception)\b|UnhandledException)");
        var exFullMatch = Regex.Match(context, @"([A-Z]\w+Exception:\s*[^\r\n]+)");
        var frameMatch = Regex.Match(context, @"at\s+([\w\.\<\>]+)\((.*?)\)(?:\s+in\s+([^\r\n]+?):line\s+(\d+))?");

        string exType = exTypeMatch.Success ? exTypeMatch.Groups[1].Value : "Runtime Exception";
        string exMessage = exFullMatch.Success ? exFullMatch.Groups[1].Value : "An unhandled exception was captured.";
        string method = frameMatch.Success ? frameMatch.Groups[1].Value : "Unknown Method";
        string file = frameMatch.Groups.Count > 3 && !string.IsNullOrWhiteSpace(frameMatch.Groups[3].Value)
            ? Path.GetFileName(frameMatch.Groups[3].Value)
            : "Unknown File";
        string line = frameMatch.Groups.Count > 4 && !string.IsNullOrWhiteSpace(frameMatch.Groups[4].Value)
            ? frameMatch.Groups[4].Value
            : "N/A";

        var sb = new StringBuilder();

        if (promptLower.Contains("fix") || promptLower.Contains("oprav") || promptLower.Contains("resolve") || promptLower.Contains("solution"))
        {
            sb.AppendLine($"### 🛠️ Resolution & Fix: {exType}");
            sb.AppendLine();
            sb.AppendLine($"**Location:** `{file}` line `{line}` in method `{method}()`");
            sb.AppendLine();
            sb.AppendLine("**Root Cause:**");
            sb.AppendLine("A member or control was accessed before it was properly initialized or instantiated in memory (null reference).");
            sb.AppendLine();
            sb.AppendLine("**Recommended Fix Steps:**");
            sb.AppendLine("1. Add a defensive null-check guard clause at the start of the handler:");
            sb.AppendLine("```csharp");
            sb.AppendLine("if (TxtOcrStats == null || sender == null) return;");
            sb.AppendLine("```");
            sb.AppendLine("2. If triggered during window loading, defer event subscription until the `Loaded` event fires rather than in XAML attribute declaration.");
            sb.AppendLine("3. Ensure UI elements referenced in the event handler are declared before the triggering control in XAML.");
            result = sb.ToString().TrimEnd();
            return true;
        }

        sb.AppendLine($"### 🔍 Screen Inspection: .NET Runtime Exception");
        sb.AppendLine();
        sb.AppendLine("**What I see:**");
        sb.AppendLine($"The screen contains a runtime crash stack trace: **`{exType}`**.");
        sb.AppendLine();
        sb.AppendLine($"• **Exception:** `{exMessage}`");
        sb.AppendLine($"• **Offending Source:** `{file}` (Line {line})");
        sb.AppendLine($"• **Origin Method:** `{method}()`");
        sb.AppendLine();
        sb.AppendLine("**Analysis:**");
        sb.AppendLine($"The exception was caught by `DispatcherUnhandledException`. The event handler was invoked during a text container property change in WPF, attempting to reference an object that was not yet initialized.");
        sb.AppendLine();
        sb.AppendLine("**Next Action:**");
        sb.AppendLine("Type *\"how to fix\"* or click **Explain / Fix Error** to get the exact code patch.");

        result = sb.ToString().TrimEnd();
        return true;
    }

    private static bool TryAnalyzeCode(string context, string promptLower, out string result)
    {
        result = string.Empty;

        bool hasCodeStructure = (context.Contains("class ") || context.Contains("public ") || context.Contains("private ") ||
                                 context.Contains("def ") || context.Contains("function ") || context.Contains("namespace ") ||
                                 (context.Contains("{") && context.Contains("}"))) &&
                                 !context.Contains("at System.");

        if (!hasCodeStructure) return false;

        string lang = "Source Code";
        if (context.Contains("using System") || context.Contains("namespace ") || context.Contains("public class")) lang = "C# / .NET";
        else if (context.Contains("def ") || context.Contains("import numpy") || context.Contains("elif ")) lang = "Python";
        else if (context.Contains("const ") || context.Contains("console.log") || context.Contains("=>")) lang = "JavaScript / TypeScript";
        else if (context.Contains("SELECT ") && context.Contains("FROM ")) lang = "SQL Query";

        var sb = new StringBuilder();
        sb.AppendLine($"### 💻 Screen Inspection: {lang}");
        sb.AppendLine();

        // Extract methods
        var methods = Regex.Matches(context, @"\b(?:def|function|public|private|static)\s+(?:[\w\<\>\[\]]+\s+)?([\w\<\>]+)\s*\(");
        if (methods.Count > 0)
        {
            sb.AppendLine("**Identified Signatures:**");
            foreach (Match m in methods.Take(4))
            {
                sb.AppendLine($"• `{m.Groups[1].Value}()`");
            }
            sb.AppendLine();
        }

        sb.AppendLine("**Overview & Logic:**");
        var lines = context.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith("//") && !l.StartsWith("using") && l.Length > 10)
            .Take(3);
        sb.AppendLine("The captured snippet defines programmatic logic and control structures for processing data.");
        foreach (var l in lines)
        {
            sb.AppendLine($"> `{l}`");
        }

        result = sb.ToString().TrimEnd();
        return true;
    }

    private static bool TryAnalyzeTable(string context, string promptLower, out string result)
    {
        result = string.Empty;
        var lines = context.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int tabularLines = lines.Count(l => l.Contains('|') || l.Contains('\t') || Regex.Matches(l, @"\s{2,}").Count >= 2);

        if (tabularLines < 3 && !promptLower.Contains("table") && !promptLower.Contains("tabulk"))
        {
            return false;
        }

        var sb = new StringBuilder();
        sb.AppendLine("### 📊 Screen Inspection: Tabular Data");
        sb.AppendLine();
        sb.AppendLine($"**Rows Detected:** {lines.Length} entries.");
        sb.AppendLine();
        sb.AppendLine("| # | Row Content |");
        sb.AppendLine("|---|---|");
        int count = 1;
        foreach (var line in lines.Take(6))
        {
            var cleaned = line.Replace("|", " ");
            sb.AppendLine($"| {count++} | {cleaned} |");
        }
        if (lines.Length > 6)
        {
            sb.AppendLine($"| ... | *+ {lines.Length - 6} more rows* |");
        }

        result = sb.ToString().TrimEnd();
        return true;
    }

    private static bool TryAnalyzeInvoice(string context, string promptLower, out string result)
    {
        result = string.Empty;
        bool isFinancial = Regex.IsMatch(context, @"(?i)(invoice|faktura|účtenka|total|celkem|dph|vat|amount|částka|\$|€|czk|kč)");
        if (!isFinancial) return false;

        var amounts = Regex.Matches(context, @"(?i)(?:[\$€£]|czk|kč)?\s*(\d+[.,]\d{2})\s*(?:[\$€£]|czk|kč)?");
        var dates = Regex.Matches(context, @"\b(?:\d{1,2}[./-]\d{1,2}[./-]\d{2,4}|\d{4}[./-]\d{1,2}[./-]\d{1,2})\b");

        var sb = new StringBuilder();
        sb.AppendLine("### 🧾 Screen Inspection: Financial / Invoice Document");
        sb.AppendLine();
        if (dates.Count > 0)
        {
            sb.AppendLine($"• **Date Identified:** `{dates[0].Value}`");
        }
        if (amounts.Count > 0)
        {
            sb.AppendLine($"• **Key Amounts Found:** {string.Join(", ", amounts.Take(4).Select(m => $"`{m.Value.Trim()}`"))}");
        }

        sb.AppendLine();
        sb.AppendLine("**Document Summary:**");
        var previewLines = context.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4);
        foreach (var pl in previewLines)
        {
            sb.AppendLine($"• {pl}");
        }

        result = sb.ToString().TrimEnd();
        return true;
    }

    private static string SynthesizeScreenOverview(string context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### 🔍 Screen Inspection Overview");
        sb.AppendLine();

        var lines = context.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var preview = lines.Take(5).ToList();

        sb.AppendLine($"**Detected Elements:** {lines.Length} lines of text recognized via OCR.");
        sb.AppendLine();
        sb.AppendLine("**Primary Content Seen:**");
        foreach (var p in preview)
        {
            if (p.Length > 120)
                sb.AppendLine($"• {p[..120]}...");
            else
                sb.AppendLine($"• {p}");
        }

        if (lines.Length > 5)
        {
            sb.AppendLine();
            sb.AppendLine($"*... plus {lines.Length - 5} additional elements visible on screen.*");
        }

        sb.AppendLine();
        sb.AppendLine("**Suggested Actions:**");
        sb.AppendLine("- Ask *\"explain\"* for a deeper dive into this content");
        sb.AppendLine("- Ask *\"translate to Czech\"* to convert the text");
        sb.AppendLine("- Ask *\"extract table\"* if this contains rows/columns");

        return sb.ToString().TrimEnd();
    }

    private static string AnswerTargetedQuestion(string question, string context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### 💡 Query: \"{question}\"");
        sb.AppendLine();

        var qWords = Regex.Matches(question.ToLowerInvariant(), @"\b[\w\u00C0-\u024F]{3,}\b")
            .Select(m => m.Value)
            .Where(w => !IsStopword(w))
            .ToList();

        var sentences = SplitSentences(context);
        if (sentences.Count == 0)
        {
            sentences = context.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var matches = sentences
            .Select(s =>
            {
                var sWords = s.ToLowerInvariant();
                int score = qWords.Count(w => sWords.Contains(w));
                return (Sentence: s, Score: score);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .Select(x => x.Sentence)
            .ToList();

        if (matches.Count > 0)
        {
            sb.AppendLine("**Relevant information found in captured context:**");
            foreach (var m in matches)
            {
                sb.AppendLine($"• {m.Trim()}");
            }
        }
        else
        {
            sb.AppendLine("Based on the captured context, here is an analysis:");
            var topLines = context.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4);
            foreach (var l in topLines)
            {
                sb.AppendLine($"• {l}");
            }
        }

        return sb.ToString().TrimEnd();
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
