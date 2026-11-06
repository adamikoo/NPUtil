# NPUtil v1.0.0 — Modular Windows 11 Utility Suite (ARM64 Native)

**NPUtil** is a lightweight, modular Windows 11 desktop utility suite powered by 100% local AI and hardware-accelerated for **Qualcomm Snapdragon X Elite** and **Qualcomm Hexagon NPU** (QNN HTP execution provider) with automatic vectorized CPU fallback.

---

## 🚀 Key Modules Included

### 1. 🔍 NPUtil Screen Lens (Google Lens Workflow)
- **Global Shortcut:** `Ctrl + Alt + L` (customizable in Settings)
- **Instant Snip & Floating Mica Card:** Drag and select any region of your screen without minimizing your workflow.
- **Type to It:** Immediate keyboard focus on an interactive prompt box. Ask questions directly about the snip (*"what can you see"*, *"explain this error"*, *"how to fix"*, *"extract table"*).
- **Intelligent Error & Code Comprehension:** Automatically detects .NET/Windows crash logs, parses offending source files, lines, and root causes, and generates concrete code fix patches.
- **1-Click Lens Chips:** Quick actions for *What can you see?*, *Explain & Fix*, *Translate (CZ ⇄ EN)*, *Extract Table*, and *Copy Clean Text / Image*.

### 2. 📁 Intelligent File Organizer
- Continuous real-time monitoring of Windows Downloads folder.
- Dynamic categorization for unclassified or recurring documents (e.g. *Zpravodaj* / periodicals).
- Full audit log, review queue, and 1-click multi-step Undo with collision-free file naming.
- Local vector + SQLite FTS5 search.

### 3. 🤖 Context Assistant (`Ctrl + Alt + A`)
- Captures active window context or selected text across any Windows app.
- Instant summarization, code explanation, key points extraction, and professional polish.
- Persistent local SQLite conversation history (`context_assistant.db`).

### 4. 📊 Smart Data Extractor
- Multi-format deterministic data extractor for PDF, DOCX, XLSX, PPTX, CSV, TXT, and scanned images.
- Specialized extraction categories: *Tables & Data*, *Invoices & Receipts*, *Lists & Records*, *Dates & Deadlines*, and *Structured JSON*.
- Export to CSV, Excel (XLSX), JSON, TXT, and Word (DOCX).

---

## 🎨 Windows 11 Native Experience
- **Fluent Design & Mica:** Follows authentic Windows 11 design guidelines, Mica backdrop, and dynamic theme switching.
- **120Hz Physics-Based Smooth Scrolling:** Frame-rate matched exponential spring damping designed specifically for high-refresh-rate displays and precision touchpads (no micro-stuttering or accelerated jumping).
- **Subtle Fluent Animations:** Cohesive slide-and-fade page entrance and tab switching animations.
- **Bilingual Support:** Stock English interface with seamless live toggle to Czech in Settings.

---

## 📦 Installation & Setup
1. Download **`NPUtil-v1.0.0-win-arm64.zip`** from Assets below.
2. Extract the archive to any folder (e.g. `C:\Program Files\NPUtil` or `C:\Users\<User>\AppData\Local\Programs\NPUtil`).
3. Run **`NPUtil.exe`** (or double-click `run.bat`).
