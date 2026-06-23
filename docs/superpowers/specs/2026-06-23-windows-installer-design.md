# SmartExtract Windows Installer & GitHub Release Pipeline — Design

**Date:** 2026-06-23  
**Status:** Approved

## Goal

Replace the PowerShell-only install approach with a proper Windows installer (Inno Setup) that:
- Presents a GUI wizard with an install-scope choice (machine-wide vs per-user)
- Auto-detects the 7-Zip location and lets the user confirm or override it
- Stores the confirmed 7-Zip path in the registry for use by `SevenZipLocator`
- Checks for the .NET 10 Desktop Runtime and warns if absent
- Registers in Add/Remove Programs with a proper uninstaller

A GitHub Actions workflow builds the installer on every version tag push and attaches it to a draft GitHub Release.

The existing PowerShell scripts are kept (renamed directory) for power users and CI.

---

## Repository Structure

### Before

```
install/
  install.ps1
  uninstall.ps1
  (publish artifacts dropped here during manual builds)
```

### After

```
scripts/
  install.ps1          ← updated: -PublishDir (default build/publish/), optional -SevenZipDir (written to registry)
  uninstall.ps1        ← unchanged logic; updated path references only
build/
  publish/             ← dotnet publish output (gitignored)
installer/
  SmartExtract.iss     ← Inno Setup script
dist/
  SmartExtractSetup.exe ← produced by Inno Setup (gitignored)
.github/
  workflows/
    release.yml        ← tag push → build → Inno Setup → draft GitHub Release
LICENSE                ← new: MIT license file (shown in installer wizard)
```

