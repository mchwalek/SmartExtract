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
| 1 | Repo restructure (install/ → scripts/, .gitignore) | complete | 51d5388..a3bce24, ec2d7ab |
| 2 | Update scripts/install.ps1 (-PublishDir, -SevenZipDir) | complete | 1fc680c |
| 3 | SevenZipLocator: add LocateFromSmartExtractConfig | complete | eb01d94 |
| 4 | Add MIT LICENSE | complete | 0d04d8e |
| 5 | Inno Setup script (installer/SmartExtract.iss) | complete | 90e9b54 |
| 6 | GitHub Actions release workflow | complete | 6f3637b |
| 7 | Documentation updates | complete | this commit |

## Bug fixes during initial implementation
- 8276191: ArchiveListParser: split on blank lines within ---------- blocks (D12)
- 3ffd07a: ExtractionRunner: UseShellExecute=true for 7zG.exe (D13)
- ArchiveListParserTests: added RealFormat test (multi-entry block)

## Test results (initial implementation)
- 36/36 unit tests passing (dotnet test -c Release)
- Smoke test 1 (well-wrapped archive): PASS
- Smoke test 2 (flat archive): PASS

## Next step
Review the draft release after tagging and verify `dist/SmartExtractSetup.exe` before publishing.
