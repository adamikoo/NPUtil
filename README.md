<p align="center">
  <img src="assets/NPUtil_logo.png" width="180" alt="NPUtil Logo" />
</p>

<h1 align="center">NPUtil</h1>
<p align="center">
  <strong>Modular Windows 11 Utility Suite Powered by Local AI & Qualcomm Hexagon NPU</strong>
</p>

<p align="center">
  <a href="https://github.com/adamikoo/NPUtil/releases"><img src="https://img.shields.io/badge/Release-v1.0.0-0078D4.svg" alt="Release" /></a>
  <a href="#"><img src="https://img.shields.io/badge/Platform-Windows%2011%20ARM64-0078D4?logo=windows11" alt="Windows 11 ARM64" /></a>
  <a href="#"><img src="https://img.shields.io/badge/NPU%20Acceleration-Qualcomm%20Hexagon%20(QNN)-009688?logo=qualcomm" alt="Qualcomm Hexagon" /></a>
  <a href="#"><img src="https://img.shields.io/badge/Architecture-Snapdragon%20X%20Elite-FF5500" alt="Snapdragon X Elite" /></a>
  <a href="#"><img src="https://img.shields.io/badge/UI-Fluent%20Design%20(Mica)-blue" alt="Fluent Design" /></a>
  <a href="#"><img src="https://img.shields.io/badge/Tests-37%20Passed-success" alt="Tests" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-Source--Available-green" alt="License" /></a>
</p>

---

## 💡 Overview

**NPUtil** is a lightweight, modular Windows 11 utility application engineered from the ground up to make practical, native use of **100% local AI** and the **Qualcomm Hexagon NPU** on ARM64 processors (such as the Snapdragon X Elite on ASUS Vivobook S15 and Surface Laptop).

NPUtil is built as an all-in-one suite rather than a loose collection of scripts:
- **100% Local-First & Private:** No telemetry, no cloud subscriptions, no external API keys required. Everything runs locally on device.
- **Hardware-Accelerated:** Offloads neural embedding and transformer workloads to the Qualcomm Hexagon NPU via ONNX Runtime QNN Execution Provider (HTP profile), with seamless fallback to vectorized CPU (ARM64 NEON).
- **Authentic Windows 11 Experience:** Built with genuine Fluent Design, Mica backdrop, system theme synchronization, 120Hz physics-based smooth scrolling, and subtle entrance animations.

---

## 🚀 Core AI Modules

```
NPUtil Suite
 ├── 🔍 Screen Lens (Google Lens Workflow) ─── [ Ctrl + Alt + L ]
 ├── 📁 Intelligent File Organizer ─────────── [ Background Watcher ]
 ├── 🤖 Context Assistant ─────────────────── [ Ctrl + Alt + A ]
 ├── 📊 Smart Data Extractor ──────────────── [ Multi-format Parser ]
 └── 🌐 Universal OCR & Translator ─────────── [ Windows.Media.Ocr ]
```

### 1. 🔍 NPUtil Screen Lens (Google Lens Experience)
*Global Shortcut: `Ctrl + Alt + L` (Customizable in Settings)*

A system-wide visual intelligence layer that works anywhere in Windows without interrupting your active workflow:
- **Dimmed Screen Selection:** Drag and crop any window, error dialogue, code snippet, or article on your screen.
- **Floating Mica Lens Card:** Pops up directly beside your crop with zero window flicker.
- **Immediate Focus ("Type to It"):** Keyboard focus is automatically placed into the search box upon capture. Type questions directly (e.g., *"what can you see"*, *"explain this error"*, *"how to fix"*, *"extract table"*).
- **Intelligent Exception & Crash Diagnosis:** Recognizes .NET and Windows runtime crash logs (such as `NullReferenceException`), extracts the offending method, file, and line number, explains the root cause, and generates ready-to-paste code fixes.
- **1-Click Lens Chips:**
  - 🔍 **What can you see?** — Comprehensive visual summary of recognized screen elements.
  - 💡 **Explain & Fix** — Highlighted automatically when code or error logs are detected.
  - 🌐 **Translate** — Instant bilingual conversion (English ⇄ Czech).
  - 📊 **Extract Table / JSON** — Formats data rows into clean markdown or structured JSON.
  - 📄 **Copy Text / Image** — One-click clean clipboard extraction.

---

### 2. 📁 Intelligent File Organizer
*Automated Background Downloads Routing*

Monitors your Windows Downloads folder in real-time and routes incoming files using hybrid neural classification:
- **Dynamic Categorization:** If an incoming file (e.g. *Zpravodaj* / company bulletin) does not match existing standard categories, NPUtil dynamically creates an appropriate new category (`Work\Newsletters`) instead of misclassifying it.
- **Collision-Free Safety:** Employs non-destructive hashing and sequential renaming (`filename (1).pdf`) so no data is ever overwritten.
- **Multi-Level Undo:** Every operation is logged in a local SQLite audit database; revert individual or batch moves with one click.
- **Search:** Combines SQLite FTS5 unaccented full-text search with cosine vector similarity.

---

### 3. 🤖 Context Assistant
*Global Shortcut: `Ctrl + Alt + A`*