`.gitignore` updated to ignore `build/`, `dist/`, and `installer/Output/` (Inno Setup's default compiler output directory).  
`README.md`, `AGENTS.md`, `DECISIONS.md`, and `PROGRESS.md` updated to reflect the new structure.

---

## SevenZipLocator Changes

A new `LocateFromSmartExtractConfig()` method is prepended to the lookup chain:

```
1. HKCU\Software\SmartExtract\SevenZipPath   ← written by installer (new)
2. HKCU\Software\7-Zip → Path64 / Path        ← existing
3. PATH environment variable                   ← existing
4. C:\Program Files\7-Zip\                    ← hardcoded fallback
```

`Locate()` calls `LocateFromSmartExtractConfig()` before `LocateFromRegistry()`. If the key is absent (users who installed via PS scripts or haven't run the installer yet), behavior is identical to today.

**Registry key:** `HKCU\Software\SmartExtract` value `SevenZipPath` (REG_SZ), storing the directory path (trailing backslash, matching existing conventions).

A new `SevenZipLocatorTests` test case verifies the `LocateFromSmartExtractConfig()` path (written/read/cleaned up within the test).

---

## Inno Setup Script (`installer/SmartExtract.iss`)

### Wizard Pages (in order)

| Page | Content |
|------|---------|
| Welcome | Standard Inno Setup welcome |
| License | MIT LICENSE file |
| Install scope | Radio: "For all users (requires administrator)" / "For me only (no admin required)" |
| Destination folder | Pre-filled: `C:\Program Files\SmartExtract\` (admin) or `{localappdata}\Programs\SmartExtract\` (per-user) |
| 7-Zip location | Custom page: text input pre-filled by Pascal detection script; Browse button |
| Ready to install | Standard summary |
| Installing | Progress |
| Finish | Standard finish |

### 7-Zip Auto-Detection (Pascal script)

The `.iss` Pascal script mirrors `SevenZipLocator` logic:
1. Read `HKCU\Software\7-Zip` → `Path64` then `Path`
2. If not found, search `PATH` environment variable for `7z.exe`
3. If not found, default to `C:\Program Files\7-Zip\`

### .NET 10 Runtime Check

On wizard launch (before `wpReady`), a Pascal script checks for .NET 10 Desktop Runtime by reading:

```
HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App
```

If no key with a version prefix `10.` is found, a non-blocking warning dialog is shown:

> ".NET 10 Desktop Runtime was not detected on this machine. SmartExtract requires it to run. You can download it from https://dotnet.microsoft.com/download/dotnet/10.0 — installation will continue regardless."

### Post-Install Registry Writes

- `HKCU\Software\SmartExtract\SevenZipPath` ← user's confirmed 7-Zip directory
- `HKCU\Software\Classes\SystemFileAssociations\<ext>\shell\SmartExtract\` for `.zip`, `.7z`, `.rar`, `.gz`, `.bz2`, `.tar`
- `HKCU\Software\Classes\SystemFileAssociations\<ext>\shell\SmartExtract\command\` with `"<ExePath>" "%1"`

### Uninstall (auto-generated)

Inno Setup's generated uninstaller removes:
- All installed files and the install directory
- `HKCU\Software\SmartExtract` key (entire tree)
- All context menu registry entries
- The Apps & Features entry

---

## GitHub Actions Workflow (`.github/workflows/release.yml`)

**Trigger:** `push` to tags matching `v*.*.*`

**Runner:** `windows-latest`

### Steps

1. `actions/checkout@v4`
2. `actions/setup-dotnet@v4` — .NET 10
3. `dotnet test SmartExtract.slnx -c Release` — fail fast on test breakage
4. `dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o build/publish/`
5. Install Inno Setup 6 via Chocolatey: `choco install innosetup --no-progress`
6. Run Inno Setup: `iscc installer/SmartExtract.iss "/DAppVersion=${{ github.ref_name }}"`  
   — the `.iss` script strips the `v` prefix from `AppVersion` for display
7. `softprops/action-gh-release@v2` — creates a **draft** release with `dist/SmartExtractSetup.exe` attached; uses `GITHUB_TOKEN` (no additional secrets needed)

The release name is the tag name (e.g., `v1.0.0`). Release notes are left blank in the draft for the maintainer to fill in before publishing.

---

## Decisions Made

| # | Decision | Rationale |
|---|----------|-----------|
| D14 | Store user's 7-Zip path in `HKCU\Software\SmartExtract\SevenZipPath` | Per-user, works for both install scopes, independent of 7-Zip's own key |
| D15 | Check and warn for .NET 10 runtime (don't block, don't bundle) | Keeps installer small; most users will have .NET 10 once the app is released |
| D16 | Draft releases only | Gives maintainer a review window before going public |
| D17 | Keep PowerShell scripts in `scripts/` | Useful for power users and future CI automation |
| D18 | Add MIT LICENSE file | Required by Inno Setup license page; good practice |
| D19 | `softprops/action-gh-release@v2` for release creation | Well-maintained, handles draft+asset attachment in one step |
| D20 | `iscc` via Chocolatey on `windows-latest` | Chocolatey is pre-installed on GitHub-hosted Windows runners |

---

## Files Changed / Created

| File | Change |
|------|--------|
| `install/` → `scripts/` | Directory renamed |
| `scripts/install.ps1` | Updated to accept `-PublishDir` param (default `build/publish/`) and `-SevenZipDir` param (optional; if provided, written to `HKCU\Software\SmartExtract\SevenZipPath`) |
| `scripts/uninstall.ps1` | Path reference updates only |
| `src/SmartExtract/SevenZipLocator.cs` | Add `LocateFromSmartExtractConfig()`; update `Locate()` |
| `tests/SmartExtract.Tests/SevenZipLocatorTests.cs` | Add test for `LocateFromSmartExtractConfig()` |
| `installer/SmartExtract.iss` | New: Inno Setup script |
| `.github/workflows/release.yml` | New: GitHub Actions release workflow |
| `LICENSE` | New: MIT license file |
| `.gitignore` | Add `build/` and `dist/` |
| `README.md` | Update install instructions, structure diagram |
| `AGENTS.md` | Update project structure section, development commands |
| `DECISIONS.md` | Add D14–D20 |
| `PROGRESS.md` | Update with installer task progress |

---

## Out of Scope

- Code signing the installer executable (not planned; would require a certificate purchase)
- Winget / Chocolatey package submission (future work)
- Auto-update mechanism (future work)
- Supporting non-English locales in the installer wizard (English only)
