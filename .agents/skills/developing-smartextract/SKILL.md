---
name: developing-smartextract
description: Use when adding features, modifying extraction logic, changing archive format support, updating the install script, or working with the Windows shell integration in the SmartExtract project.
metadata:
  internal: true
---

# Developing SmartExtract

## Overview

SmartExtract is a **Windows-only** WinExe (no console window) that adds a "Smart Extract" right-click context menu entry for archive files. It calls `7z.exe` to inspect the archive, decides on the extraction strategy, then delegates to `7zG.exe` for extraction with its native progress dialog.

**Windows is a hard requirement** — the project uses `net10.0-windows`, `System.Windows.Forms` (MessageBox), `Microsoft.Win32` (registry), and the Windows shell context menu. It will not build or run on Linux/macOS.

## Build and Publish

```powershell
dotnet build SmartExtract.slnx               # debug build
dotnet build SmartExtract.slnx -c Release    # release build

# Produce deployable artifacts into install/
dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o install/
```

Publish output: `SmartExtract.exe`, `SmartExtract.dll`, `SmartExtract.deps.json`, `SmartExtract.runtimeconfig.json` — all four files are required at runtime.

## Project Structure

```
src/SmartExtract/
  Program.cs              Entry point — arg validation, error MessageBox, orchestration
  NameHelper.cs           Base name extraction (strips .tar.gz, .tar.bz2, .tar.xz, .tar.zst then last ext)
  ArchiveEntry.cs         Record: Path (string), IsDirectory (bool)
  ArchiveListParser.cs    Parses 7z.exe l -slt stdout into List<ArchiveEntry>
  SmartExtractLogic.cs    Returns Direct or Wrapped based on top-level entries
  SevenZipLocator.cs      Finds 7-Zip: registry (HKCU\Software\7-Zip) → PATH → fallback
  ArchiveInspector.cs     Calls 7z.exe, wires parser + logic, returns ExtractionMode
  ExtractionRunner.cs     Invokes 7zG.exe with UseShellExecute=true

tests/SmartExtract.Tests/   # One file per source class (see testing-smartextract skill)

install/
  install.ps1             Per-user install — no admin. Copies all publish artifacts, writes registry
  uninstall.ps1           Removes registry keys and install dir
```

## Core Logic

### Smart extraction decision (`SmartExtractLogic.Determine`)

- **Direct**: exactly one top-level entry, it is a directory, its name equals archive base name (OrdinalIgnoreCase)
- **Wrapped**: everything else — flat archives, mismatched folder names, multiple root entries

Top-level = `ArchiveEntry.Path` contains no `/` or `\`.

### Archive listing parser (`ArchiveListParser.Parse`)

7z `-slt` output quirk: multiple entries may appear in a single `----------` block, separated by blank lines (observed with 7-Zip 26 ZIP archives). The parser splits on `----------` first, then on `\n\n` within each block. Only emits entries that have both `Path = ` and `Folder = ` lines (filters the archive-level metadata block).

### 7zG.exe invocation (`ExtractionRunner.Extract`)

Must use `UseShellExecute = true`. Using `false` prevents 7zG.exe from initializing its message loop, causing `WaitForExit()` to return before extraction completes.

### Context menu registration (`install.ps1`)

Registered under `HKCU\Software\Classes\SystemFileAssociations\.<ext>\shell\SmartExtract` — per-user, no admin required. Supported extensions: `.zip`, `.7z`, `.rar`, `.gz`, `.bz2`, `.tar`.

## Code Conventions

- Target framework: `net10.0-windows` — required for WinForms and registry APIs
- Output type: `WinExe` — suppresses console window when launched from Explorer
- All classes are `public static` — no instances, no DI
- Errors surface as `MessageBox.Show(...)` with title `"SmartExtract"`; silent on success
- Namespaces: `SmartExtract` (source), `SmartExtract.Tests` (tests)

## Adding a New Archive Format

1. Add the extension to `$Extensions` in `install/install.ps1` and `install/uninstall.ps1`
2. No code changes needed — 7-Zip handles the actual format support

## Changing the Extraction Logic

`SmartExtractLogic.cs` is the only file to touch. It is pure (no I/O), fully unit-tested in `SmartExtractLogicTests.cs`. Add a test case first.
