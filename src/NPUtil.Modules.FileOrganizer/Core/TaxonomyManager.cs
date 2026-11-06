using System.Text.Json;
using System.Text.Json.Serialization;

namespace NPUtil.Modules.FileOrganizer.Core;

public class CategoryDefinition
{
    public string RelativePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public List<string> FileExtensions { get; set; } = new();
    public List<string> RegexPatterns { get; set; } = new();
    public bool IsExcluded { get; set; } = false;
    public int Priority { get; set; } = 0;

    public CategoryDefinition() { }

    public CategoryDefinition(string relativePath, string name, string description, string[]? keywords = null, string[]? extensions = null, string[]? regex = null, int priority = 0)
    {
        RelativePath = relativePath;
        Name = name;
        Description = description;
        if (keywords != null) Keywords.AddRange(keywords);
        if (extensions != null) FileExtensions.AddRange(extensions);
        if (regex != null) RegexPatterns.AddRange(regex);
        Priority = priority;
    }
}

public class TaxonomyManager
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _taxonomyFilePath;
    private readonly List<CategoryDefinition> _categories = new();
    private readonly object _lock = new();

    public event EventHandler? TaxonomyChanged;

    public TaxonomyManager(string? customPath = null)
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NPUtil", "Config");
        Directory.CreateDirectory(configDir);
        _taxonomyFilePath = customPath ?? Path.Combine(configDir, "taxonomy.json");

        LoadOrCreateDefaults();
    }

    public IReadOnlyList<CategoryDefinition> GetCategories()
    {
        lock (_lock)
        {
            return _categories.OrderByDescending(c => c.Priority).ThenBy(c => c.RelativePath).ToList();
        }
    }

    public CategoryDefinition? FindByRelativePath(string relativePath)
    {
        lock (_lock)
        {
            var normalized = NormalizePath(relativePath);
            return _categories.FirstOrDefault(c => NormalizePath(c.RelativePath).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void AddOrUpdateCategory(CategoryDefinition category)
    {
        lock (_lock)
        {
            var normalized = NormalizePath(category.RelativePath);
            var existingIndex = _categories.FindIndex(c => NormalizePath(c.RelativePath).Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                _categories[existingIndex] = category;
            }
            else
            {
                _categories.Add(category);
            }
            Save();
        }
        TaxonomyChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RemoveCategory(string relativePath)
    {
        var removed = false;
        lock (_lock)
        {
            var normalized = NormalizePath(relativePath);
            removed = _categories.RemoveAll(c => NormalizePath(c.RelativePath).Equals(normalized, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) Save();
        }
        if (removed) TaxonomyChanged?.Invoke(this, EventArgs.Empty);
        return removed;
    }

    public void RenameCategory(string oldRelativePath, string newRelativePath, string newName)
    {
        lock (_lock)
        {
            var normOld = NormalizePath(oldRelativePath);
            var cat = _categories.FirstOrDefault(c => NormalizePath(c.RelativePath).Equals(normOld, StringComparison.OrdinalIgnoreCase));
            if (cat != null)
            {
                cat.RelativePath = newRelativePath;
                cat.Name = newName;
                Save();
            }
        }
        TaxonomyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string NormalizePath(string path) =>
        path.Trim().Trim('\\', '/').Replace('/', '\\');

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_categories, JsonOpts);
                File.WriteAllText(_taxonomyFilePath, json);
            }
            catch { /* ignore */ }
        }
    }

    private void LoadOrCreateDefaults()
    {
        lock (_lock)
        {
            if (File.Exists(_taxonomyFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_taxonomyFilePath);
                    var list = JsonSerializer.Deserialize<List<CategoryDefinition>>(json, JsonOpts);
                    if (list != null && list.Count > 0)
                    {
                        _categories.Clear();
                        _categories.AddRange(list);
                        return;
                    }
                }
                catch { /* fall through to defaults */ }
            }

            _categories.Clear();
            _categories.AddRange(GetDefaultTaxonomy());
            Save();
        }
    }

    public static List<CategoryDefinition> GetDefaultTaxonomy()
    {
        var currentYear = DateTime.Now.Year.ToString();

        return new List<CategoryDefinition>
        {
            // Education
            new(@"Education\Languages\English", "English Learning",
                "Cambridge C1 Advanced, CAE, IELTS, TOEFL, English textbooks, grammar guides, vocabulary exercises, English language certificates, study materials",
                keywords: ["cambridge", "c1", "advanced", "ielts", "toefl", "english", "textbook", "grammar", "vocabulary", "anglictina", "cefr"],
                extensions: [".pdf", ".epub", ".docx", ".mobi"],
                priority: 15),

            new(@"Education\Languages\Czech", "Czech Language",
                "Czech language learning, Czech grammar, maturita čeština, cvičebnice českého jazyka, pravopis, literatura",
                keywords: ["cestina", "český", "pravopis", "mluvnice", "diktat", "maturita"],
                extensions: [".pdf", ".docx"],
                priority: 10),

            new(@"Education\School", "School & University",
                "School assignments, university homework, lecture slides, syllabus, exam preparations, essays, academic notes, úkoly, přednášky, cvičení, semestrální práce",
                keywords: ["homework", "assignment", "lecture", "prednaska", "ukol", "semestralka", "zkouska", "test", "syllabus", "cviceni", "skola"],
                extensions: [".pdf", ".docx", ".pptx", ".xlsx", ".txt"],
                priority: 12),

            new(@"Education\Mathematics", "Mathematics",
                "Mathematics, calculus, algebra, geometry, statistics, discrete math, linear algebra, formulas, matematika, lineární algebra, derivace, integrály",
                keywords: ["math", "mathematics", "algebra", "calculus", "geometry", "statistics", "integral", "matematika", "statistika"],
                extensions: [".pdf", ".docx"],
                priority: 10),

            new(@"Education\Learning Resources", "Learning Resources",
                "Tutorials, cheatsheets, technical reference guides, study guides, educational books, přehledy, taháky, studijní materiály",
                keywords: ["tutorial", "cheatsheet", "guide", "handbook", "learning", "study", "prirucka", "tahak"],
                extensions: [".pdf", ".epub", ".docx", ".md"],
                priority: 8),

            // Programming
            new(@"Programming\Projects\Website", "Website Projects",
                "Website source code, HTML, CSS, frontend scripts, web designs, landing page, portfolio, blog, webové stránky, šablony",
                keywords: ["website", "landing", "web", "frontend", "html", "css", "portfolio", "wordpress", "react", "vue"],
                extensions: [".html", ".css", ".js", ".ts", ".jsx", ".tsx", ".vue"],
                priority: 14),

            new(@"Programming\Projects", "Programming Projects",
                "Software projects, git repositories, solution files, application development, apps, skripty, projekty",
                keywords: ["project", "solution", "repo", "application", "software", "projekt"],
                extensions: [".sln", ".csproj", ".py", ".rs", ".go", ".cpp"],
                priority: 10),

            new(@"Programming\Source Code", "Source Code",
                "Scripts and standalone source code files in C#, Python, TypeScript, JavaScript, Rust, C++, Go, scripts, skripty",
                keywords: ["script", "function", "class", "def", "import", "namespace", "code", "kod"],
                extensions: [".cs", ".py", ".js", ".ts", ".cpp", ".c", ".h", ".rs", ".go", ".java", ".php", ".sh", ".ps1", ".sql"],
                priority: 12),

            new(@"Programming\Documentation", "API & Developer Documentation",
                "API documentation, software specifications, architecture diagrams, SDK manuals, technická dokumentace",
                keywords: ["api", "documentation", "reference", "manual", "spec", "architecture", "sdk", "dokumentace"],
                extensions: [".md", ".pdf", ".json", ".yaml", ".yml"],
                priority: 8),

            // Finance
            new($@"Finance\Invoices\{currentYear}", $"Invoices {currentYear}",
                $"Invoices, bills, billing statements, faktura, daňový doklad, payment receipts, bank receipts, accounting, vyúčtování, zálohová faktura {currentYear}",
                keywords: ["invoice", "faktura", "faktury", "doklad", "vyuctovani", "bill", "billing", "receipt", "dph", "vat", "iban", "platba", currentYear],
                extensions: [".pdf", ".xml", ".docx", ".xlsx"],
                regex: [@"faktur", @"invoice", @"doklad", @"inv[-_]?\d+"],
                priority: 20),

            new(@"Finance\Invoices", "Invoices & Receipts",
                "Invoices, receipts, tax documents, faktury a účtenky z minulých let, paragony",
                keywords: ["invoice", "faktura", "receipt", "uctenka", "paragon", "doklad"],
                extensions: [".pdf", ".xml"],
                priority: 15),

            new(@"Finance\Trading\Strategies", "Trading Strategies",
                "Trading strategies, MT5, MetaTrader, Expert Advisors, backtests, algorithmic trading, forex, indicators, obchodní strategie, indikátory",
                keywords: ["mt5", "metatrader", "expert advisor", "backtest", "trading", "strategy", "forex", "algorithmic", "indikator"],
                extensions: [".pdf", ".mq5", ".mq4", ".ex5", ".csv", ".docx"],
                priority: 18),

            new(@"Finance\Banking", "Banking & Statements",
                "Bank account statements, transaction histories, credit card statements, bankovní výpisy, potvrzení o převodu",
                keywords: ["bank", "statement", "vypis", "transakce", "ucet", "airbank", "csob", "sporitelna", "fio", "revolut"],
                extensions: [".pdf", ".csv", ".xlsx"],
                priority: 16),

            new(@"Finance\Investments", "Investments",
                "Stock portfolios, investment reports, ETF prospectus, dividend summaries, investice, akcie, portfolio",
                keywords: ["investment", "stocks", "etf", "portfolio", "dividend", "investice", "akcie"],
                extensions: [".pdf", ".xlsx"],
                priority: 10),

            // Media
            new(@"Media\Images\Wallpapers", "Wallpapers",
                "High resolution wallpapers, desktop backgrounds, tapety na plochu, 4K wallpapers",
                keywords: ["wallpaper", "background", "desktop", "tapeta", "4k", "uhd"],
                extensions: [".jpg", ".jpeg", ".png", ".webp"],
                priority: 12),

            new(@"Media\Images\Cars", "Car Photographs",
                "Photographs of cars, automobiles, vehicles, sports cars, supercars, automobily, auta, vozidla",
                keywords: ["car", "automobile", "auto", "vehicle", "ferrari", "porsche", "bmw", "audi", "mercedes", "motor"],
                extensions: [".jpg", ".jpeg", ".png", ".webp"],
                priority: 14),

            new(@"Media\Images", "Photos & Images",
                "General photos, pictures, graphics, illustrations, screenshots, fotografie, obrázky",
                keywords: ["photo", "picture", "image", "screenshot", "fotka", "obrazek", "snimek"],
                extensions: [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".svg", ".ico"],
                priority: 5),

            new(@"Media\Videos", "Videos",
                "Video recordings, films, tutorials, screen recordings, videa, záznamy obrazovky",
                keywords: ["video", "recording", "movie", "film", "zaznam"],
                extensions: [".mp4", ".mkv", ".mov", ".avi", ".webm"],
                priority: 10),

            new(@"Media\Audio", "Audio & Music",
                "Music tracks, audio recordings, podcasts, sound effects, hudba, zvukové nahrávky",
                keywords: ["audio", "music", "song", "podcast", "sound", "hudba", "zvuk"],
                extensions: [".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg"],
                priority: 10),

            // Software
            new(@"Software\Installers", "Software Installers",
                "Application setups, installer executables, MSI packages, instalační balíčky softwaru",
                keywords: ["setup", "install", "installer", "instalator"],
                extensions: [".exe", ".msi", ".appx", ".msix", ".msixbundle"],
                regex: [@".*setup.*", @".*install.*"],
                priority: 15),

            new(@"Software\Utilities", "Portable Tools & Utilities",
                "Portable applications, system utilities, diagnostic tools, pomocné nástroje",
                keywords: ["portable", "tool", "utility", "nastroj"],
                extensions: [".exe"],
                priority: 8),

            new(@"Software\Drivers", "Hardware Drivers",
                "Hardware device drivers, firmware updates, BIOS files, ovladače hardware",
                keywords: ["driver", "firmware", "bios", "ovladac"],
                extensions: [".inf", ".sys", ".cat", ".exe"],
                priority: 12),

            // Work
            new(@"Work\Newsletters", "Newsletters & Bulletins",
                "Zpravodaje, věstníky, bulletiny, periodika, tiskové zprávy, firemní a obecní zpravodaj, newsletters",
                keywords: ["zpravodaj", "vestnik", "věstník", "bulletin", "newsletter", "oběžník", "ročník", "vydání"],
                extensions: [".pdf", ".docx"],
                priority: 16),

            new(@"Work\Documents", "Work Documents",
                "Work reports, memos, business presentations, meeting minutes, contracts, pracovní dokumenty, smlouvy, zápisy",
                keywords: ["work", "business", "contract", "meeting", "report", "smlouva", "prace", "zapis"],
                extensions: [".pdf", ".docx", ".xlsx", ".pptx"],
                priority: 10),

            new(@"Work\Projects", "Work Projects",
                "Work project files, client deliverables, project specifications, business planning, firemní projekty",
                keywords: ["work", "client", "deliverable", "project", "firemni", "zakazka"],
                extensions: [".pdf", ".docx", ".xlsx", ".pptx", ".zip"],
                priority: 10),

            // Personal
            new(@"Personal\Health & Medical", "Health & Medical",
                "Lékařské zprávy, recepty, vyšetření, očkování, zdravotní dokumentace, medical reports",
                keywords: ["lékař", "lekar", "zdravotni", "recept", "vysetreni", "pacient", "hospital"],
                extensions: [".pdf", ".docx", ".jpg"],
                priority: 12),

            new(@"Personal\Travel & Tickets", "Travel & Tickets",
                "Letenky, jízdenky, rezervace ubytování, hotel, boarding pass, vstupenky",
                keywords: ["letenka", "jizdenka", "boarding pass", "flight", "hotel", "rezervace", "booking"],
                extensions: [".pdf", ".pkpass"],
                priority: 12),

            // Archives
            new(@"Archives", "Compressed Archives",
                "ZIP archives, 7z archives, RAR archives, tarballs, komprimované archivy",
                keywords: ["archive", "zip", "backup", "archiv"],
                extensions: [".zip", ".7z", ".rar", ".tar", ".gz", ".bz2"],
                priority: 5),

            // To Review
            new(@"To Review", "Ambiguous / Review",
                "Files requiring manual inspection, ambiguous content, unclassified items",
                keywords: ["review", "unknown"],
                extensions: [],
                priority: 0)
        };
    }
}
