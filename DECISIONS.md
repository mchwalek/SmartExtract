# SmartUnzip — Architecture Decisions

## D1: Separate context menu entry (not inside 7-Zip's submenu)
7-Zip's context menu is a COM shell extension (7-zip.dll) that cannot be extended
from outside. SmartUnzip registers its own "Smart Extract" entry at
HKCU\Software\Classes\SystemFileAssociations\.<ext>\shell\SmartExtract.

## D2: 7zG.exe for extraction (not 7z.exe)
7zG.exe is 7-Zip's GUI executable. It shows 7-Zip's native progress dialog and
handles password prompts. No custom progress window needed in SmartUnzip.

## D3: 7z.exe for archive listing
7z.exe CLI is used with `l -slt` to list contents and parse the output.

## D4: No conflict dialog
When the destination folder already exists, 7zG.exe default behavior (merge/overwrite
with -y flag) applies. No custom conflict dialog is shown.

## D5: Sequential task execution with limited parallelism
Tasks 2-5 operate on independent files, but all commit to the same git repo.
Strategy: Tasks 1-6 sequential; Task 7 (install scripts) parallel with Task 6.

## D6: No unit tests for ArchiveInspector or Program.cs
Both wrap live process calls (7z.exe, 7zG.exe, file system). Core logic is fully
covered by NameHelper, ArchiveListParser, SmartExtractLogic, SevenZipLocator, and
ExtractionRunner tests. Integration verified by smoke tests.

## D7: SevenZipLocator tests are integration tests
Tests verify real registry access and file existence. This is intentional:
7-Zip installed is a hard prerequisite for the tool, so registry testing is appropriate.

## D8: SDD bash scripts replaced with PowerShell equivalents
The subagent-driven-development skill references bash scripts (task-brief,
review-package). On Windows, the orchestrator uses inline PowerShell equivalents
documented in the plan Execution Notes section.

## D9: WinExe + UseWindowsForms for MessageBox
OutputType=WinExe suppresses the console window. UseWindowsForms=true enables
System.Windows.Forms.MessageBox for error dialogs. No Form is ever shown.

## D10: Test project targets net10.0-windows
Required because SmartUnzip.csproj uses Windows APIs, and SevenZipLocatorTests
access the Windows registry directly.
