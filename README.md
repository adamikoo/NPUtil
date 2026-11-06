# NPUtil — Modulární desktopová sada nástrojů pro Windows 11

**NPUtil** je lehká, modulární desktopová aplikace pro systém Windows 11 (nativní ARM64 a x64), navržená jako integrovaná sada praktických nástrojů s lokální umělou inteligencí.

Aplikace běží **100% lokálně** s hardwarovou akcelerací na procesorech **Qualcomm Snapdragon X Elite** s využitím **Qualcomm Hexagon NPU** (prostřednictvím ONNX Runtime QNN Execution Provideru) a plynulým fallbackem na vektorizovaný CPU (ARM64 NEON).

---

## 1. Čtyři integrované moduly

### 1. Intelligent File Organizer
Automaticky monitoruje složku *Stažené soubory* (Downloads) a na základě sémantické analýzy obsahu souborů a hybridního AI modelu organizuje dokumenty do přehledné adresářové struktury.
- **Dynamická kategorizace**: Pokud systém narazí na nespecifikovaný dokument (např. *Zpravodaj* nebo periodikum), nevloží jej chybně do financí, nýbrž dynamicky vytvoří novou vhodnou kategorii (`Work\Newsletters` apod.).
- **Plná reverzibilita (Undo)**: Bezpečné kolizní číslování souborů a možnost okamžitého vrácení jednotlivých i hromadných operací z persistentní SQLite databáze.
- **Lokální vyhledávání**: SQLite FTS5 s odstraněním diakritiky v kombinaci s kosinovou podobností vektorů.

### 2. Context Assistant (Klávesová zkratka: `Ctrl+Alt+A`)
Inteligentní asistent porozumí kontextu vaší aktuální práce napříč systémem Windows.
- **Zpracování vybraného textu**: Zvolte text v jakékoliv aplikaci a stiskněte globální zkratku. Asistent nabídne kontextové akce: vysvětlení kódu nebo textu, shrnutí, překlad, extrakci klíčových bodů či profesionální přepis.
- **Detekce aktivní aplikace (Opt-in)**: Zjišťuje aktivní okno (VS Code, prohlížeč, PDF čtečka, Průzkumník) a přizpůsobuje nabídku akcí.
- **Podpora dokumentů**: Možnost přiložit libovolný dokument (PDF, DOCX, XLSX, TXT, CSV) jako kontext.
- **Soukromí na prvním místě**: Žádné skryté snímky obrazovky ani nahrávání. Uživatel vždy vidí přesný náhled zachyceného kontextu před odesláním do lokálního inferenčního modelu.
- **Historie konverzací**: Všechny dotazy jsou lokálně uloženy v SQLite databázi (`context_assistant.db`).

### 3. Smart Data Extractor
Všestranný nástroj pro vytěžování a strukturování dat z dokumentů a souborů.
- **Podporované formáty**: PDF, DOCX, XLSX, PPTX, CSV, TXT / Markdown, naskenované obrázky (PNG, JPG, WebP).
- **Předdefinované kategorie**:
  - **Tables & Data**: Detekce tabulek, číselných řad a mřížek s dynamickým náhledem v tabulce.
  - **Formatted Text**: Zachování nadpisů, odstavců a struktury.
  - **Invoices & Receipts**: Automatická identifikace dodavatele, odběratele, čísla faktury, data vystavení/splatnosti, částky, DPH a položek.
  - **Lists & Records**: Extrakce záznamů, seznamů a klíčových dvojic.
  - **Dates & Deadlines**: Vyhledání všech termínů, schůzek a deadlinů s okolním kontextem.
  - **Structured JSON**: Konverze dokumentu do validního formátovaného JSONu.
  - **Vlastní instrukce**: Zpracování požadavků v přirozeném jazyce (např. *"Vytáhni všechny produkty a ceny"*).
- **Export**: Výstup lze uložit do formátů **CSV**, **XLSX** (nativní Excel), **JSON**, **TXT** a **DOCX** (Word).

### 4. Universal OCR, Translator & Screen Lens (Klávesová zkratka: `Ctrl+Alt+L` / `Ctrl+Alt+O`)
Systémový OCR snímač, překladač a inteligentní **Screen Lens** (ve stylu Google Lens) dostupný odkudkoliv ve Windows.
- **Plovoucí Google Lens Card (`ScreenLensWindow`)**: Po výběru oblasti obrazovky se neotevírá velké hlavní okno, nýbrž se přímo vedle výběru zobrazí elegantní plovoucí panel s Mica efektem a automaticky zacíleným textovým polem (*"Ask about this selection..."*).
- **Interaktivní dotazování ("Type to it")**: Na výběr se můžete přímo dotazovat (např. *"what can you see"*, *"explain error"*, *"how to fix"*). Pro chybové hlášky a stack trace systém automaticky detekuje typ výjimky, soubor, řádek a navrhne konkrétní opravu kódu.
- **Jednoklikové rychlé akce (Lens Chips)**:
  - 🔍 **What can you see?**: Komplexní rozbor a přehled zachyceného obsahu na obrazovce.
  - 💡 **Explain & Fix**: Automaticky zvýrazněno při detekci kódu nebo runtime výjimky.
  - 🌐 **Translate**: Okamžitý překlad mezi češtinou a angličtinou.
  - 📊 **Extract Table / Data**: Strukturování dat do tabulky či JSON formátu.
  - 📄 **Copy Text / Image**: Okamžité zkopírování vytěženého textu nebo výřezu obrázku do schránky.
