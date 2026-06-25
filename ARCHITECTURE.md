# SmartExtract — Architecture

Design decisions and implementation notes for contributors.

---

## Shell integration

### Own context menu entry

SmartExtract registers its own "Smart Extract" verb rather than extending 7-Zip's context menu. 7-Zip's menu is a COM shell extension (`7-zip.dll`) that cannot be extended from outside.

Registration path (per-user, no admin required):

```
HKCU\Software\Classes\SystemFileAssociations\.<ext>\shell\SmartExtract\command
```

Supported extensions: `.zip`, `.7z`, `.rar`, `.gz`, `.bz2`, `.tar`.

### Windows 11

Windows 11's modern context menu only surfaces packaged (MSIX) apps at the top level. SmartExtract appears under **Show more options** — the same as 7-Zip and WinRAR. Moving to the top level would require either a sparse MSIX package (needs a signing certificate) or a COM `IExplorerCommand` DLL.

### 7-Zip path discovery

`SevenZipLocator.Locate()` checks in this order:

| Priority | Source | Registry / env |
|----------|--------|----------------|
| 1 | SmartExtract config (set by installer or `install.ps1 -SevenZipDir`) | `HKCU\Software\SmartExtract\SevenZipPath` |
| 2 | 7-Zip's own registry key (per-user install) | `HKCU\Software\7-Zip` → `Path64` / `Path` |
| 3 | `PATH` environment variable | searches for `7z.exe` |
| 4 | Hardcoded fallback | `C:\Program Files\7-Zip\` |

The SmartExtract config key is written by the installer wizard after the user confirms the 7-Zip path, and by `scripts/install.ps1 -SevenZipDir`. It is removed on uninstall.

---

## Archive inspection and extraction

### Two-phase approach

SmartExtract separates listing from extraction deliberately:

- **Listing** uses `7z.exe l -slt` (CLI). Its stdout is machine-parseable and carries the metadata needed to decide the extraction strategy.
- **Extraction** uses `7zG.exe` (GUI). It shows 7-Zip's native progress dialog and handles password prompts without SmartExtract needing to implement any UI for those.

### Extraction decision

`SmartExtractLogic` returns one of two modes:

- **Direct** — exactly one top-level entry, it is a directory, its name matches the archive base name (case-insensitive). Contents are extracted directly into the parent directory — no double-wrapping.
- **Wrapped** — everything else. A new folder named after the archive is created and used as the extraction root.

Top-level means the `Path` value contains no `/` or `\`.

### No conflict dialog

When the destination folder already exists, `7zG.exe`'s default merge/overwrite behaviour applies. SmartExtract does not show a custom conflict dialog.

### 7zG.exe must use `UseShellExecute = true`

`7zG.exe` is a GUI application that requires proper shell/window-station initialisation. With `UseShellExecute = false`, `WaitForExit()` returns before extraction completes because `7zG.exe` cannot initialise its message pump. `UseShellExecute = true` is mandatory.

### ArchiveListParser — 7-Zip 26 blank-line quirk

7-Zip 26.00 ZIP output packs multiple entries inside a single `----------` block, separated by blank lines instead of separate `----------` separators. The parser splits each `----------` section on `\n\n` before extracting `Path` and `Folder` properties.

---

## Application model

SmartExtract is a `WinExe` with `UseWindowsForms = true`. This combination:

- Suppresses the console window when launched from Explorer.
- Enables `System.Windows.Forms.MessageBox` for error dialogs without showing any Form.

All errors surface as a `MessageBox`. The application is silent on success. There is no stdout/stderr output.

All source classes are `public static` — stateless, no dependency injection.

### .NET runtime

If the .NET 10 Desktop Runtime is missing, Windows itself shows a dialog with a download link when `SmartExtract.exe` is launched. The installer does not check for the runtime; this native behaviour is sufficient.

---

## Testing

### No tests for `ArchiveInspector` or `Program`

Both wrap live process calls (`7z.exe`, `7zG.exe`, filesystem). Core logic is fully covered by unit tests for `NameHelper`, `ArchiveListParser`, `SmartExtractLogic`, `SevenZipLocator`, and `ExtractionRunner`. End-to-end behaviour is verified by smoke tests.

### `SevenZipLocatorTests` are integration tests

These tests read the real Windows registry and verify that 7-Zip executables exist on disk. 7-Zip must be installed on the test machine. This is intentional — 7-Zip is a hard runtime prerequisite.

### Tests must run in Release mode

Windows Smart App Control blocks freshly compiled Debug DLLs. Always use:

```powershell
dotnet test SmartExtract.slnx -c Release
```

---

## Installer and distribution

### Installer (Inno Setup 6)

`installer/SmartExtract.iss` produces `dist/SmartExtractSetup.exe`. The wizard:

- Offers machine-wide (admin) or per-user (no admin) install via `PrivilegesRequired=lowest` + `PrivilegesRequiredOverridesAllowed=dialog`.
- Auto-detects the 7-Zip path (mirrors `SevenZipLocator` registry and PATH logic) and lets the user confirm or correct it.
- Writes the confirmed path to `HKCU\Software\SmartExtract\SevenZipPath`.
- Registers context menu entries in `HKCU` for all six extensions.
- Registers in Add/Remove Programs with a generated uninstaller that removes all files, context menu entries, and the `HKCU\Software\SmartExtract` key.

Build the installer locally:

```powershell
dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o build/publish/
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\iscc.exe" installer\SmartExtract.iss /DAppVersion=1.0.0
```

### PowerShell scripts

`scripts/install.ps1` and `scripts/uninstall.ps1` are kept for power users and CI. The install script accepts `-PublishDir` (default: `build\publish\`) and optional `-SevenZipDir` (writes to registry when provided).

### GitHub releases

The release workflow (`.github/workflows/release.yml`) triggers on `v*.*.*` tag pushes. It runs tests, publishes, builds the installer via Chocolatey-installed `iscc`, and creates a **draft** GitHub Release with `SmartExtractSetup.exe` attached. The maintainer reviews the draft before publishing.

`softprops/action-gh-release@v2` is used for release creation; it handles draft creation and asset attachment with only the built-in `GITHUB_TOKEN`.
