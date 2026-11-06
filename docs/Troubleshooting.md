# Řešení potíží (Troubleshooting Guide)

Tento průvodce pomáhá vyřešit běžné situace a otázky ohledně chování aplikace **NPUtil**.

---

## 1. Kde jsou uložena data, konfigurace a logy?

Všechna lokální data aplikace jsou uložena ve standardní složce `%LOCALAPPDATA%\NPUtil`:

| Cesta | Účel |
|---|---|
| `%LOCALAPPDATA%\NPUtil\Config\settings.json` | Globální předvolby (téma, spouštění po startu, baterie) |
| `%LOCALAPPDATA%\NPUtil\Config\taxonomy.json` | Definice kategorií, klíčových slov a popisů pro AI |
| `%LOCALAPPDATA%\NPUtil\Data\history.db` | SQLite databáze historie přesunů pro bezpečné Undo |
| `%LOCALAPPDATA%\NPUtil\Data\search_index.db` | SQLite FTS5 index pro fulltextové vyhledávání |
| `%LOCALAPPDATA%\NPUtil\Data\embeddings_cache.db` | Vyrovnávací paměť vektorů (šetří energii a čas) |
| `%LOCALAPPDATA%\NPUtil\Data\user_preferences.db` | Naučené předvolby z ručních úprav uživatele |
| `%LOCALAPPDATA%\NPUtil\Logs\nputil.log` | Textový soubor s detailními logy aplikace |
| `%LOCALAPPDATA%\NPUtil\Models\` | Stažený neuronový ONNX model (~112 MB) |

---

## 2. Soubory ve složce Stažené soubory se nepřesouvají okamžitě

**Důvod A: Režim "Revize všeho" (výchozí)**
Aplikace je ve výchozím nastavení záměrně nastavena na bezpečný režim *Revize všeho (Review Everything)*. Soubory se tedy nepřesouvají samy bez vašeho vědomí, ale čekají ve **Frontě k revizi (Review Queue)**, kde je můžete jedním kliknutím schválit nebo hromadně potvrdit tlačítkem *Schválit vše*.
- *Řešení*: Pokud si přejete automatické třídění, přepněte v *Nastavení modulu* na *Inteligentní automatika* nebo *Plně automatický*.

**Důvod B: Soubor se stále stahuje v prohlížeči**
NPUtil kontroluje stabilitu souboru. Pokud má soubor příponu `.crdownload`, `.part`, `.tmp` nebo je zamčený běžícím stahováním, NPUtil počká, dokud prohlížeč soubor nedokončí a neuvolní zámek zápisu.

**Důvod C: Sledování je pozastaveno**
Zkontrolujte na záložce *Přehled*, zda je stav sledování zelený (*Stav: Aktivní*). Pokud je pozastaven, klikněte na *Obnovit sledování*.

---

## 3. Akcelerace Qualcomm Hexagon NPU

### Jak poznám, zda běží NPU nebo CPU?
V aplikaci přejděte na záložku **Hardware & AI Diagnostics** v levém postranním panelu:
- Položka **Aktivní AI zprostředkovatel** zobrazuje přesný backend:
  - `Qualcomm Hexagon NPU (QNN HTP)` — model je akcelerován přímo na procesoru Hexagon NPU.
  - `CPU (ARM64 Vectorized / NEON)` — inference běží na procesoru s využitím ARM64 vektorových instrukcí.
  - `Lexical-Semantic Vectorizer (Fast Built-in)` — používá se vestavěný rychlý vektorizér (před stažením neuronového modelu).

### Proč se aplikace přepnula na CPU?
Aplikace obsahuje přísný ověřovací proces:
1. Zkontroluje existenci knihoven `QnnHtp.dll` a `onnxruntime_providers_qnn.dll`.
2. Provede tzv. warm-up (zkušební běh) na NPU.
3. Pokud dojde k chybě (např. nepodporovaný operátor v modelu, nekompatibilita ovladače), **nikdy nespadne**, ale plynule přejde na ARM64 CPU. V diagnostickém logu najdete přesnou příčinu.

---

## 4. Jak vrátit nechtěný přesun (Undo)?

1. V aplikaci přejděte do modulu **File Organizer** -> záložka **Activity History**.
2. V tabulce najděte dotyčný soubor.
3. Klikněte na tlačítko **Undo Move**.
4. Soubor bude okamžitě přesunut zpět na svou původní cestu. Pokud by na původním místě mezitím vznikl nový soubor se stejným názvem, NPUtil soubor neprepíše, ale bezpečně jej pojmenuje např. `soubor (restored 1).ext`.
5. Pro vrácení celé dávky stiskněte **Undo Selected Batch**.

---

## 5. Jak resetovat nastavení do výchozího stavu?

Pokud chcete začít s čistým štítem:
1. Ukončete aplikaci NPUtil.
2. Smažte složku `%LOCALAPPDATA%\NPUtil`.
3. Při dalším spuštění si aplikace automaticky vytvoří novou čistou konfiguraci a výchozí taxonomii složek.