- **Nativní Windows OCR**: Využívá integrovaný engine `Windows.Media.Ocr` s plnou hardwarovou akcelerací a podporou češtiny i angličtiny.
- **Provázanost modulů**: Jedním kliknutím odešlete rozpoznaný text do **Context Assistantu** nebo **Smart Data Extraktoru**.
- **Dávkové OCR**: Možnost nahrát více obrázků najednou a hromadně vytěžit text.

---

## 2. Architektura řešení

```
NPUtil/
├── src/
│   ├── NPUtil.Core/                         # Sdílená inteligence a systémové jádro
│   │   ├── AI/                              # ModelManager (správa RAM a modelů), LocalInferenceEngine
│   │   ├── Context/                         # ContextCaptureService, ContextAssistantHistoryDb
│   │   ├── Extraction/                      # DocumentExtractor (PDF, DOCX, XLSX, PPTX, CSV, TXT)
│   │   ├── Hardware/                        # Diagnostika Qualcomm Snapdragon X Elite, NPU, baterie
│   │   ├── Localization/                    # Lokalizační engine (angličtina výchozí, živý přepínač do češtiny)
│   │   ├── Logging/                         # Logování (AppLogger)
│   │   ├── Modules/                         # IModule a ModuleRegistry
│   │   ├── OCR/                             # WindowsOcrService (Windows.Media.Ocr)
│   │   └── Translation/                     # LocalTranslationService (bilingvní česko-anglický engine)
│   ├── NPUtil.Modules.FileOrganizer/        # Modul 0: Organizace souborů
│   ├── NPUtil.Modules.ContextAssistant/     # Modul 1: Kontextový asistent
│   │   ├── Core/                            # ContextAssistantEngine
│   │   └── UI/                              # ContextAssistantMainView, ContextAssistantPopupWindow
│   ├── NPUtil.Modules.SmartDataExtractor/   # Modul 2: Chytrý extraktor dat
│   │   ├── Engine/                          # SmartDataExtractorEngine
│   │   ├── Export/                          # DataExporter (CSV, XLSX, JSON, TXT, DOCX)
│   │   └── UI/                              # SmartDataExtractorMainView
│   ├── NPUtil.Modules.UniversalOcr/         # Modul 3: Univerzální OCR a překladač
│   │   ├── Core/                            # UniversalOcrEngine
│   │   └── UI/                              # UniversalOcrMainView, ScreenSnipOverlayWindow
│   └── NPUtil.App/                          # Hlavní aplikace WPF Fluent UI
├── tests/
│   └── NPUtil.Tests/                        # 34 automatizovaných unit a integračních testů
├── publish/
│   └── win-arm64/                           # Publikované nativní ARM64 spustitelné soubory
├── build.ps1                                # Skript pro sestavení, otestování a publikaci
└── run.bat                                  # Skript pro okamžité spuštění
```

---

## 3. Rychlé spuštění a ovládání

### Spuštění aplikace
Poklepejte na `run.bat` v kořenovém adresáři, nebo spusťte přímo:
```powershell
.\publish\win-arm64\NPUtil.exe
```

### Globální klávesové zkratky
- **`Ctrl+Alt+A`**: Otevře plovoucí okno **Context Assistantu** se zachyceným textem z aktuálního okna.
- **`Ctrl+Alt+L`**: Spustí **NPUtil Screen Lens** (výběr oblasti obrazovky a plovoucí Google Lens AI karta). Lze přizpůsobit v Nastavení.
- **`Ctrl+Alt+O`**: Spustí zatmavení obrazovky pro okamžitý **OCR výřez** libovolného textu na monitoru.

### Sestavení a testy
V PowerShellu spusťte:
```powershell
.\build.ps1
```
Spustí kompilaci v konfiguraci Release pro ARM64, provede všech **37 automatizovaných testů** a publikuje samostatnou aplikaci do `publish/win-arm64/NPUtil.exe`.
Tento skript sestaví všechny projekty, provede všech **34 automatizovaných testů** a publikuje optimalizované binárky do `publish\win-arm64\`.

---

## 4. Licence a podmínky použití

Projekt **NPUtil** podléhá licenci **NPUtil Source-Available Non-Commercial License (Version 1.0)**. Kompletní znění licence naleznete v souboru [`LICENSE`](LICENSE).

### Co je povoleno:
- Prohlížet a studovat kompletní zdrojové kódy.
- Forkovat repozitář a vytvářet vlastní odvozené verze.
- Modifikovat, rozšiřovat a přepisovat libovolnou část kódu.
- Využívat software a jeho modifikace pro osobní, vzdělávací a nekomerční účely.
- Bezplatně šířit zdrojové kódy i sestavené binární verze za předpokladu zachování informací o původních autorech.

### Co je zakázáno:
- Prodávat NPUtil nebo jeho odvozeniny za peníze.
- Monetizovat software (předplatné, placený hosting, placené pluginy či placené balíčky).
- Vydávat dílo za svůj vlastní původní produkt bez uvedení původního projektu.
