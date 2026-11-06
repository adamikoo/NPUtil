# NPUtil — Modulární desktopová sada nástrojů pro Windows

**NPUtil** je lehká, modulární desktopová aplikace pro systém Windows 11 (ARM64 a x64), navržená jako osobní sada užitečných nástrojů. 

Aplikace je od základu koncipována jako **rozšiřitelná platforma**. Jejím prvním plně funkčním modulem je **Intelligent File Organizer** (Inteligentní organizátor souborů), který automaticky třídí a udržuje složku *Stažené soubory* (Downloads) na základě sémantického porozumění obsahu, pravidel a lokálních AI modelů.

---

## 1. Klíčové vlastnosti

- **Nativní podpora Windows 11 ARM64**: Běží nativně na procesorech Qualcomm Snapdragon X Elite (ASUS Vivobook S15 apod.) bez emulace.
- **Akcelerace NPU přes Qualcomm QNN**: Připraveno pro inferenci na Qualcomm Hexagon NPU prostřednictvím ONNX Runtime QNN Execution Provideru s automatickým plynulým přechodem (fallbackem) na vektorizovaný CPU (ARM64 NEON).
- **100% lokální zpracování (Privacy-First)**: Žádné cloudové API, telemetrie ani odesílání dat. Všechny extrakce textu, indexování a vnoření (vektory) probíhají výhradně lokálně.
- **Bezpečnost souborů a plná reverzibilita (Undo)**:
  - Žádné přepisování existujících souborů (detekce kolizí s bezpečným číslováním `soubor (1).ext`).
  - Podpora jednotlivého i hromadného vrácení operací (Undo) z persistentní SQLite databáze.
  - Zachování časových razítek a metadat souborů.
  - Kontrola stability stahování (ignorování `.crdownload`, `.part`, `.tmp`).
- **Nativní a střídmý Windows vzhled**:
  - Respektuje pravidla Windows 11 Fluent UI (světlý i tmavý režim).
  - Žádné umělé neonové přechody, falešné kontrolky ani přebublané karty. Čistý, funkční a přehledný nástroj.
- **Lokální fulltextové a sémantické vyhledávání**: SQLite FTS5 s odstraněním diakritiky v kombinaci s kosinovou podobností vektorů.

---

## 2. Architektura aplikace

NPUtil se dělí na **Core** (jádro) a **nezávislé moduly**:

```
NPUtil/
├── src/
│   ├── NPUtil.Core/                   # Jádro aplikace
│   │   ├── Hardware/                  # Diagnostika ARM64, Snapdragon X Elite, NPU, baterie
│   │   ├── Logging/                   # Strukturované logování do souboru i paměti
│   │   ├── Modules/                   # Rozhraní IModule, ModuleRegistry
│   │   ├── Services/                  # Notifikace a koordinace úloh
│   │   └── Settings/                  # Globální nastavení a JSON persistence
│   ├── NPUtil.Modules.FileOrganizer/  # První modul: Inteligentní organizátor souborů
│   │   ├── Classification/            # Hybridní klasifikátor, QNN detektor, ONNX embedding engine, paměť preferencí
│   │   ├── Core/                      # OrganizerEngine, dynamická taxonomie složek (TaxonomyManager)
│   │   ├── Extraction/                # Extrakce z PDF (PdfPig), DOCX, XLSX, PPTX, TXT, kódů, obrázků
│   │   ├── FileOperations/            # Bezpečný přesun (SafeFileMover), UndoManager, SQLite historie
│   │   ├── Models/                    # Modely a datové entity
│   │   ├── Monitoring/                # Sledování složky Downloads (DownloadsWatcher) a stabilita souborů
│   │   └── Search/                    # Lokální vyhledávač (SQLite FTS5 + sémantická podobnost)
│   └── NPUtil.App/                    # Nativní WPF desktopové uživatelské rozhraní
│       ├── Assets/                    # Ikona aplikace (nputil.ico)
│       ├── Theme/                     # Světlý / tmavý režim, nativní ovládací prvky
│       └── Views/                     # Jednotlivé obrazovky modulu a diagnostiky
├── tests/
│   └── NPUtil.Tests/                  # Sada 19 automatických testů (extrakce, klasifikace, integrita, undo)
├── publish/
│   └── win-arm64/                     # Výsledné publikované ARM64 nativní binárky
├── build.ps1                          # Skript pro kompletní sestavení, otestování a publikaci
└── run.bat                            # Skript pro okamžité spuštění aplikace
```

---

## 3. Rychlý start