A floating assistant that understands what you are currently working on:
- **Active Window Context (Opt-In):** Detects the focused application (VS Code, browser, PDF reader, Terminal) and adapts suggested actions.
- **Selected Text Processing:** Highlight text in any application, press the shortcut, and immediately trigger:
  - Code & text explanation
  - Extractive summarization
  - Bilingual translation
  - Key points & bullet extraction
  - Professional / concise rewriting
- **Persistent History:** Automatically logs past interactions into `context_assistant.db`.

---

### 4. 📊 Smart Data Extractor
*Multi-Format Document Intelligence*

Deterministic, hardware-friendly data extraction engine with multi-format export:
- **Supported Formats:** PDF, DOCX, XLSX, PPTX, CSV, TXT, Markdown, and Scanned Images (PNG, JPG, WebP).
- **Extraction Profiles:**
  - 📋 **Tables & Data** — Tabular grids and numeric columns.
  - 🧾 **Invoices & Receipts** — Vendor, invoice ID, dates, line items, VAT, and totals.
  - 📑 **Formatted Text** — Hierarchical outline with headings preserved.
  - 📌 **Lists & Records** — Bulleted key/value pairs.
  - 📅 **Dates & Deadlines** — Chronological calendar extraction with context.
  - ⚙️ **Structured JSON** — Machine-readable structured schema.
- **Universal Export:** One-click export to **CSV**, **XLSX** (Excel), **JSON**, **TXT**, and **DOCX** (Word).

---

## ⌨️ Global Keyboard Shortcuts

| Shortcut | Action | Description |
| :--- | :--- | :--- |
| **`Ctrl + Alt + L`** | **Screen Lens** | Snip any screen region and type directly to local AI *(Customizable)* |
| **`Ctrl + Alt + A`** | **Context Assistant** | Opens floating assistant with active window or selected text context |
| **`Ctrl + Alt + O`** | **Universal OCR** | Quick screen snip for side-by-side OCR and bilingual translation |

*Shortcuts can be customized in **Settings → Windows Integration → Screen Lens Global Shortcut** (supports `Ctrl+Alt+L`, `Ctrl+Alt+O`, `Ctrl+Shift+S`, `Ctrl+Shift+L`, and `Alt+Space`).*

---

## 🛠️ Architecture & Technology Stack

```
NPUtil/
├── assets/                                  # Repository assets & logos
├── src/
│   ├── NPUtil.Core/                         # Shared system intelligence
│   │   ├── AI/                              # ModelManager, LocalInferenceEngine (NPU QNN HTP)
│   │   ├── Context/                         # ContextCaptureService, ContextAssistantHistoryDb
│   │   ├── Extraction/                      # DocumentExtractor (PDF, DOCX, XLSX, PPTX, CSV, TXT)
│   │   ├── Hardware/                        # Qualcomm Snapdragon X Elite diagnostics, power watcher
│   │   ├── Localization/                    # Live bilingual localization (English / Czech)
│   │   ├── Logging/                         # AppLogger with Dispatcher unhandled exception traps
│   │   ├── Modules/                         # IModule and ModuleRegistry
│   │   ├── OCR/                             # WindowsOcrService (Windows.Media.Ocr)
│   │   ├── Settings/                        # SettingsManager, global preferences
│   │   └── Translation/                     # LocalTranslationService (bilingual engine)
│   ├── NPUtil.Modules.FileOrganizer/        # File Organizer module
│   ├── NPUtil.Modules.ContextAssistant/     # Context Assistant module & popup window
│   ├── NPUtil.Modules.SmartDataExtractor/   # Smart Data Extractor & multi-format exporters
│   ├── NPUtil.Modules.UniversalOcr/         # Universal OCR & Screen Lens floating card
│   └── NPUtil.App/                          # WPF Fluent UI shell (Mica, custom chrome, navigation)
├── tests/
│   └── NPUtil.Tests/                        # 37 automated unit and integration tests
├── publish/
│   └── win-arm64/                           # Standalone ARM64 release binaries
├── build.ps1                                # Automated ARM64 build & test script
├── run.bat                                  # 1-click launcher
└── LICENSE                                  # Source-Available License v1.0
```

---

## 📥 Getting Started

### 1. Download Pre-Built Release
Grab the latest release from the [GitHub Releases page](https://github.com/adamikoo/NPUtil/releases):
1. Download **`NPUtil-v1.0.0-win-arm64.zip`**.
2. Extract the archive to any folder (e.g. `C:\Program Files\NPUtil` or `C:\Users\<User>\AppData\Local\Programs\NPUtil`).
3. Run **`NPUtil.exe`** (or `run.bat`).

### 2. Build from Source
Requirements:
- Windows 11 (ARM64 or x64)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

In PowerShell:
```powershell
# Clone the repository
git clone https://github.com/adamikoo/NPUtil.git
cd NPUtil

# Build, test, and publish ARM64 native binaries
.\build.ps1
```

---

## 🔒 Privacy & Hardware Optimization

- **Zero Telemetry:** NPUtil does not connect to external AI endpoints, trackers, or cloud analytics.
- **Battery-Aware Throttling:** Detects when running on battery power and dynamically dials back heavy background inference passes to preserve battery life.
- **RAM Efficiency:** ModelManager enforces memory budgets and unloads inactive ONNX sessions when not in use.

---

## 📄 License

NPUtil is licensed under the **NPUtil Source-Available License Version 1.0**. See the [LICENSE](LICENSE) file for complete terms.
