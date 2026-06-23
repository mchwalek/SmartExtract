# Windows Installer & GitHub Release Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Key reference files:**
> - Spec: `docs/superpowers/specs/2026-06-23-windows-installer-design.md`
> - Progress tracker: `PROGRESS.md`
> - Decisions log: `DECISIONS.md`
>
> **After completing your assigned task:** update `PROGRESS.md` with your task row and commit hash, then report back to the orchestrator.

**Goal:** Replace the PowerShell-only install approach with an Inno Setup Windows installer, wire `SevenZipLocator` to read a user-configured 7-Zip path from the registry, and publish draft GitHub Releases automatically on version tag pushes.

**Architecture:** The repo is restructured so `scripts/` holds the PowerShell helpers, `build/publish/` holds dotnet publish output (gitignored), `installer/` holds the Inno Setup `.iss` script, and `dist/` holds the generated `SmartExtractSetup.exe` (gitignored). `SevenZipLocator` gains a new first-priority lookup (`HKCU\Software\SmartExtract\SevenZipPath`) written by both the installer wizard and `install.ps1 -SevenZipDir`. A GitHub Actions workflow triggers on `v*.*.*` tags, runs tests, publishes, builds the installer, and creates a draft release.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`), xUnit 2.9, Inno Setup 6, Pascal (Inno Setup scripting), PowerShell 5.1, GitHub Actions (`windows-latest`), `softprops/action-gh-release@v2`

## Global Constraints

- All `dotnet test` commands must use `-c Release` — Windows Smart App Control blocks Debug DLLs
- Target framework: `net10.0-windows`
- Registry writes always use `HKCU` (per-user); never `HKLM` for SmartExtract config
- Supported archive extensions: `.zip`, `.7z`, `.rar`, `.gz`, `.bz2`, `.tar`
- 7-Zip path registry key: `HKCU\Software\SmartExtract` value name `SevenZipPath` (REG_SZ, trailing backslash)
- Context menu registry path: `HKCU\Software\Classes\SystemFileAssociations\<ext>\shell\SmartExtract`
- `install.ps1` default publish source: `build\publish\` (relative to repo root)
- Inno Setup output: `dist\SmartExtractSetup.exe`
- GitHub repo: `https://github.com/mchwalek/SmartExtract`

---

## Execution Waves

Tasks are grouped into waves. Within a wave, tasks are independent and can be dispatched to parallel subagents. A wave must fully complete before the next begins.

| Wave | Tasks | Notes |
|------|-------|-------|
| 1 | Task 1 | Sequential — foundation for all others |
| 2 | Tasks 2, 3, 4 | Parallel — independent of each other |
| 3 | Task 5 | Sequential — depends on Wave 2 output |
| 4 | Tasks 6, 7 | Parallel — independent of each other |

---

## Task 1: Repo Restructure

**Files:**
- Rename: `install/` → `scripts/`
- Modify: `.gitignore`
- Modify: `PROGRESS.md`

**Interfaces:**
- Produces: `scripts/install.ps1`, `scripts/uninstall.ps1` at new paths; `build/`, `dist/`, `installer/Output/` excluded from git

- [ ] **Step 1: Rename `install/` to `scripts/`**

```powershell
git mv install scripts
```

Expected output: no error. `git status` should show renames of `install/install.ps1` → `scripts/install.ps1` and `install/uninstall.ps1` → `scripts/uninstall.ps1`.

- [ ] **Step 2: Add build artifact paths to `.gitignore`**

Open `.gitignore` (currently contains `bin/`, `obj/`, `*.user`, `.vs/`) and append:

```
build/
dist/
installer/Output/
```

- [ ] **Step 3: Verify no tracked files were accidentally lost**

```powershell
git status
```

