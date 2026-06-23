# AGENTS.md

## Project Overview

SmartExtract is a **Windows-native** context menu extension for 7-Zip. It adds a "Smart Extract" right-click action to archive files that automatically chooses the right extraction strategy: if the archive has a single root folder matching the archive name it extracts directly (no double-wrapping); otherwise it creates a containing folder. C# .NET 10, WinExe (no console window), per-user install, no admin required.

**Windows is a hard requirement.** The project targets `net10.0-windows`, uses `System.Windows.Forms` (MessageBox), `Microsoft.Win32` (registry for 7-Zip discovery and context menu registration), and the Windows shell file association system. It will not build or run on Linux or macOS.

## Project Skills

This repo includes project-level agent skills in `.agents/skills/`:

| Skill                       | When to use                                                                                       |
|-----------------------------|---------------------------------------------------------------------------------------------------|
| **developing-smartextract** | Adding features, modifying extraction logic, changing format support, updating install scripts.   |
| **testing-smartextract**    | Running tests, adding test coverage, debugging failures, running smoke tests.                     |

Use these skills instead of the sections below — they have full step-by-step instructions.

## Tech Stack

| Component         | Purpose                                                                 |
|-------------------|-------------------------------------------------------------------------|
| C# / .NET 10      | `net10.0-windows`, `WinExe` output type, `UseWindowsForms`             |
| 7z.exe            | Archive listing (`l -slt`); stdout parsed to determine extraction mode  |
| 7zG.exe           | Extraction; shows native 7-Zip progress dialog and password prompts     |
| xUnit 2.9         | Unit tests (`dotnet test -c Release` — Debug blocked by Smart App Control) |
| PowerShell 5.1    | Install/uninstall scripts; per-user, no admin                           |
| Windows Registry  | 7-Zip path discovery; `HKCU\Software\Classes\SystemFileAssociations`   |

## Project Structure

```
src/SmartExtract/
  Program.cs              Entry point — arg validation, MessageBox errors, orchestration
  NameHelper.cs           Base name extraction (compound extension stripping)
  ArchiveEntry.cs         Record: Path, IsDirectory
  ArchiveListParser.cs    Parses 7z.exe l -slt output
  SmartExtractLogic.cs    Direct vs Wrapped decision (pure logic, no I/O)
  SevenZipLocator.cs      Finds 7-Zip installation directory
  ArchiveInspector.cs     Orchestrates listing and mode decision
  ExtractionRunner.cs     Invokes 7zG.exe (UseShellExecute=true required)

tests/SmartExtract.Tests/
  NameHelperTests.cs
  ArchiveListParserTests.cs
  SmartExtractLogicTests.cs
  SevenZipLocatorTests.cs    # integration — requires 7-Zip installed
  ExtractionRunnerTests.cs

scripts/
  install.ps1              Per-user install (-PublishDir, -SevenZipDir params)
  uninstall.ps1            Removes registry entries and install dir

installer/
  SmartExtract.iss         Inno Setup script

build/
  publish/                 dotnet publish output (gitignored)

dist/
  SmartExtractSetup.exe    Produced by Inno Setup (gitignored)

.github/
  workflows/
    release.yml            Tag push -> draft GitHub Release

.agents/skills/
  developing-smartextract/ # Project skill: development guide
  testing-smartextract/    # Project skill: test guide
```

## Development Commands

```powershell
dotnet build SmartExtract.slnx                              # build
dotnet build SmartExtract.slnx -c Release                  # release build
dotnet test SmartExtract.slnx -c Release                   # all 36 tests (must be Release)
dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o build/publish/  # publish artifacts
PowerShell -ExecutionPolicy Bypass -File scripts/install.ps1    # install context menu
PowerShell -ExecutionPolicy Bypass -File scripts/uninstall.ps1  # uninstall
# Build installer (requires Inno Setup 6):
& "C:\Program Files (x86)\Inno Setup 6\iscc.exe" installer\SmartExtract.iss /DAppVersion=0.0.0
```

## Code Conventions

- All classes are `public static` — stateless, no DI
- Error handling: `MessageBox.Show(message, "SmartExtract", ...)` — never write to stdout/stderr
- Namespaces: `SmartExtract` (source), `SmartExtract.Tests` (tests)
- No console output from the application — it is a WinExe with no terminal
- `7zG.exe` must be launched with `UseShellExecute = true` — `false` causes WaitForExit to return early

## Key Constraints

- **Tests require `-c Release`** — Windows Smart App Control blocks Debug DLLs. Always run `dotnet test ... -c Release`.
- **SevenZipLocatorTests are integration tests** — they read the real Windows registry and verify 7-Zip files exist. 7-Zip must be installed on the test machine.
- **No unit tests for `ArchiveInspector` or `Program`** — both wrap live process calls. Verified by smoke tests (see testing-smartextract skill).
- **Install produces 4 required files** — `.exe`, `.dll`, `.deps.json`, `.runtimeconfig.json`. The install script copies all non-`.ps1`/non-`.pdb` files from the publish directory.
