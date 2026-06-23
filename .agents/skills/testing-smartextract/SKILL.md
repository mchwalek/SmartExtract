---
name: testing-smartextract
description: Use when running tests, adding test coverage, debugging test failures, or verifying the install and smoke tests for the SmartExtract project.
metadata:
  internal: true
---

# Testing SmartExtract

## Test Suite Overview

| Suite       | Command                                      | Files                                    | What it tests                                        |
|-------------|----------------------------------------------|------------------------------------------|------------------------------------------------------|
| Unit        | `dotnet test SmartExtract.slnx -c Release`   | `tests/SmartExtract.Tests/**/*.cs`       | All pure logic and integration with real 7-Zip       |
| Smoke tests | Manual PowerShell (see below)                | n/a                                      | End-to-end extraction via the built .exe             |

**Always use `-c Release` for tests.** Windows Smart App Control blocks freshly compiled Debug DLLs from loading. Release builds are unaffected.

## Running Tests

```powershell
dotnet test SmartExtract.slnx -c Release              # all 36 tests
dotnet test SmartExtract.slnx -c Release --filter "FullyQualifiedName~NameHelper"         # NameHelper tests only
dotnet test SmartExtract.slnx -c Release --filter "FullyQualifiedName~SmartExtractLogic"  # extraction logic tests only
```

Expected output: `36/36 passing, output pristine`.

## Test Files and Coverage

| Test file                    | Class under test      | Type        | Notes                                          |
|------------------------------|-----------------------|-------------|------------------------------------------------|
| `NameHelperTests.cs`         | `NameHelper`          | Unit        | 13 theory cases for extension stripping        |
| `ArchiveListParserTests.cs`  | `ArchiveListParser`   | Unit        | Real-format multi-entry block case included    |
| `SmartExtractLogicTests.cs`  | `SmartExtractLogic`   | Unit        | 8 cases: Direct/Wrapped, case-insensitive, backslash paths |
| `SevenZipLocatorTests.cs`    | `SevenZipLocator`     | Integration | Reads real registry; requires 7-Zip installed  |
| `ExtractionRunnerTests.cs`   | `ExtractionRunner`    | Unit        | 3 cases for `BuildArguments` only (no process) |

`ArchiveInspector` and `Program` are not unit-tested — they wrap live process calls. Covered by smoke tests.

## Unit Test Patterns

### Pure logic tests (NameHelper, SmartExtractLogic, ExtractionRunner.BuildArguments)

Test directly — no mocking needed:

```csharp
[Xunit.Theory]
[Xunit.InlineData(@"C:\downloads\project.tar.gz", "project")]
[Xunit.InlineData(@"C:\downloads\archive.TAR.GZ", "archive")]
public void GetBaseName_ReturnsCorrectBaseName(string path, string expected)
{
    Xunit.Assert.Equal(expected, NameHelper.GetBaseName(path));
}
```

### Parser tests (ArchiveListParser)

Use inline string constants that mimic real `7z.exe l -slt` output. The real-world format packs multiple entries in one `----------` block separated by blank lines — include a test for that:

```csharp
private const string WellWrappedRealFormat = """
    ...
    ----------
    Path = project
    Folder = +
    Offset = 0

    Path = project\readme.txt
    Folder = -
    """;

[Xunit.Fact]
public void Parse_RealFormat_MultipleEntriesInOneBlock_ParsedCorrectly()
{
    var entries = ArchiveListParser.Parse(WellWrappedRealFormat);
    Xunit.Assert.Equal(2, entries.Count);
}
```

### Integration tests (SevenZipLocator)

These test against the real Windows registry and filesystem. 7-Zip must be installed at `C:\Program Files\7-Zip\`. No mocking.

## Adding New Tests

1. Add a test method or `[InlineData]` row to the relevant `*Tests.cs` file
2. Run the targeted filter: `dotnet test ... --filter "FullyQualifiedName~<ClassName>"`
3. Verify it passes, then run the full suite

## Smoke Tests

Run after `dotnet publish ... -o install/` and `dotnet build SmartExtract.slnx -c Release`:

```powershell
$exe = (Resolve-Path "src\SmartExtract\bin\Release\net10.0-windows\SmartExtract.exe").Path

# Smoke 1: well-wrapped archive -> direct extraction (no double-folder)
$tmp = "$env:TEMP\se_smoke1"
New-Item -ItemType Directory -Path "$tmp\project" -Force | Out-Null
Set-Content "$tmp\project\readme.txt" "hello"
Push-Location $tmp; & "C:\Program Files\7-Zip\7z.exe" a "project.zip" "project" | Out-Null; Pop-Location
$p = Start-Process -FilePath $exe -ArgumentList "`"$tmp\project.zip`"" -Wait -PassThru
$ok = (Test-Path "$tmp\project\readme.txt") -and -not (Test-Path "$tmp\project\project")
Write-Host "Smoke 1 (well-wrapped): $(if ($ok) { 'PASS' } else { 'FAIL' }) [exit=$($p.ExitCode)]"
Remove-Item $tmp -Recurse -Force

# Smoke 2: flat archive -> wrapped in new folder
$tmp = "$env:TEMP\se_smoke2"
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
Set-Content "$tmp\file.txt" "hello"
Push-Location $tmp; & "C:\Program Files\7-Zip\7z.exe" a "flat.zip" "file.txt" | Out-Null; Pop-Location
$p = Start-Process -FilePath $exe -ArgumentList "`"$tmp\flat.zip`"" -Wait -PassThru
$ok = Test-Path "$tmp\flat\file.txt"
Write-Host "Smoke 2 (flat): $(if ($ok) { 'PASS' } else { 'FAIL' }) [exit=$($p.ExitCode)]"
Remove-Item $tmp -Recurse -Force
```

**Use `Start-Process -Wait`** (not `& $exe`) when calling the built .exe from PowerShell — SmartExtract is a WinExe, and `&` does not always synchronously wait for GUI applications.