Expected: only renames and `.gitignore` modification shown. No deletions.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "refactor: rename install/ to scripts/, add build artifacts to .gitignore"
```

---

## Task 2: Update `scripts/install.ps1`

**Files:**
- Modify: `scripts/install.ps1`

**Interfaces:**
- Consumes: nothing from other tasks
- Produces: `scripts/install.ps1` that accepts `-PublishDir` (default: `$PSScriptRoot\..\build\publish`) and `-SevenZipDir` (optional); writes `SevenZipPath` to `HKCU\Software\SmartExtract` when `-SevenZipDir` is supplied

- [ ] **Step 1: Rewrite `scripts/install.ps1`**

Replace the entire file content with:

```powershell
# SmartExtract Install Script
# Per-user - no administrator privileges required.
#
# Parameters:
#   -PublishDir  Path to the dotnet publish output directory.
#                Defaults to <repo-root>\build\publish\
#                Build first: dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o build/publish/
#   -SevenZipDir Optional path to the 7-Zip installation directory (e.g. "C:\Program Files\7-Zip\").
#                When provided, written to HKCU\Software\SmartExtract\SevenZipPath so SmartExtract
#                can find 7-Zip without relying on auto-detection.
param(
    [string]$PublishDir  = (Join-Path $PSScriptRoot "..\build\publish"),
    [string]$SevenZipDir = ""
)

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\SmartExtract"
$ExeName    = "SmartExtract.exe"
$MenuLabel  = "Smart Extract"
$MenuKey    = "SmartExtract"
$Extensions = @(".zip", ".7z", ".rar", ".gz", ".bz2", ".tar")

$PublishDir = (Resolve-Path $PublishDir -ErrorAction SilentlyContinue)?.Path ?? $PublishDir
$SourceExe  = Join-Path $PublishDir $ExeName
$ExePath    = Join-Path $InstallDir $ExeName

if (-not (Test-Path $SourceExe)) {
    Write-Error "Cannot find '$ExeName' at: $PublishDir`nRun first: dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o build/publish/"
    exit 1
}

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Write-Host "Created: $InstallDir"
}

try {
    $files = Get-ChildItem -Path $PublishDir -File |
             Where-Object { $_.Extension -notin @('.ps1', '.pdb') }
    foreach ($file in $files) {
        Copy-Item -Path $file.FullName -Destination (Join-Path $InstallDir $file.Name) -Force -ErrorAction Stop
    }
    Write-Host "Installed $($files.Count) file(s) to: $InstallDir"
} catch {
    Write-Error "Failed to copy files to '$InstallDir': $_"
    exit 1
}

if ($SevenZipDir -ne "") {
    $regPath = "HKCU:\Software\SmartExtract"
    if (-not (Test-Path $regPath)) {
        New-Item -Path $regPath -Force | Out-Null
    }
    Set-ItemProperty -Path $regPath -Name "SevenZipPath" -Value $SevenZipDir
    Write-Host "Stored 7-Zip path: $SevenZipDir"
}

foreach ($ext in $Extensions) {
    $shellKey = "HKCU:\Software\Classes\SystemFileAssociations\$ext\shell\$MenuKey"
    $cmdKey   = "$shellKey\command"

    New-Item  -Path $shellKey -Force | Out-Null
    Set-ItemProperty -Path $shellKey -Name "(Default)" -Value $MenuLabel
    Set-ItemProperty -Path $shellKey -Name "Icon"      -Value "`"$ExePath`",0"

    New-Item  -Path $cmdKey -Force | Out-Null
    Set-ItemProperty -Path $cmdKey -Name "(Default)" -Value "`"$ExePath`" `"%1`""

    Write-Host "Registered: $ext"
}