### Požadavky
- Windows 11 (doporučeno ARM64, podporováno i x64)
- .NET 9 Runtime nebo SDK (SDK je nainstalováno v `C:\Users\Asus\.dotnet`)

### Spuštění aplikace
Jednoduše poklepejte na `run.bat` v kořenovém adresáři, nebo spusťte přímo:
```powershell
.\publish\win-arm64\NPUtil.exe
```

### Sestavení ze zdrojových kódů a spuštění testů
V PowerShellu spusťte automatizovaný skript:
```powershell
.\build.ps1
```
Tento skript:
1. Sestaví celou solution v konfiguraci Release.
2. Spustí 19 automatických unit a integračních testů.
3. Vygeneruje nativní sestavení do složky `publish\win-arm64\`.

---

## 4. Modul: Inteligentní organizátor souborů

Modul nabízí 6 přehledných zobrazení:

1. **Přehled (Overview)**:
   - Stav sledování (Aktivní / Pozastaveno) a tlačítko *Spustit skenování*.
   - Kompaktní metriky: počet indexovaných souborů, zorganizovaných souborů, fronta k revizi, vrácené operace.
   - Stav AI akcelerace (Qualcomm Hexagon NPU / CPU) a správa stažení neuronového modelu.
   - Poslední aktivita přesunů.
2. **Fronta k revizi (Review Queue)**:
   - Seznam souborů čekajících na schválení s navrženou cílovou složkou, mírou spolehlivosti a vysvětlením.
   - Možnost schválit jedním kliknutím, zamítnout, nebo vybrat alternativní cílovou kategorii.
   - Tlačítko pro hromadné schválení všech položek.
3. **Průzkumník složek (Folder Explorer)**:
   - Stromová hierarchie kategorií (`Education\Languages\English`, `Finance\Invoices\2026`, `Programming\Projects` atd.).
   - Editace názvů, sémantických popisů (které AI model používá pro párování), klíčových slov a přípon.
   - Možnost vyloučit složku z automatizace nebo otevřít složku v Průzkumníku Windows.
4. **Vyhledávání (Search)**:
   - Okamžité vyhledávání v reálném čase podle názvu, kategorie, přípony i vytěženého obsahu dokumentů.
   - Sémantické vyhledávání: dotaz např. *"Cambridge English"* najde relevantní materiály (např. `c1_advanced_handbook.pdf`), i když se slova neshodují přesně.
5. **Historie a Undo (Activity)**:
   - Kompletní auditní stopa každé provedené operace.
   - Tlačítko **Vrátit přesun (Undo)** u každého souboru i možnost vrátit celou dávku (Undo Batch).
6. **Nastavení modulu (Settings)**:
   - Volba sledované složky (výchozí: systémová složka *Stažené soubory* zjištěná přes Windows Known Folders API).
   - Režimy organizace:
     - *Revize všeho (výchozí, doporučeno)* — Žádný soubor se nepřesune bez vašeho schválení.
     - *Inteligentní automatika* — Soubory s vysokou jistotou (např. >75 %) přesune rovnou, sporné zařadí do fronty.
     - *Plně automatický* — Přesouvá automaticky podle nejvyšší shody.
   - Nastavení prahu spolehlivosti a chování při napájení z baterie.

---

## 5. Lokální AI a akcelerace na Qualcomm Snapdragon NPU

### Podpora hardwaru
Aplikace obsahuje komponentu `QnnDetector`, která prověřuje dostupnost knihoven `QnnHtp.dll` a `onnxruntime_providers_qnn.dll`.
Při inicializaci provede zkušební inferenci. Pokud je NPU dostupné, aktivuje se Hexagon HTP backend. V případě jakékoli nekompatibility operátorů nebo nedostupnosti ovladače se aplikace automaticky a bezpečně přepne na vektorizovaný běh na CPU (ARM64 NEON).

### Dvojúrovňový inferenční systém
1. **Rychlý vestavěný vícejazyčný vektorizér**:
   - Funguje okamžitě offline bez nutnosti stahovat externí modely.
   - Mapuje české i anglické texty, n-gramy a klíčová slova do L2-normalizovaného 384-dimenzionálního prostoru.
2. **Kvantizovaný neuronový model (ONNX)**:
   - Model `paraphrase-multilingual-MiniLM-L12-v2` (~112 MB) pod licencí Apache 2.0.
   - Lze stáhnout přímo z rozhraní aplikace jediným kliknutím.
   - Zajišťuje špičkovou sémantickou přesnost pro složité české i anglické dokumenty.
