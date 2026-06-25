# SmartExtract

A Windows-native context menu extension for 7-Zip that extracts archives intelligently — no double-wrapping, no scattered files.

## The problem

Standard archive extraction has an annoying inconsistency:

- `project.zip` containing `project/...` → extract here → you get `project/project/...` (double-wrapped)
- `files.zip` containing loose files → extract here → files scatter into the current folder

You end up either manually moving folders or always extracting to a named subfolder regardless of what's inside.

## What SmartExtract does

Right-click any archive and choose **Smart Extract**. SmartExtract inspects the archive contents first, then chooses the right extraction strategy automatically:

| Archive contents | What happens |
|-----------------|--------------|
| Single root folder whose name matches the archive | Extracted directly — no double-wrapping |
| Anything else (loose files, mismatched folder name, multiple entries) | Extracted into a new folder named after the archive |

In both cases the result in your working directory is a single, cleanly named folder.

### Example

```
# Archive: my-project.zip
# Contains: my-project/src/, my-project/README.md, ...
# Result: my-project/ appears directly — no my-project/my-project/ nesting

# Archive: release.zip
# Contains: bin/, lib/, README.md (no root folder)
# Result: release/ is created and all contents go inside it
```

Compound archive extensions are handled correctly: `my-project.tar.gz` produces a folder named `my-project`.

## Requirements

- **Windows** — SmartExtract is designed exclusively for the Windows desktop environment. It integrates with the Windows shell context menu and relies on Windows-specific APIs (registry, WinForms message loop, Windows file associations). It will not build or run on Linux or macOS.
- [7-Zip](https://www.7-zip.org/) installed (any recent version; `7z.exe` and `7zG.exe` must be present)
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Desktop Runtime)

## Supported formats

`.zip` · `.7z` · `.rar` · `.gz` · `.bz2` · `.tar`

All formats supported by 7-Zip work transparently — SmartExtract delegates the actual extraction to `7zG.exe`, so password-protected archives get 7-Zip's native password dialog.

## Installation

### Option A: Windows Installer (recommended)

Download `SmartExtractSetup.exe` from the [latest release](https://github.com/mchwalek/SmartExtract/releases/latest) and run it.

The installer wizard will:
1. Let you choose between a machine-wide install (requires administrator) or a per-user install (no admin required)
2. Auto-detect your 7-Zip installation and let you confirm or correct the path
3. Register the **Smart Extract** context menu entry for all supported extensions

To uninstall, use **Settings → Apps** or **Control Panel → Programs and Features**.

### Option B: PowerShell scripts (power users / CI)

**1. Build**

```powershell
dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o build/publish/
```

**2. Install** (no administrator rights required — per-user install)

```powershell
PowerShell -ExecutionPolicy Bypass -File scripts/install.ps1
# Optional: specify 7-Zip location explicitly
PowerShell -ExecutionPolicy Bypass -File scripts/install.ps1 -SevenZipDir "C:\Program Files\7-Zip\"
```

This copies the application to `%LOCALAPPDATA%\Programs\SmartExtract\` and registers the **Smart Extract** context menu entry for all supported extensions under your user account (`HKCU`).

**3. Use**

Right-click any supported archive file in Windows Explorer → **Smart Extract**.

## Uninstall

**If installed via installer:** use **Settings → Apps → SmartExtract → Uninstall**.

**If installed via PowerShell:**

```powershell
PowerShell -ExecutionPolicy Bypass -File scripts/uninstall.ps1
```

Removes the context menu entries and the install directory. No leftovers.

## How it works

SmartExtract is a headless C# WinForms application (`WinExe`) — it shows no window of its own. When invoked:

1. **Inspect** — calls `7z.exe l -slt` to list archive contents
2. **Decide** — if there is exactly one top-level directory whose name matches the archive base name (case-insensitive), use Direct mode; otherwise use Wrapped mode
3. **Extract** — delegates to `7zG.exe` (7-Zip's GUI executable), which shows the native 7-Zip progress dialog and handles any password prompts
4. **Done** — errors are surfaced as a `MessageBox`; silent on success

7-Zip's path is discovered from SmartExtract's stored configuration (`HKCU\Software\SmartExtract\SevenZipPath`), then 7-Zip's registry key, `PATH`, and finally `C:\Program Files\7-Zip\`.

## Project structure

```
src/SmartExtract/
  Program.cs              Entry point — argument validation and orchestration
  NameHelper.cs           Archive base name extraction (strips .tar.gz etc.)
  ArchiveEntry.cs         Data record: path + directory flag
  ArchiveListParser.cs    Parses 7z.exe -slt output
  SmartExtractLogic.cs    Core Direct/Wrapped decision logic
  SevenZipLocator.cs      Finds 7-Zip installation
  ArchiveInspector.cs     Orchestrates listing and mode decision
  ExtractionRunner.cs     Invokes 7zG.exe

tests/SmartExtract.Tests/
  NameHelperTests.cs
  ArchiveListParserTests.cs
  SmartExtractLogicTests.cs
  SevenZipLocatorTests.cs
  ExtractionRunnerTests.cs

scripts/
  install.ps1             Per-user install + context menu registration
  uninstall.ps1           Cleanup

installer/
  SmartExtract.iss        Inno Setup script (produces dist/SmartExtractSetup.exe)
```

## Building and testing

```powershell
dotnet build SmartExtract.slnx
dotnet test SmartExtract.slnx -c Release
```

> **Note:** Windows Smart App Control may block Debug-configuration DLLs from running under `dotnet test`. Use `-c Release` for tests.

## Tech stack

- C# / .NET 10 (`net10.0-windows`)
- WinForms (`System.Windows.Forms`) — for `MessageBox` only; no window is shown
- Windows Registry (`Microsoft.Win32`) — for 7-Zip discovery and context menu registration
- xUnit 2.9 — unit tests
- 7-Zip (`7z.exe`, `7zG.exe`) — archive listing and extraction

## Architecture

Design decisions and implementation notes are in [ARCHITECTURE.md](ARCHITECTURE.md).