Write-Host ""
Write-Host "Installation complete. Right-click any archive and choose '$MenuLabel'."
```

- [ ] **Step 2: Commit**

```powershell
git add scripts/install.ps1
git commit -m "feat: update install.ps1 - add -PublishDir and -SevenZipDir params"
```

---

## Task 3: SevenZipLocator — Add SmartExtract Config Lookup

**Files:**
- Modify: `src/SmartExtract/SevenZipLocator.cs`
- Modify: `tests/SmartExtract.Tests/SevenZipLocatorTests.cs`

**Interfaces:**
- Consumes: nothing from other tasks
- Produces:
  - `SevenZipLocator.LocateFromSmartExtractConfig(): string?` — reads `HKCU\Software\SmartExtract\SevenZipPath`
  - `SevenZipLocator.Locate()` updated to call `LocateFromSmartExtractConfig()` first

- [ ] **Step 1: Write the failing test**

Add to `tests/SmartExtract.Tests/SevenZipLocatorTests.cs` (append before the closing `}`):

```csharp
    [Xunit.Fact]
    public void LocateFromSmartExtractConfig_ReturnsStoredPath_WhenRegistryKeyExists()
    {
        const string testPath = @"C:\Fake\SevenZip\";
        const string regKey = @"Software\SmartExtract";

        // Arrange: write the test value
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regKey);
        key.SetValue("SevenZipPath", testPath);

        try
        {
            // Act
            var result = SevenZipLocator.LocateFromSmartExtractConfig();

            // Assert
            Xunit.Assert.Equal(testPath, result);
        }
        finally
        {
            // Cleanup: remove the test registry key
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(regKey, throwOnMissingSubKey: false);
        }
    }

    [Xunit.Fact]
    public void LocateFromSmartExtractConfig_ReturnsNull_WhenRegistryKeyAbsent()
    {
        // Ensure the key does not exist
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\SmartExtract", throwOnMissingSubKey: false);

        var result = SevenZipLocator.LocateFromSmartExtractConfig();

        Xunit.Assert.Null(result);
    }

    [Xunit.Fact]
    public void Locate_PrefersSmartExtractConfigOverSevenZipRegistry()
    {
        const string testPath = @"C:\Fake\SevenZip\";
        const string regKey = @"Software\SmartExtract";

        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regKey);
        key.SetValue("SevenZipPath", testPath);

        try
        {
            var result = SevenZipLocator.Locate();
            Xunit.Assert.Equal(testPath, result);
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(regKey, throwOnMissingSubKey: false);
        }
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test SmartExtract.slnx -c Release --filter "LocateFromSmartExtractConfig|Locate_PrefersSmartExtract"
```

Expected: 3 failures — `LocateFromSmartExtractConfig` does not exist yet.

- [ ] **Step 3: Implement `LocateFromSmartExtractConfig` and update `Locate`**

Replace `src/SmartExtract/SevenZipLocator.cs` with:

```csharp
using Microsoft.Win32;

namespace SmartExtract;

public static class SevenZipLocator
{
    private const string FallbackPath = @"C:\Program Files\7-Zip\";

    public static string Locate() =>
        LocateFromSmartExtractConfig() ?? LocateFromRegistry() ?? LocateFromPath() ?? FallbackPath;

    public static string? LocateFromSmartExtractConfig()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\SmartExtract");
            if (key is null) return null;
            var path = key.GetValue("SevenZipPath") as string;
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch { return null; }
    }

    public static string? LocateFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\7-Zip");
            if (key is null) return null;
            var path = (key.GetValue("Path64") as string)
                    ?? (key.GetValue("Path") as string);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch { return null; }
    }

    public static string? LocateFromPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null) return null;

        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, "7z.exe");
            if (File.Exists(candidate))
            {
                var containing = Path.GetDirectoryName(candidate);
                return containing is null ? null : containing.TrimEnd('\\') + '\\';
            }
        }
        return null;
    }
}
```

- [ ] **Step 4: Run all tests**

```powershell
dotnet test SmartExtract.slnx -c Release
```

Expected: all tests pass (was 36; now 39 with the 3 new tests).

- [ ] **Step 5: Commit**

```powershell
git add src/SmartExtract/SevenZipLocator.cs tests/SmartExtract.Tests/SevenZipLocatorTests.cs
git commit -m "feat: SevenZipLocator - check HKCU\Software\SmartExtract\SevenZipPath first"
```

---

## Task 4: Add MIT LICENSE File

**Files:**
- Create: `LICENSE`

**Interfaces:**
- Produces: `LICENSE` at repo root (referenced by `installer/SmartExtract.iss` in Task 5)

- [ ] **Step 1: Create `LICENSE`**

Create `LICENSE` at the repo root with content:

```
MIT License

