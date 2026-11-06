# Příručka pro vývojáře: Jak přidat nový modul do NPUtil

Jedním z klíčových požadavků na NPUtil je **rozšiřitelnost bez nutnosti refaktorovat jádro aplikace**.

Tento dokument vysvětluje, jak vytvořit a zaregistrovat nový nezávislý nástroj (např. *Clipboard Manager*, *Batch Image Converter*, *System Cleaner*).

---

## 1. Architektura modulu

Každý modul implementuje rozhraní `NPUtil.Core.Modules.IModule`:

```csharp
namespace NPUtil.Core.Modules;

public interface IModule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string IconGlyph { get; }
    int SortOrder { get; }
    bool IsEnabled { get; set; }

    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
    object CreateView();
}
```

---

## 2. Krok za krokem: Vytvoření nového modulu

Představme si vytvoření nového modulu **SystemCleaner**:

### Krok A: Založení knihovny tříd
Vytvořte projekt ve složce `src/NPUtil.Modules.SystemCleaner`:

```powershell
dotnet new classlib -o src/NPUtil.Modules.SystemCleaner -n NPUtil.Modules.SystemCleaner -f net9.0-windows
dotnet sln add src/NPUtil.Modules.SystemCleaner/NPUtil.Modules.SystemCleaner.csproj
dotnet add src/NPUtil.Modules.SystemCleaner/NPUtil.Modules.SystemCleaner.csproj reference src/NPUtil.Core/NPUtil.Core.csproj
dotnet add src/NPUtil.App/NPUtil.App.csproj reference src/NPUtil.Modules.SystemCleaner/NPUtil.Modules.SystemCleaner.csproj
```

### Krok B: Implementace třídy modulu

Vytvořte třídu `SystemCleanerModule.cs`:

```csharp
using NPUtil.Core.Logging;
using NPUtil.Core.Modules;

namespace NPUtil.Modules.SystemCleaner;

public class SystemCleanerModule : IModule
{
    public string Id => "system-cleaner";
    public string Name => "System Cleaner";
    public string Description => "Bezpečné čištění dočasných souborů a vyrovnávací paměti.";
    public string IconGlyph => "\uE74D"; // Ikona popelnice / smetáku z písma Segoe MDL2 Assets
    public int SortOrder => 2;
    public bool IsEnabled { get; set; } = true;

    private ILogger? _logger;
    private Func<object>? _viewFactory;

    public void RegisterViewFactory(Func<object> viewFactory) => _viewFactory = viewFactory;

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        _logger = (ILogger?)services.GetService(typeof(ILogger));
        _logger?.Info("SystemCleanerModule", "System Cleaner byl inicializován.");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public object CreateView()
    {
        if (_viewFactory != null) return _viewFactory();
        return "System Cleaner View";
    }
}
```

### Krok C: Vytvoření UI pohledu (WPF UserControl)

V projektu `NPUtil.App` vytvořte UserControl `SystemCleanerView.xaml` a `.cs`:

```xml
<UserControl x:Class="NPUtil.App.Views.SystemCleaner.SystemCleanerView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel Margin="24,16,24,24">
        <TextBlock Text="System Cleaner" Style="{StaticResource PageTitleStyle}" />
        <TextBlock Text="Nástroj pro bezpečné uvolnění místa na disku." Style="{StaticResource PageSubtitleStyle}" />
        
        <Button Content="Skenovat dočasné soubory" Style="{StaticResource AccentButtonStyle}" Width="200" HorizontalAlignment="Left" />
    </StackPanel>
</UserControl>
```

### Krok D: Registrace modulu v `MainWindow.xaml.cs`

V metodě `MainWindow_Loaded` jednoduše zaregistrujte modul do `ModuleRegistry`:

```csharp
// Přidání nového modulu
var cleanerModule = new SystemCleanerModule();
cleanerModule.RegisterViewFactory(() => new SystemCleanerView(cleanerModule));
await cleanerModule.InitializeAsync(serviceProvider);
_moduleRegistry.Register(cleanerModule);
```

Modul se automaticky zařadí do navigačního panelu a jeho životní cyklus bude řízen jádrem aplikace.

---

## 3. Zásady pro autory modulů

1. **Izolace logiky**: Každý modul má mít vlastní podadresáře pro logiku, modely a persistence.
2. **Žádné přímé vazby mezi moduly**: Modul `SystemCleaner` nesmí odkazovat na `FileOrganizer`. Pro komunikaci používejte sdílené služby z `NPUtil.Core`.
3. **Sdílený design**: Používejte styly z `Styles.xaml` a štětce z `LightTheme.xaml` / `DarkTheme.xaml` (`{DynamicResource SurfaceBackgroundBrush}` apod.), aby byl vzhled všech modulů 100% konzistentní.
4. **Respekt k baterii a prostředkům**: Před spuštěním náročných úloh na pozadí ověřte `_hardwareService.IsRunningOnBattery()`.
