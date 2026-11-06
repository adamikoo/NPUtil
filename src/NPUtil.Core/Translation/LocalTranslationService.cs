using System.Text;
using System.Text.RegularExpressions;
using NPUtil.Core.Logging;

namespace NPUtil.Core.Translation;

public class LocalTranslationService : ITranslationService
{
    private readonly ILogger? _logger;

    // Common bilingual dictionary for Czech <-> English
    private static readonly Dictionary<string, string> CsToEnDict = new(StringComparer.OrdinalIgnoreCase)
    {
        // Document & UI
        { "faktura", "invoice" }, { "daňový doklad", "tax document" }, { "datum vystavení", "issue date" },
        { "datum splatnosti", "due date" }, { "variabilní symbol", "variable symbol" },
        { "odběratel", "customer / client" }, { "dodavatel", "supplier / vendor" },
        { "celkem k úhradě", "total amount due" }, { "částka", "amount" }, { "cena", "price" },
        { "množství", "quantity" }, { "položka", "item" }, { "popis", "description" },
        { "sleva", "discount" }, { "daň", "tax" }, { "dph", "VAT" }, { "základ daně", "tax base" },
        { "bankovní účet", "bank account" }, { "číslo účtu", "account number" }, { "kód banky", "bank code" },
        { "smlouva", "contract / agreement" }, { "dodatek", "amendment" }, { "dohoda", "agreement" },
        { "zápis z jednání", "meeting minutes" }, { "předmět smlouvy", "subject of agreement" },
        { "podpis", "signature" }, { "zástupce", "representative" }, { "platnost", "validity" },
        { "objednávka", "purchase order" }, { "dodací list", "delivery note" },
        { "potvrzení", "confirmation" }, { "zpráva", "report / message" }, { "přehled", "overview / summary" },
        { "název souboru", "file name" }, { "velikost", "size" }, { "datum změny", "date modified" },
        { "uložit", "save" }, { "storno", "cancel" }, { "zrušit", "cancel" }, { "odstranit", "delete" },
        { "vymazat", "clear" }, { "upravit", "edit" }, { "kopírovat", "copy" }, { "vložit", "paste" },
        { "hledat", "search" }, { "vyhledat", "find" }, { "výsledky", "results" }, { "nastavení", "settings" },
        { "nápověda", "help" }, { "zavřít", "close" }, { "otevřít", "open" }, { "nový", "new" },

        // Conversational / Assistant
        { "ahoj", "hello" }, { "dobrý den", "good day" }, { "děkuji", "thank you" }, { "prosím", "please" },
        { "ano", "yes" }, { "ne", "no" }, { "shrnutí", "summary" }, { "vysvětlení", "explanation" },
        { "překlad", "translation" }, { "oprava", "correction" }, { "kód", "code" }, { "chyba", "error" },
        { "funkce", "function" }, { "třída", "class" }, { "metoda", "method" }, { "projekt", "project" }
    };

    private static readonly Dictionary<string, string> EnToCsDict = new(StringComparer.OrdinalIgnoreCase);

    static LocalTranslationService()
    {
        foreach (var (cs, en) in CsToEnDict)
        {
            var primaryEn = en.Split('/')[0].Trim();
            if (!EnToCsDict.ContainsKey(primaryEn))
            {
                EnToCsDict[primaryEn] = cs;
            }
        }

        // Additional English specific mappings
        EnToCsDict["invoice"] = "faktura";
        EnToCsDict["total"] = "celkem";
        EnToCsDict["subtotal"] = "mezisoučet";
        EnToCsDict["due date"] = "datum splatnosti";
        EnToCsDict["tax"] = "daň";
        EnToCsDict["customer"] = "zákazník / odběratel";
        EnToCsDict["vendor"] = "dodavatel";
        EnToCsDict["supplier"] = "dodavatel";
        EnToCsDict["quantity"] = "množství";
        EnToCsDict["amount"] = "částka";
        EnToCsDict["description"] = "popis";
        EnToCsDict["summary"] = "shrnutí";
        EnToCsDict["notes"] = "poznámky";
        EnToCsDict["date"] = "datum";
        EnToCsDict["status"] = "stav";
        EnToCsDict["active"] = "aktivní";
        EnToCsDict["completed"] = "dokončeno";
        EnToCsDict["pending"] = "čekající";
        EnToCsDict["approved"] = "schváleno";
        EnToCsDict["rejected"] = "odmítnuto";
    }

    public LocalTranslationService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "en";

        // Czech diacritics indicators
        int czChars = text.Count(c => "ěščřžýáíéůúťďňĚŠČŘŽÝÁÍÉŮÚŤĎŇ".Contains(c));
        if (czChars > 0) return "cs";

        // Check common Czech words
        var czWords = new[] { " a ", " v ", " se ", " na ", " že ", " to ", " je ", " pro ", " do ", " o ", " jak " };
        if (czWords.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase))) return "cs";

        return "en";
    }

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        return await Task.Run(() =>
        {
            var isToEnglish = targetLang.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            var dict = isToEnglish ? CsToEnDict : EnToCsDict;

            var lines = text.Split('\n');
            var result = new StringBuilder();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    result.AppendLine();
                    continue;
                }

                // Check exact full line match first
                var trimmedLine = line.Trim();
                if (dict.TryGetValue(trimmedLine, out var translatedLine))
                {
                    result.AppendLine(PreserveCasing(trimmedLine, translatedLine));
                    continue;
                }

                // Translate phrase-by-phrase / word-by-word
                var wordsAndDelims = Regex.Matches(line, @"[\w\u00C0-\u024F]+|[^\w\u00C0-\u024F]+");
                var lineResult = new StringBuilder();

                for (int i = 0; i < wordsAndDelims.Count; i++)
                {
                    var match = wordsAndDelims[i];
                    var val = match.Value;

                    // Check two-word phrases first
                    if (i + 2 < wordsAndDelims.Count && !string.IsNullOrWhiteSpace(wordsAndDelims[i + 1].Value))
                    {
                        var twoWord = $"{val} {wordsAndDelims[i + 2].Value}".Trim();
                        if (dict.TryGetValue(twoWord, out var translatedPhrase))
                        {
                            lineResult.Append(PreserveCasing(twoWord, translatedPhrase));
                            i += 2;
                            continue;
                        }
                    }

                    if (dict.TryGetValue(val, out var translatedWord))
                    {
                        lineResult.Append(PreserveCasing(val, translatedWord));
                    }
                    else
                    {
                        lineResult.Append(val);
                    }
                }

                result.AppendLine(lineResult.ToString());
            }

            return result.ToString().TrimEnd();
        }, ct);
    }

    private static string PreserveCasing(string original, string translated)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(translated)) return translated;

        if (original.All(char.IsUpper) && original.Length > 1)
        {
            return translated.ToUpperInvariant();
        }

        if (char.IsUpper(original[0]))
        {
            return char.ToUpperInvariant(translated[0]) + (translated.Length > 1 ? translated[1..] : string.Empty);
        }

        return translated.ToLowerInvariant();
    }
}
