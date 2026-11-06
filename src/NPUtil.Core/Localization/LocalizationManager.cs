using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NPUtil.Core.Localization;

public class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _instance.Value;

    private string _currentLanguage = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged(string.Empty); // refresh all bindings
            }
        }
    }

    public string this[string key] => GetString(key);

    public static string Get(string key) => Instance.GetString(key);

    public string GetString(string key, params object[] args)
    {
        var dict = _currentLanguage == "cs" ? CzechStrings : EnglishStrings;
        if (!dict.TryGetValue(key, out var val))
        {
            if (!EnglishStrings.TryGetValue(key, out val))
            {
                val = key;
            }
        }

        if (args != null && args.Length > 0)
        {
            try
            {
                return string.Format(val, args);
            }
            catch
            {
                return val;
            }
        }

        return val;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static readonly Dictionary<string, string> EnglishStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // App
        { "AppTitle", "NPUtil — Windows Utility Suite" },
        { "NavFileOrganizer", "File Organizer" },
        { "NavHardware", "System & Diagnostics" },
        { "NavSettings", "Settings" },

        // Tabs
        { "TabOverview", "Overview" },
        { "TabReview", "Review Queue" },
        { "TabExplorer", "Categories & Folders" },
        { "TabSearch", "Search" },
        { "TabActivity", "History & Undo" },
        { "TabModuleSettings", "Module Preferences" },

        // Overview
        { "OverviewTitle", "File Organizer" },
        { "OverviewSubtitle", "Continuously monitors the Downloads folder and automatically organizes files into semantic categories." },
        { "MonitoringActiveTitle", "Folder monitoring active" },
        { "MonitoringPausedTitle", "Folder monitoring paused" },
        { "MonitoringWatchedFolder", "Watched folder: {0}" },
        { "BtnPause", "Pause" },
        { "BtnResume", "Resume" },
        { "BtnScanNow", "Scan Now" },
        { "StatusOverview", "Status Overview" },
        { "StatIndexed", "INDEXED FILES" },
        { "StatOrganized", "ORGANIZED FILES" },
        { "StatPending", "AWAITING REVIEW" },
        { "StatUndone", "UNDONE MOVES" },
        { "AiAcceleration", "Hardware Acceleration & AI Model" },
        { "ActiveProvider", "Active inference provider:" },
        { "BtnDownloadModel", "Download Neural Model (~112 MB)" },
        { "RecentActivity", "Recent Organization Activity" },
        { "ColTime", "Time" },
        { "ColFileName", "File Name" },
        { "ColDestination", "Destination Category" },
        { "ColConfidence", "Confidence" },
        { "ColReason", "Reasoning / Keywords" },
        { "ColAction", "Action" },
        { "ColType", "Type" },

        // Review Queue
        { "ReviewTitle", "Review Queue" },
        { "ReviewSubtitle", "Inspect and approve suggested file organization before safe moves are executed." },
        { "PendingCountFormat", "{0} files awaiting approval" },
        { "BtnRefresh", "Refresh Queue" },
        { "BtnApproveAll", "Approve All in Queue" },
        { "BtnApprove", "Approve" },
        { "BtnReject", "Reject" },
        { "DetailTitle", "Selected File Details" },
        { "DetailPlaceholder", "Select a file above to inspect classification reasoning and alternative categories." },
        { "AltCategories", "Alternative categories:" },
        { "BtnChooseThis", "Choose this" },
        { "ChangeDestination", "Change destination category:" },
        { "BtnApply", "Apply" },
        { "ExtractedKeywords", "Extracted keywords: [{0}]" },
        { "BatchApprovalComplete", "Batch Approval Complete" },
        { "BatchApprovalMsg", "Successfully approved and organized {0} files." },

        // Folder Explorer
        { "ExplorerTitle", "Categories & Folders" },
        { "ExplorerSubtitle", "Edit category hierarchy, AI semantic descriptions and assigned keywords." },
        { "AddCategory", "Add Category" },
        { "CategoryDetails", "Category Details" },
        { "CategoryPath", "Relative destination path:" },
        { "SemanticDescription", "Semantic description (for AI vector matching):" },
        { "Keywords", "Keywords (comma separated):" },
        { "FileExtensions", "File extensions (e.g. .pdf, .docx):" },
        { "BtnSaveCategory", "Save Category" },
        { "BtnDeleteCategory", "Delete Category" },
        { "CategorySaved", "Category saved successfully." },

        // Search
        { "SearchTitle", "Local & Semantic Search" },
        { "SearchSubtitle", "Search indexed files by filename, category, extension or semantic meaning." },
        { "SearchPlaceholder", "Enter search query or filename..." },
        { "AllCategories", "All Categories" },
        { "AllExtensions", "All Types" },
        { "BtnSearch", "Search" },
        { "SearchResultsFormat", "{0} results found" },
        { "BtnOpenFile", "Open File" },
        { "BtnOpenFolder", "Open in Explorer" },
        { "ColMatchScore", "Match Score" },
        { "ColSize", "Size" },
        { "ColExtractedSnippet", "Extracted Snippet" },

        // Activity
        { "ActivityTitle", "Operation History & Undo" },
        { "ActivitySubtitle", "Persistent audit log of all organized files with one-click reversible undo." },
        { "BtnUndoBatch", "Undo Selected Batch" },
        { "BtnUndoMove", "Undo Move" },
        { "BtnRestored", "Restored" },
        { "HistorySummaryFormat", "Total operations: {0}  |  Active: {1}  |  Undone: {2}" },
        { "UndoSuccessTitle", "Undo Successful" },
        { "UndoSuccessMsg", "File successfully restored to:\n{0}" },
        { "UndoFailedTitle", "Undo Failed" },
        { "UndoFailedMsg", "Could not undo move: {0}" },
        { "BatchUndoConfirmTitle", "Confirm Batch Undo" },
        { "BatchUndoConfirmMsg", "Are you sure you want to undo all operations in batch '{0}'?" },
        { "BatchUndoDoneTitle", "Batch Undo Complete" },
        { "BatchUndoDoneMsg", "Restored {0} out of {1} files in batch." },

        // Settings (Module)
        { "ModuleSettingsTitle", "File Organizer Preferences" },
        { "ModuleSettingsSubtitle", "Configure monitoring parameters, automation level and hardware acceleration." },
        { "WatchedDirSection", "Watched Directory" },
        { "WatchedPathLabel", "Downloads folder path:" },
        { "BtnBrowse", "Browse..." },
        { "KnownFolderHint", "NPUtil automatically resolves your official Windows Downloads folder using the Known Folders API." },
        { "AutomationSection", "Automation Level" },
        { "ModeReviewEverything", "Review Everything (Recommended)" },
        { "ModeReviewDesc", "Every suggested move is placed into the Review Queue awaiting your confirmation." },
        { "ModeIntelligentAuto", "Intelligent Automation" },
        { "ModeIntelligentDesc", "Moves high-confidence matches automatically; places ambiguous files into the queue." },
        { "ModeFullyAuto", "Fully Automatic" },
        { "ModeFullyDesc", "Organize all files immediately upon download completion according to highest match." },
        { "ConfidenceThreshold", "Confidence threshold for automatic moves:" },
        { "HardwarePowerSection", "Hardware & Power Management" },
        { "AiProviderLabel", "AI acceleration provider:" },
        { "AiAuto", "Automatic (Qualcomm Hexagon NPU if available, otherwise CPU)" },
        { "AiForceQnn", "Force Qualcomm Hexagon NPU (QNN HTP)" },
        { "AiForceCpu", "Force CPU (ARM64 Vectorized / NEON)" },
        { "OptThrottleBattery", "Throttle background tasks and pause scanning on battery power" },
        { "OptIgnoreIncomplete", "Ignore incomplete browser downloads (.crdownload, .part, .tmp)" },
        { "OptIgnoreHidden", "Ignore hidden and system files" },
        { "BtnSaveSettings", "Save Settings" },
        { "SettingsSaved", "Settings saved successfully." },

        // Global Settings
        { "GlobalSettingsTitle", "NPUtil Settings" },
        { "GlobalSettingsSubtitle", "Global appearance preferences, Windows integration and background behavior." },
        { "AppearanceSection", "Appearance & Theme" },
        { "ThemeLabel", "Application color theme" },
        { "ThemeDesc", "NPUtil automatically respects Windows 11 Light and Dark modes." },
        { "ThemeSystem", "Match Windows theme" },
        { "ThemeLight", "Light mode" },
        { "ThemeDark", "Dark mode" },
        { "LanguageLabel", "Application Language" },
        { "LanguageDesc", "Choose the display language for the application." },
        { "LangEn", "English (Default)" },
        { "LangCs", "Čeština (Czech)" },
        { "WindowsIntegrationSection", "Windows Integration" },
        { "StartWithWindowsLabel", "Start with Windows" },
        { "StartWithWindowsDesc", "Automatically launch NPUtil minimized in the background on user login." },
        { "TrayIconLabel", "Show icon in System Tray" },
        { "TrayIconDesc", "Allows quick access, manual scans and status monitoring next to the clock." },
        { "CloseToTrayLabel", "Minimize to tray on window close" },
        { "CloseToTrayDesc", "Keep background monitoring active when closing the main window." },
        { "BatterySection", "Resource & Power Management" },
        { "BatteryThrottleLabel", "Throttle processing on battery" },
        { "BatteryThrottleDesc", "Switch to lightweight extraction profiles when on battery to preserve battery life." },

        // Hardware Diagnostics
        { "HardwareTitle", "System & Hardware Diagnostics" },
        { "HardwareSubtitle", "Information about ARM64 execution, Qualcomm Hexagon NPU and live activity logs." },
        { "PlatformCardTitle", "Hardware Platform" },
        { "ProcessorLabel", "Processor:" },
        { "ArchitectureLabel", "Architecture:" },
        { "MemoryLabel", "System Memory:" },
        { "OsLabel", "Operating System:" },
        { "AiCardTitle", "AI Acceleration & Power" },
        { "ActiveAiLabel", "Active AI inference provider:" },
        { "QnnStatusLabel", "Qualcomm QNN Library Status:" },
        { "PowerStatusLabel", "Power source:" },
        { "LiveLogsTitle", "Live Application Logs" },
        { "BtnExportLogs", "Export Log to File..." },
        { "PowerAc", "AC Power (Full Performance)" },
        { "PowerBattery", "Battery ({0}%)" },

        // Status Bar
        { "StatusMonitoringActive", "Monitoring: Active ({0})" },
        { "StatusMonitoringPaused", "Monitoring: Paused" }
    };

    private static readonly Dictionary<string, string> CzechStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // App
        { "AppTitle", "NPUtil — Windows Utility Suite" },
        { "NavFileOrganizer", "Organizace souborů" },
        { "NavHardware", "Systém a diagnostika" },
        { "NavSettings", "Nastavení" },

        // Tabs
        { "TabOverview", "Přehled" },
        { "TabReview", "Fronta k revizi" },
        { "TabExplorer", "Kategorie a složky" },
        { "TabSearch", "Vyhledávání" },
        { "TabActivity", "Historie a Undo" },
        { "TabModuleSettings", "Předvolby modulu" },

        // Overview
        { "OverviewTitle", "Organizace souborů" },
        { "OverviewSubtitle", "Průběžně monitoruje složku Stažené soubory a automaticky navrhuje sémantické zařazení do kategorií." },
        { "MonitoringActiveTitle", "Sledování složky aktivní" },
        { "MonitoringPausedTitle", "Sledování složky pozastaveno" },
        { "MonitoringWatchedFolder", "Sledovaný adresář: {0}" },
        { "BtnPause", "Pozastavit" },
        { "BtnResume", "Obnovit" },
        { "BtnScanNow", "Spustit skenování" },
        { "StatusOverview", "Přehled stavu" },
        { "StatIndexed", "INDEXOVANÉ SOUBORY" },
        { "StatOrganized", "ORGANIZOVANÉ SOUBORY" },
        { "StatPending", "ČEKÁ NA SCHVÁLENÍ" },
        { "StatUndone", "VRÁCENÉ PŘESUNY" },
        { "AiAcceleration", "Hardwarová akcelerace a AI model" },
        { "ActiveProvider", "Aktivní zprostředkovatel inference:" },
        { "BtnDownloadModel", "Stáhnout neuronový model (~112 MB)" },
        { "RecentActivity", "Poslední operace organizace" },
        { "ColTime", "Čas" },
        { "ColFileName", "Název souboru" },
        { "ColDestination", "Cílová kategorie" },
        { "ColConfidence", "Jistota" },
        { "ColReason", "Důvod zařazení / Klíčová slova" },
        { "ColAction", "Akce" },
        { "ColType", "Typ" },

        // Review Queue
        { "ReviewTitle", "Fronta k revizi" },
        { "ReviewSubtitle", "Zkontrolujte a schvalte navržená zařazení před provedením bezpečného přesunu." },
        { "PendingCountFormat", "{0} souborů čeká na schválení" },
        { "BtnRefresh", "Obnovit frontu" },
        { "BtnApproveAll", "Schválit vše ve frontě" },
        { "BtnApprove", "Schválit" },
        { "BtnReject", "Odmítnout" },
        { "DetailTitle", "Detail vybraného souboru" },
        { "DetailPlaceholder", "Vyberte soubor v tabulce pro zobrazení podrobností o sémantické analýze a alternativách." },
        { "AltCategories", "Alternativní kategorie:" },
        { "BtnChooseThis", "Zvolit tuto" },
        { "ChangeDestination", "Změnit cílovou kategorii:" },
        { "BtnApply", "Použít" },
        { "ExtractedKeywords", "Extrahovaná klíčová slova: [{0}]" },
        { "BatchApprovalComplete", "Hromadné schválení dokončeno" },
        { "BatchApprovalMsg", "Úspěšně schváleno a uspořádáno {0} souborů." },

        // Folder Explorer
        { "ExplorerTitle", "Kategorie a složky" },
        { "ExplorerSubtitle", "Upravujte hierarchii složek, sémantické popisy pro AI model a přiřazená klíčová slova." },
        { "AddCategory", "Přidat kategorii" },
        { "CategoryDetails", "Detail kategorie" },
        { "CategoryPath", "Relativní cílová cesta:" },
        { "SemanticDescription", "Sémantický popis (pro párování AI vektory):" },
        { "Keywords", "Klíčová slova (oddělená čárkami):" },
        { "FileExtensions", "Přípony souborů (např. .pdf, .docx):" },
        { "BtnSaveCategory", "Uložit kategorii" },
        { "BtnDeleteCategory", "Smazat kategorii" },
        { "CategorySaved", "Kategorie byla úspěšně uložena." },

        // Search
        { "SearchTitle", "Lokální a sémantické vyhledávání" },
        { "SearchSubtitle", "Prohledávejte indexované soubory podle názvu, kategorie, přípony i sémantického významu obsahu." },
        { "SearchPlaceholder", "Zadejte název souboru nebo sémantický dotaz..." },
        { "AllCategories", "Všechny kategorie" },
        { "AllExtensions", "Všechny typy" },
        { "BtnSearch", "Hledat" },
        { "SearchResultsFormat", "Nalezeno {0} výsledků" },
        { "BtnOpenFile", "Otevřít soubor" },
        { "BtnOpenFolder", "Otevřít složku" },
        { "ColMatchScore", "Skóre shody" },
        { "ColSize", "Velikost" },
        { "ColExtractedSnippet", "Úryvek obsahu" },

        // Activity
        { "ActivityTitle", "Historie operací a Undo" },
        { "ActivitySubtitle", "Kompletní persistentní auditní stopa všech provedených přesunů s možností okamžitého vrácení." },
        { "BtnUndoBatch", "Vrátit celou dávku" },
        { "BtnUndoMove", "Vrátit přesun" },
        { "BtnRestored", "Obnoveno" },
        { "HistorySummaryFormat", "Celkem operací: {0}  |  Aktivní přesuny: {1}  |  Vrácené (Undo): {2}" },
        { "UndoSuccessTitle", "Obnovení úspěšné" },
        { "UndoSuccessMsg", "Soubor byl úspěšně vrácen do:\n{0}" },
        { "UndoFailedTitle", "Obnovení se nezdařilo" },
        { "UndoFailedMsg", "Přesun nelze vrátit: {0}" },
        { "BatchUndoConfirmTitle", "Potvrdit vrácení dávky" },
        { "BatchUndoConfirmMsg", "Opravdu si přejete vrátit všechny operace v dávce '{0}'?" },
        { "BatchUndoDoneTitle", "Vrácení dávky dokončeno" },
        { "BatchUndoDoneMsg", "Úspěšně obnoveno {0} z {1} souborů v dávce." },

        // Settings (Module)
        { "ModuleSettingsTitle", "Předvolby modulu Organizace souborů" },
        { "ModuleSettingsSubtitle", "Konfigurace parametrů sledování, úrovně automatizace a hardwarové akcelerace." },
        { "WatchedDirSection", "Sledovaný adresář" },
        { "WatchedPathLabel", "Cesta ke složce Stažené soubory:" },
        { "BtnBrowse", "Procházet..." },
        { "KnownFolderHint", "NPUtil automaticky zjišťuje skutečné systémové umístění složky Downloads přes Windows Known Folders API." },
        { "AutomationSection", "Úroveň automatizace" },
        { "ModeReviewEverything", "Revize všeho (Doporučeno)" },
        { "ModeReviewDesc", "Každý navržený přesun se zařadí do Fronty k revizi a čeká na vaše potvrzení." },
        { "ModeIntelligentAuto", "Inteligentní automatika" },
        { "ModeIntelligentDesc", "Jednoznačné soubory s vysokou jistotou přesune automaticky, sporné případy zařadí do fronty." },
        { "ModeFullyAuto", "Plně automatický" },
        { "ModeFullyDesc", "Organizovat všechny soubory ihned po dokončení stahování podle nejvyšší shody." },
        { "ConfidenceThreshold", "Prah jistoty pro automatické zařazení:" },
        { "HardwarePowerSection", "Hardware a správa napájení" },
        { "AiProviderLabel", "Zprostředkovatel AI akcelerace:" },
        { "AiAuto", "Automaticky (Qualcomm Hexagon NPU pokud je k dispozici, jinak CPU)" },
        { "AiForceQnn", "Vynutit Qualcomm Hexagon NPU (QNN HTP)" },
        { "AiForceCpu", "Vynutit CPU (ARM64 Vectorized / NEON)" },
        { "OptThrottleBattery", "Omezit úlohy a pozastavit skenování při chodu na baterii" },
        { "OptIgnoreIncomplete", "Ignorovat nedokončená stahování prohlížeče (.crdownload, .part, .tmp)" },
        { "OptIgnoreHidden", "Ignorovat skryté a systémové soubory" },
        { "BtnSaveSettings", "Uložit nastavení" },
        { "SettingsSaved", "Nastavení bylo úspěšně uloženo." },

        // Global Settings
        { "GlobalSettingsTitle", "Nastavení NPUtil" },
        { "GlobalSettingsSubtitle", "Globální předvolby vzhledu, integrace se systémem Windows a chování na pozadí." },
        { "AppearanceSection", "Vzhled a motiv" },
        { "ThemeLabel", "Barevný motiv aplikace" },
        { "ThemeDesc", "Aplikace automaticky respektuje nastavení světlého i tmavého režimu Windows 11." },
        { "ThemeSystem", "Podle systému Windows" },
        { "ThemeLight", "Světlý režim" },
        { "ThemeDark", "Tmavý režim" },
        { "LanguageLabel", "Jazyk aplikace" },
        { "LanguageDesc", "Zvolte jazyk uživatelského rozhraní." },
        { "LangEn", "English (Výchozí)" },
        { "LangCs", "Čeština" },
        { "WindowsIntegrationSection", "Integrace se systémem Windows" },
        { "StartWithWindowsLabel", "Spouštět se systémem Windows" },
        { "StartWithWindowsDesc", "Automaticky spustit NPUtil na pozadí po přihlášení uživatele." },
        { "TrayIconLabel", "Zobrazovat ikonu v oznamovací oblasti (System Tray)" },
        { "TrayIconDesc", "Umožňuje rychlé spuštění skenování a zobrazení stavu vedle hodin." },
        { "CloseToTrayLabel", "Při zavření okna minimalizovat do lišty" },
        { "CloseToTrayDesc", "Ponechat sledování aktivní na pozadí i po zavření hlavního okna." },
        { "BatterySection", "Správa prostředků a napájení" },
        { "BatteryThrottleLabel", "Omezit zpracování při chodu na baterii" },
        { "BatteryThrottleDesc", "Při provozu na baterii použít odlehčený profil extrakce pro úsporu energie." },

        // Hardware Diagnostics
        { "HardwareTitle", "Systém a diagnostika hardwaru" },
        { "HardwareSubtitle", "Informace o běhovém prostředí ARM64, Qualcomm Hexagon NPU a živý záznam událostí." },
        { "PlatformCardTitle", "Hardwarová platforma" },
        { "ProcessorLabel", "Procesor:" },
        { "ArchitectureLabel", "Architektura:" },
        { "MemoryLabel", "Operační paměť:" },
        { "OsLabel", "Operační systém:" },
        { "AiCardTitle", "AI akcelerace a napájení" },
        { "ActiveAiLabel", "Aktivní zprostředkovatel AI:" },
        { "QnnStatusLabel", "Stav nativních knihoven QNN:" },
        { "PowerStatusLabel", "Zdroj napájení:" },
        { "LiveLogsTitle", "Živý záznam událostí (Aplikační log)" },
        { "BtnExportLogs", "Exportovat záznam do souboru..." },
        { "PowerAc", "Síťové napájení (Plný výkon)" },
        { "PowerBattery", "Baterie ({0} %)" },

        // Status Bar
        { "StatusMonitoringActive", "Sledování: Aktivní ({0})" },
        { "StatusMonitoringPaused", "Sledování: Pozastaveno" }
    };
}