Copyright (c) 2026 SmartExtract Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 2: Commit**

```powershell
git add LICENSE
git commit -m "chore: add MIT LICENSE"
```

---

## Task 5: Inno Setup Script

**Files:**
- Create: `installer/SmartExtract.iss`

**Interfaces:**
- Consumes:
  - `build\publish\SmartExtract.exe`, `SmartExtract.dll`, `SmartExtract.deps.json`, `SmartExtract.runtimeconfig.json` (from `dotnet publish`)
  - `LICENSE` at repo root (from Task 4)
  - `HKCU\Software\SmartExtract\SevenZipPath` registry value (written during install, read by Task 3's `LocateFromSmartExtractConfig`)
- Produces: `dist\SmartExtractSetup.exe`

**Prerequisites:** Inno Setup 6 must be installed to test locally. Install via: `choco install innosetup` or download from https://jrsoftware.org/isinfo.php

- [ ] **Step 1: Create `installer/` directory and the `.iss` script**

Create `installer\SmartExtract.iss`:

```iss
; SmartExtract Inno Setup Script
; Build: iscc SmartExtract.iss /DAppVersion=1.0.0
; Output: ..\dist\SmartExtractSetup.exe

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define MyAppName      "SmartExtract"
#define MyAppVersion   AppVersion
#define MyAppPublisher "SmartExtract"
#define MyAppURL       "https://github.com/mchwalek/SmartExtract"
#define MyAppExeName   "SmartExtract.exe"

[Setup]
AppId={{B4A5D3E2-7C91-4F8A-B2E6-5D9C1A3F8E4B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=SmartExtractSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\build\publish\SmartExtract.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\publish\SmartExtract.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\publish\SmartExtract.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\publish\SmartExtract.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; SmartExtract config key (removed on uninstall)
Root: HKCU; Subkey: "Software\SmartExtract"; Flags: uninsdeletekey

; Context menu: .zip
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .7z
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .rar
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .gz
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .bz2
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .tar
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Code]
var
  SevenZipPage: TInputDirWizardPage;

{ Detect 7-Zip path using same priority as SevenZipLocator.cs:
  1. HKCU\Software\7-Zip -> Path64
  2. HKCU\Software\7-Zip -> Path
  3. Hardcoded fallback
  (PATH env search is not feasible in Pascal; handled at runtime by the app) }
function GetSevenZipDefaultPath(): String;
var
  Path: String;
begin
  if RegQueryStringValue(HKCU, 'Software\7-Zip', 'Path64', Path) and (Path <> '') then
  begin
    Result := Path;
    Exit;
  end;
  if RegQueryStringValue(HKCU, 'Software\7-Zip', 'Path', Path) and (Path <> '') then
  begin
    Result := Path;
    Exit;
  end;
  Result := 'C:\Program Files\7-Zip\';
end;

procedure InitializeWizard();
begin
  SevenZipPage := CreateInputDirPage(
    wpSelectDir,
    '7-Zip Location',
    'Where is 7-Zip installed?',
    'SmartExtract needs 7-Zip to inspect and extract archives. ' +
    'Confirm or correct the folder containing 7z.exe and 7zG.exe.',
    False,
    ''
  );
  SevenZipPage.Add('');
  SevenZipPage.Values[0] := GetSevenZipDefaultPath();
end;

{ Write the confirmed 7-Zip path to HKCU\Software\SmartExtract\SevenZipPath
  after files have been installed (ssPostInstall). }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    RegWriteStringValue(HKCU, 'Software\SmartExtract', 'SevenZipPath', SevenZipPage.Values[0]);
end;

{ Check for .NET 10 Desktop Runtime via registry.
  Key: HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App
  Subkeys are version strings like "10.0.0". }
function IsDotNet10Installed(): Boolean;
var
  Keys: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(
    HKLM,
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
    Keys) then
  begin
    for I := 0 to GetArrayLength(Keys) - 1 do
      if Copy(Keys[I], 1, 3) = '10.' then
      begin
        Result := True;
        Break;
      end;
  end;
end;

{ Show a non-blocking warning if .NET 10 is absent. Installation continues. }
function InitializeSetup(): Boolean;
begin
  if not IsDotNet10Installed() then
    MsgBox(
      '.NET 10 Desktop Runtime was not detected on this machine.' + #13#10 +
      'SmartExtract requires it to run.' + #13#10 + #13#10 +
      'Download it from:' + #13#10 +
      'https://dotnet.microsoft.com/download/dotnet/10.0' + #13#10 + #13#10 +
      'Installation will continue regardless.',
      mbInformation,
      MB_OK
    );
  Result := True;
end;
```

- [ ] **Step 2: Verify the script compiles (requires Inno Setup 6 installed)**

First build publish artifacts:
```powershell
dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o build/publish/
```

Then compile the installer:
```powershell
& "C:\Program Files (x86)\Inno Setup 6\iscc.exe" installer\SmartExtract.iss /DAppVersion=0.0.0-test
```

Expected: output ends with `Successful compile (0.xx sec). Setup program size: X,XXX,XXX bytes.` and `dist\SmartExtractSetup.exe` exists.

If Inno Setup is not installed locally, skip this step — it will be validated by the GitHub Actions workflow.

- [ ] **Step 3: Commit**

```powershell
git add installer/SmartExtract.iss
git commit -m "feat: add Inno Setup installer script"
```

---

## Task 6: GitHub Actions Release Workflow

**Files:**
- Create: `.github/workflows/release.yml`

**Interfaces:**
- Consumes:
  - `installer/SmartExtract.iss` (from Task 5)
  - `dist/SmartExtractSetup.exe` as the release asset
- Produces: draft GitHub Release with `SmartExtractSetup.exe` attached on every `v*.*.*` tag push

- [ ] **Step 1: Create `.github/workflows/` directory and `release.yml`**

Create `.github\workflows\release.yml`:

```yaml
name: Release

on:
  push:
    tags:
      - 'v*.*.*'

jobs:
  build-and-release:
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Run tests
        run: dotnet test SmartExtract.slnx -c Release

      - name: Publish
        run: dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o build/publish/

      - name: Install Inno Setup 6
        run: choco install innosetup --no-progress -y

      - name: Extract version (strip leading 'v')
        id: version
        shell: pwsh
        run: |
          $ver = '${{ github.ref_name }}'.TrimStart('v')
          echo "version=$ver" >> $env:GITHUB_OUTPUT

      - name: Build installer
        shell: pwsh
        run: |
          $iscc = "C:\Program Files (x86)\Inno Setup 6\iscc.exe"
          & $iscc installer\SmartExtract.iss "/DAppVersion=${{ steps.version.outputs.version }}"
          if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

      - name: Create draft release
        uses: softprops/action-gh-release@v2
        with:
          draft: true
          name: ${{ github.ref_name }}
          files: dist/SmartExtractSetup.exe
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

- [ ] **Step 2: Commit**

```powershell
git add .github/workflows/release.yml
git commit -m "ci: add GitHub Actions release workflow (tag push -> draft release)"
```

---

## Task 7: Documentation Updates

**Files:**
- Modify: `README.md`
- Modify: `AGENTS.md`
- Modify: `DECISIONS.md`
- Modify: `PROGRESS.md`

**Interfaces:**
- Consumes: all tasks above (final structure is now known)
- Produces: up-to-date docs reflecting the new structure, install commands, and decisions

- [ ] **Step 1: Update `README.md`**

Replace the **Installation** section (currently lines 51–76) with:

```markdown
## Installation

### Option A: Windows Installer (recommended)

Download `SmartExtractSetup.exe` from the [latest release](https://github.com/mchwalek/SmartExtract/releases/latest) and run it.

The installer wizard will:
1. Let you choose between a machine-wide install (requires administrator) or a per-user install (no admin required)
2. Auto-detect your 7-Zip installation and let you confirm or correct the path
3. Warn if the .NET 10 Desktop Runtime is not present (with a download link)
4. Register the **Smart Extract** context menu entry for all supported extensions

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
```

Replace the **Project structure** section's `install/` references:

```markdown
scripts/
  install.ps1             Per-user install + context menu registration
  uninstall.ps1           Cleanup

installer/
  SmartExtract.iss        Inno Setup script (produces dist/SmartExtractSetup.exe)
```

- [ ] **Step 2: Update `AGENTS.md`**

In `AGENTS.md`, update the **Project Structure** section to replace `install/` with:

```
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
```

Update the **Development Commands** section to add:

```powershell
dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o build/publish/  # publish artifacts
PowerShell -ExecutionPolicy Bypass -File scripts/install.ps1    # install context menu
PowerShell -ExecutionPolicy Bypass -File scripts/uninstall.ps1  # uninstall
# Build installer (requires Inno Setup 6):
& "C:\Program Files (x86)\Inno Setup 6\iscc.exe" installer\SmartExtract.iss /DAppVersion=0.0.0
```

- [ ] **Step 3: Update `DECISIONS.md`**

Append the following to `DECISIONS.md`:

```markdown
## D14: 7-Zip path stored in HKCU\Software\SmartExtract\SevenZipPath
Per-user, works for both machine-wide and per-user installs. Independent of 7-Zip's
own registry key. SevenZipLocator checks this before any other source.

## D15: Check and warn for .NET 10 runtime (don't block, don't bundle)
Keeps the installer small (<5 MB). Most users on a fresh machine will install .NET
before or alongside SmartExtract. Blocking install on a missing runtime frustrates
users on air-gapped machines.

## D16: Draft releases only (not published immediately)
Gives the maintainer a review window to add/edit release notes and verify the asset
before it goes public.

## D17: Keep PowerShell scripts in scripts/ for power users
Useful for scripted/CI installs and for users who prefer not to run a GUI wizard.

## D18: Add MIT LICENSE file
Required by the Inno Setup license page. Also good practice for an open-source tool.

## D19: softprops/action-gh-release@v2 for GitHub release creation
Well-maintained action that handles draft creation and asset attachment in a single
step using only the built-in GITHUB_TOKEN.

## D20: iscc via Chocolatey on windows-latest
Chocolatey is pre-installed on GitHub-hosted Windows runners. choco install innosetup
is the standard approach for Inno Setup in CI.

## D21: install/ renamed to scripts/
Cleaner separation: scripts/ holds PowerShell helpers, build/publish/ holds dotnet
output, installer/ holds the Inno Setup script, dist/ holds the generated installer.
```

- [ ] **Step 4: Update `PROGRESS.md`**

Replace the current `PROGRESS.md` with:

```markdown
# SmartExtract — Progress Ledger

This file is the durable progress record. After any context compaction or session
resume, check this file and `git log` to determine where to resume.

## Plans

| Plan | Path |
|------|------|
| Initial implementation | `docs/superpowers/plans/2026-06-20-smart-unzip.md` |
| Windows installer + CI | `docs/superpowers/plans/2026-06-23-windows-installer.md` |

## Initial implementation tasks (all complete)

| Task | Description | Status | Commits |
|------|-------------|--------|---------|
| 1 | Project scaffold | complete | e2dea60 |
| 2 | NameHelper | complete | 5350c55 |
| 3 | ArchiveEntry + ArchiveListParser + SmartExtractLogic | complete | 53ee9e8 |
| 4 | SevenZipLocator | complete | 7fd0c60 |
| 5 | ExtractionRunner | complete | a7ef9da |
| 6 | ArchiveInspector + Program.cs integration | complete | 564e3b4 |
| 7 | Install/uninstall scripts | complete | 4b978d0 |

## Windows installer + CI tasks

| Task | Description | Status | Commits |
|------|-------------|--------|---------|
| 1 | Repo restructure (install/ → scripts/, .gitignore) | pending | — |
| 2 | Update scripts/install.ps1 (-PublishDir, -SevenZipDir) | pending | — |
| 3 | SevenZipLocator: add LocateFromSmartExtractConfig | pending | — |
| 4 | Add MIT LICENSE | pending | — |
| 5 | Inno Setup script (installer/SmartExtract.iss) | pending | — |
| 6 | GitHub Actions release workflow | pending | — |
| 7 | Documentation updates | pending | — |

## Bug fixes during initial implementation
- 8276191: ArchiveListParser: split on blank lines within ---------- blocks (D12)
- 3ffd07a: ExtractionRunner: UseShellExecute=true for 7zG.exe (D13)
- ArchiveListParserTests: added RealFormat test (multi-entry block)

## Test results (initial implementation)
- 36/36 unit tests passing (dotnet test -c Release)
- Smoke test 1 (well-wrapped archive): PASS
- Smoke test 2 (flat archive): PASS

## Next step
Wave 1: dispatch Task 1 (repo restructure)
Wave 2 (after Task 1): dispatch Tasks 2, 3, 4 in parallel
Wave 3 (after Wave 2): dispatch Task 5
Wave 4 (after Wave 3): dispatch Tasks 6, 7 in parallel
```

- [ ] **Step 5: Commit all documentation changes**

```powershell
git add README.md AGENTS.md DECISIONS.md PROGRESS.md
git commit -m "docs: update README, AGENTS.md, DECISIONS.md, PROGRESS.md for installer"
```

---

## Self-Review

### Spec Coverage Check

| Spec requirement | Covered by task |
|-----------------|-----------------|
| Rename `install/` → `scripts/` | Task 1 |
| `install.ps1` accepts `-PublishDir` | Task 2 |
| `install.ps1` accepts `-SevenZipDir`, writes registry | Task 2 |
| `SevenZipLocator` checks `HKCU\Software\SmartExtract\SevenZipPath` first | Task 3 |
| New `LocateFromSmartExtractConfig()` method + tests | Task 3 |
| MIT LICENSE file | Task 4 |
| Inno Setup wizard with install scope choice | Task 5 (`PrivilegesRequiredOverridesAllowed=dialog`) |
| 7-Zip page auto-detects from registry | Task 5 (`GetSevenZipDefaultPath()`) |
| .NET 10 check + non-blocking warning | Task 5 (`IsDotNet10Installed()`) |
| Context menu registered for 6 extensions | Task 5 (`[Registry]` section) |
| Uninstaller removes all registry entries | Task 5 (`Flags: uninsdeletekey`) |
| `HKCU\Software\SmartExtract\SevenZipPath` written post-install | Task 5 (`CurStepChanged`) |
| GitHub Actions: tag push trigger `v*.*.*` | Task 6 |
| Run tests before building installer | Task 6 |
| Strip `v` prefix from tag for version | Task 6 (`steps.version`) |
| Draft release with `SmartExtractSetup.exe` attached | Task 6 |
| `build/`, `dist/`, `installer/Output/` gitignored | Task 1 |
| README updated with installer instructions | Task 7 |
| AGENTS.md updated | Task 7 |
| DECISIONS.md D14–D21 added | Task 7 |
| PROGRESS.md updated | Task 7 |

### Placeholder Scan
No TBDs, TODOs, or vague requirements found. All code is complete.

### Type/Name Consistency
- Registry key `HKCU\Software\SmartExtract` value `SevenZipPath`: consistent across Task 2 (PS script), Task 3 (C#), Task 5 (Inno Setup Pascal), Task 7 (DECISIONS.md)
- `LocateFromSmartExtractConfig()`: defined in Task 3 implementation, tested in Task 3 tests
- `build\publish\` path: consistent across Task 2 (install.ps1 default), Task 5 (Inno Setup source), Task 6 (workflow publish step)
- `dist\SmartExtractSetup.exe`: consistent between Task 5 (`OutputDir`/`OutputBaseFilename`) and Task 6 (`files:`)
