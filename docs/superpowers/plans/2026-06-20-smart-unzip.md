# SmartExtract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows context menu tool that intelligently extracts archives — directly if already wrapped in a single matching folder, or into a new folder otherwise.

**Architecture:** A headless C# WinExe (`SmartExtract.exe`) invoked by the Windows context menu with the archive path as its sole argument. It calls `7z.exe l -slt` to list archive contents, applies smart extraction logic, then delegates extraction to `7zG.exe` (7-Zip GUI executable, which shows native progress and handles passwords). No custom UI is shown — errors use `MessageBox.Show`.

**Tech Stack:** C# .NET 10 (`net10.0-windows`), xUnit 2.9.x, 7z.exe + 7zG.exe from 7-Zip 26.x at `C:\Program Files\7-Zip\`, PowerShell 5.1 for install scripts.

## Global Constraints

- Target framework: `net10.0-windows` for both main project and test project
- Output type: `WinExe` (no console window flash when launched from context menu)
- `UseWindowsForms`: `true` (required for `MessageBox.Show`)
- 7-Zip path: discovered at runtime from `HKCU\Software\7-Zip\Path64` then `Path`, fallback to `C:\Program Files\7-Zip\`
- Context menu: `HKCU\Software\Classes\SystemFileAssociations\.<ext>\shell\SmartExtract` (per-user, no admin)
- Supported extensions: `.zip`, `.7z`, `.rar`, `.gz`, `.bz2`, `.tar`
- Compound extensions stripped: `.tar.gz`, `.tar.bz2`, `.tar.xz`, `.tar.zst`
- All source: namespace `SmartExtract`; all tests: namespace `SmartExtract.Tests`
- Entry point: class `Program` with `[STAThread] static int Main(string[] args)`
- Install directory variable: `$InstallDir = "C:\Program Files\SmartExtract"` at top of install script
- xUnit: `2.9.3`; `Microsoft.NET.Test.Sdk`: `17.12.0`; `xunit.runner.visualstudio`: `2.8.2`

---

## File Structure

```
smart_unzip/
  SmartExtract.sln
  .gitignore
  DECISIONS.md
  PROGRESS.md
  src/SmartExtract/SmartExtract.csproj
  src/SmartExtract/Program.cs
  src/SmartExtract/NameHelper.cs
  src/SmartExtract/ArchiveEntry.cs
  src/SmartExtract/ArchiveListParser.cs
  src/SmartExtract/SmartExtractLogic.cs
  src/SmartExtract/SevenZipLocator.cs
  src/SmartExtract/ArchiveInspector.cs
  src/SmartExtract/ExtractionRunner.cs
  tests/SmartExtract.Tests/SmartExtract.Tests.csproj
  tests/SmartExtract.Tests/NameHelperTests.cs
  tests/SmartExtract.Tests/ArchiveListParserTests.cs
  tests/SmartExtract.Tests/SmartExtractLogicTests.cs
  tests/SmartExtract.Tests/SevenZipLocatorTests.cs
  tests/SmartExtract.Tests/ExtractionRunnerTests.cs
  install/install.ps1
  install/uninstall.ps1
  docs/superpowers/plans/2026-06-20-smart-unzip.md
```

---

### Task 1: Project Scaffold

**Files:**
- Create: `SmartExtract.sln`
- Create: `.gitignore`
- Create: `src/SmartExtract/SmartExtract.csproj`
- Create: `src/SmartExtract/Program.cs` (stub — returns 0)
- Create: `tests/SmartExtract.Tests/SmartExtract.Tests.csproj`
- Create: `tests/SmartExtract.Tests/PlaceholderTest.cs`

**Interfaces:**
- Produces: `dotnet build SmartExtract.sln` exits 0; `dotnet test` exits 0 with 1 passing test

- [ ] **Step 1: Create `src/SmartExtract/SmartExtract.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>SmartExtract</AssemblyName>
    <RootNamespace>SmartExtract</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create `src/SmartExtract/Program.cs`**

```csharp
namespace SmartExtract;

class Program
{
    [System.STAThread]
    static int Main(string[] args)
    {
        return 0;
    }
}
```

- [ ] **Step 3: Create `tests/SmartExtract.Tests/SmartExtract.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <UseWindowsForms>true</UseWindowsForms>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\SmartExtract\SmartExtract.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create `tests/SmartExtract.Tests/PlaceholderTest.cs`**

```csharp
namespace SmartExtract.Tests;

public class PlaceholderTest
{
    [Xunit.Fact]
    public void Scaffold_IsWiredUp()
    {
        Xunit.Assert.True(true);
    }
}
```

- [ ] **Step 5: Create solution and add projects**

```
dotnet new sln -n SmartExtract
dotnet sln add src/SmartExtract/SmartExtract.csproj
dotnet sln add tests/SmartExtract.Tests/SmartExtract.Tests.csproj
```

- [ ] **Step 6: Build and test**

```
dotnet build SmartExtract.sln
dotnet test SmartExtract.sln
```

Expected: build succeeds, 1 test passes, output pristine.

- [ ] **Step 7: Commit**

```
git add .
git commit -m "chore: scaffold solution, projects, and placeholder test"
```

---

### Task 2: NameHelper — Archive Base Name Extraction

**Files:**
- Create: `src/SmartExtract/NameHelper.cs`
- Create: `tests/SmartExtract.Tests/NameHelperTests.cs`

**Interfaces:**
- Produces: `public static class NameHelper` with `public static string GetBaseName(string archivePath)`
- Strips compound suffixes (`.tar.gz`, `.tar.bz2`, `.tar.xz`, `.tar.zst`) case-insensitively first; otherwise `Path.GetFileNameWithoutExtension`.

- [ ] **Step 1: Write failing tests `tests/SmartExtract.Tests/NameHelperTests.cs`**

```csharp
namespace SmartExtract.Tests;

public class NameHelperTests
{
    [Xunit.Theory]
    [Xunit.InlineData(@"C:\downloads\project.zip", "project")]
    [Xunit.InlineData(@"C:\downloads\project.7z", "project")]
    [Xunit.InlineData(@"C:\downloads\project.rar", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar", "project")]
    [Xunit.InlineData(@"C:\downloads\project.gz", "project")]
    [Xunit.InlineData(@"C:\downloads\project.bz2", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar.gz", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar.bz2", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar.xz", "project")]
    [Xunit.InlineData(@"C:\downloads\project.tar.zst", "project")]
    [Xunit.InlineData(@"C:\downloads\my-project.tar.gz", "my-project")]
    [Xunit.InlineData(@"C:\downloads\My Project.zip", "My Project")]
    [Xunit.InlineData(@"C:\downloads\archive.TAR.GZ", "archive")]
    public void GetBaseName_ReturnsCorrectBaseName(string path, string expected)
    {
        Xunit.Assert.Equal(expected, NameHelper.GetBaseName(path));
    }
}
```

- [ ] **Step 2: Run — verify fail**

```
dotnet test tests/SmartExtract.Tests/SmartExtract.Tests.csproj --filter "FullyQualifiedName~NameHelperTests"
```

Expected: build error — `NameHelper` not defined.

- [ ] **Step 3: Implement `src/SmartExtract/NameHelper.cs`**

```csharp
namespace SmartExtract;

public static class NameHelper
{
    private static readonly string[] CompoundSuffixes =
    [
        ".tar.gz", ".tar.bz2", ".tar.xz", ".tar.zst"
    ];

    public static string GetBaseName(string archivePath)
    {
        var fileName = Path.GetFileName(archivePath);
        foreach (var suffix in CompoundSuffixes)
        {
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return fileName[..^suffix.Length];
        }
        return Path.GetFileNameWithoutExtension(fileName);
    }
}
```

- [ ] **Step 4: Run — verify pass**

```
dotnet test tests/SmartExtract.Tests/SmartExtract.Tests.csproj --filter "FullyQualifiedName~NameHelperTests"
```

Expected: 13 tests passing, output pristine.

- [ ] **Step 5: Full suite**

```
dotnet test SmartExtract.sln
```

- [ ] **Step 6: Commit**

```
git add src/SmartExtract/NameHelper.cs tests/SmartExtract.Tests/NameHelperTests.cs
git commit -m "feat: add NameHelper with compound extension stripping"
```

---

### Task 3: ArchiveEntry + ArchiveListParser + SmartExtractLogic

**Files:**
- Create: `src/SmartExtract/ArchiveEntry.cs`
- Create: `src/SmartExtract/ArchiveListParser.cs`
- Create: `src/SmartExtract/SmartExtractLogic.cs`
- Create: `tests/SmartExtract.Tests/ArchiveListParserTests.cs`
- Create: `tests/SmartExtract.Tests/SmartExtractLogicTests.cs`

**Interfaces:**
- `public record ArchiveEntry(string Path, bool IsDirectory)`
- `public static IReadOnlyList<ArchiveEntry> ArchiveListParser.Parse(string sevenZipOutput)`
  - Splits on `"----------"` (ten dashes). Per block: extract `Path = ` and `Folder = ` lines.
  - Only emits entry when BOTH Path and Folder lines are present (filters archive header).
  - `Folder = +` → IsDirectory true, `Folder = -` → IsDirectory false.
- `public enum ExtractionMode { Direct, Wrapped }`
- `public static ExtractionMode SmartExtractLogic.Determine(string archiveBaseName, IReadOnlyList<ArchiveEntry> entries)`
  - Top-level entry = path contains no `/` or `\`
  - Returns `Direct` iff: exactly 1 top-level entry AND it is a directory AND name equals baseName (OrdinalIgnoreCase)
  - Otherwise returns `Wrapped`

- [ ] **Step 1: Write failing tests**

`tests/SmartExtract.Tests/ArchiveListParserTests.cs`:
```csharp
namespace SmartExtract.Tests;

public class ArchiveListParserTests
{
    private const string WellWrappedOutput = """
        7-Zip 26.00 (x64)

        Listing archive: project.zip

        --
        Path = project.zip
        Type = zip

        ----------
        Path = project
        Folder = +
        Size = 0

        ----------
        Path = project/readme.txt
        Folder = -
        Size = 1234

        ----------
        Path = project/src
        Folder = +
        Size = 0
        """;

    private const string FlatOutput = """
        7-Zip 26.00 (x64)

        Listing archive: flat.zip

        --
        Path = flat.zip
        Type = zip

        ----------
        Path = readme.txt
        Folder = -
        Size = 100

        ----------
        Path = main.py
        Folder = -
        Size = 200
        """;

    [Xunit.Fact]
    public void Parse_WellWrapped_ReturnsThreeEntries()
    {
        var entries = ArchiveListParser.Parse(WellWrappedOutput);
        Xunit.Assert.Equal(3, entries.Count);
    }

    [Xunit.Fact]
    public void Parse_WellWrapped_ArchiveHeaderNotIncluded()
    {
        var entries = ArchiveListParser.Parse(WellWrappedOutput);
        Xunit.Assert.DoesNotContain(entries, e => e.Path == "project.zip");
    }

    [Xunit.Fact]
    public void Parse_WellWrapped_RootFolderIsDirectory()
    {
        var entries = ArchiveListParser.Parse(WellWrappedOutput);
        var root = Xunit.Assert.Single(entries, e => e.Path == "project");
        Xunit.Assert.True(root.IsDirectory);
    }

    [Xunit.Fact]
    public void Parse_WellWrapped_FileEntryIsNotDirectory()
    {
        var entries = ArchiveListParser.Parse(WellWrappedOutput);
        var file = Xunit.Assert.Single(entries, e => e.Path == "project/readme.txt");
        Xunit.Assert.False(file.IsDirectory);
    }

    [Xunit.Fact]
    public void Parse_Flat_ReturnsTwoEntries()
    {
        var entries = ArchiveListParser.Parse(FlatOutput);
        Xunit.Assert.Equal(2, entries.Count);
        Xunit.Assert.All(entries, e => Xunit.Assert.False(e.IsDirectory));
    }

    [Xunit.Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var entries = ArchiveListParser.Parse(string.Empty);
        Xunit.Assert.Empty(entries);
    }
}
```

`tests/SmartExtract.Tests/SmartExtractLogicTests.cs`:
```csharp
namespace SmartExtract.Tests;

public class SmartExtractLogicTests
{
    [Xunit.Fact]
    public void Determine_SingleMatchingRootFolder_ReturnsDirect()
    {
        var entries = new List<ArchiveEntry>
        {
            new("project", true),
            new("project/readme.txt", false),
            new("project/src", true),
        };
        Xunit.Assert.Equal(ExtractionMode.Direct, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_CaseInsensitiveMatch_ReturnsDirect()
    {
        var entries = new List<ArchiveEntry>
        {
            new("Project", true),
            new("Project/file.txt", false),
        };
        Xunit.Assert.Equal(ExtractionMode.Direct, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_MultipleTopLevelEntries_ReturnsWrapped()
    {
        var entries = new List<ArchiveEntry>
        {
            new("readme.txt", false),
            new("src", true),
        };
        Xunit.Assert.Equal(ExtractionMode.Wrapped, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_SingleRootFolderWithDifferentName_ReturnsWrapped()
    {
        var entries = new List<ArchiveEntry>
        {
            new("old-name", true),
            new("old-name/file.txt", false),
        };
        Xunit.Assert.Equal(ExtractionMode.Wrapped, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_FilesAtRoot_ReturnsWrapped()
    {
        var entries = new List<ArchiveEntry>
        {
            new("file1.txt", false),
            new("file2.txt", false),
        };
        Xunit.Assert.Equal(ExtractionMode.Wrapped, SmartExtractLogic.Determine("project", entries));
    }

    [Xunit.Fact]
    public void Determine_SingleFileAtRoot_ReturnsWrapped()
    {
        var entries = new List<ArchiveEntry> { new("readme.txt", false) };
        Xunit.Assert.Equal(ExtractionMode.Wrapped, SmartExtractLogic.Determine("readme", entries));
    }

    [Xunit.Fact]
    public void Determine_EmptyEntries_ReturnsWrapped()
    {
        Xunit.Assert.Equal(ExtractionMode.Wrapped,
            SmartExtractLogic.Determine("project", new List<ArchiveEntry>()));
    }

    [Xunit.Fact]
    public void Determine_BackslashSeparatorInSubpaths_TopLevelDetectedCorrectly()
    {
        var entries = new List<ArchiveEntry>
        {
            new("project", true),
            new(@"project\file.txt", false),
        };
        Xunit.Assert.Equal(ExtractionMode.Direct, SmartExtractLogic.Determine("project", entries));
    }
}
```

- [ ] **Step 2: Run — verify fail**

```
dotnet test tests/SmartExtract.Tests/SmartExtract.Tests.csproj --filter "FullyQualifiedName~ArchiveListParser|FullyQualifiedName~SmartExtractLogic"
```

Expected: build errors.

- [ ] **Step 3: Implement `src/SmartExtract/ArchiveEntry.cs`**

```csharp
namespace SmartExtract;

public record ArchiveEntry(string Path, bool IsDirectory);
```

- [ ] **Step 4: Implement `src/SmartExtract/ArchiveListParser.cs`**

```csharp
namespace SmartExtract;

public static class ArchiveListParser
{
    public static IReadOnlyList<ArchiveEntry> Parse(string output)
    {
        var entries = new List<ArchiveEntry>();
        var blocks = output.Split("----------", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            string? path = null;
            bool isDirectory = false;
            bool hasFolder = false;

            foreach (var line in block.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Path = ", StringComparison.Ordinal))
                    path = trimmed["Path = ".Length..].Trim();
                else if (trimmed.StartsWith("Folder = ", StringComparison.Ordinal))
                {
                    isDirectory = trimmed["Folder = ".Length..].Trim() == "+";
                    hasFolder = true;
                }
            }

            if (path != null && hasFolder)
                entries.Add(new ArchiveEntry(path, isDirectory));
        }

        return entries;
    }
}
```

- [ ] **Step 5: Implement `src/SmartExtract/SmartExtractLogic.cs`**

```csharp
namespace SmartExtract;

public enum ExtractionMode { Direct, Wrapped }

public static class SmartExtractLogic
{
    public static ExtractionMode Determine(string archiveBaseName, IReadOnlyList<ArchiveEntry> entries)
    {
        var topLevel = entries
            .Where(e => !e.Path.Contains('/') && !e.Path.Contains('\\'))
            .ToList();

        if (topLevel.Count == 1
            && topLevel[0].IsDirectory
            && string.Equals(topLevel[0].Path, archiveBaseName, StringComparison.OrdinalIgnoreCase))
        {
            return ExtractionMode.Direct;
        }

        return ExtractionMode.Wrapped;
    }
}
```

- [ ] **Step 6: Run — verify pass**

```
dotnet test tests/SmartExtract.Tests/SmartExtract.Tests.csproj --filter "FullyQualifiedName~ArchiveListParser|FullyQualifiedName~SmartExtractLogic"
```

Expected: 14 tests passing (6 parser + 8 logic), output pristine.

- [ ] **Step 7: Full suite**

```
dotnet test SmartExtract.sln
```

- [ ] **Step 8: Commit**

```
git add src/SmartExtract/ArchiveEntry.cs src/SmartExtract/ArchiveListParser.cs src/SmartExtract/SmartExtractLogic.cs tests/SmartExtract.Tests/ArchiveListParserTests.cs tests/SmartExtract.Tests/SmartExtractLogicTests.cs
git commit -m "feat: add ArchiveEntry, ArchiveListParser, SmartExtractLogic"
```

---

### Task 4: SevenZipLocator

**Files:**
- Create: `src/SmartExtract/SevenZipLocator.cs`
- Create: `tests/SmartExtract.Tests/SevenZipLocatorTests.cs`

**Interfaces:**
- `public static string SevenZipLocator.Locate()` — never null; tries registry then PATH then `C:\Program Files\7-Zip\`
- `public static string? SevenZipLocator.LocateFromRegistry()` — reads `HKCU\Software\7-Zip\Path64` then `Path`
- `public static string? SevenZipLocator.LocateFromPath()` — scans PATH for `7z.exe`; returns dir with trailing `\` or null

Tests are integration tests (7-Zip installed on this machine at `C:\Program Files\7-Zip\`).

- [ ] **Step 1: Write failing tests `tests/SmartExtract.Tests/SevenZipLocatorTests.cs`**

```csharp
namespace SmartExtract.Tests;

public class SevenZipLocatorTests
{
    [Xunit.Fact]
    public void LocateFromRegistry_ReturnsProgramFilesPath()
    {
        var result = SevenZipLocator.LocateFromRegistry();
        Xunit.Assert.NotNull(result);
        Xunit.Assert.Contains("7-Zip", result, System.StringComparison.OrdinalIgnoreCase);
    }

    [Xunit.Fact]
    public void Locate_ReturnedDirectoryContains7zExe()
    {
        var dir = SevenZipLocator.Locate();
        Xunit.Assert.True(System.IO.File.Exists(System.IO.Path.Combine(dir, "7z.exe")),
            $"7z.exe not found in: {dir}");
    }

    [Xunit.Fact]
    public void Locate_ReturnedDirectoryContains7zGExe()
    {
        var dir = SevenZipLocator.Locate();
        Xunit.Assert.True(System.IO.File.Exists(System.IO.Path.Combine(dir, "7zG.exe")),
            $"7zG.exe not found in: {dir}");
    }

    [Xunit.Fact]
    public void Locate_NeverReturnsNull()
    {
        var result = SevenZipLocator.Locate();
        Xunit.Assert.NotNull(result);
        Xunit.Assert.NotEmpty(result);
    }
}
```

- [ ] **Step 2: Run — verify fail**

```
dotnet test tests/SmartExtract.Tests/SmartExtract.Tests.csproj --filter "FullyQualifiedName~SevenZipLocator"
```

Expected: build errors.

- [ ] **Step 3: Implement `src/SmartExtract/SevenZipLocator.cs`**

```csharp
using Microsoft.Win32;

namespace SmartExtract;

public static class SevenZipLocator
{
    private const string FallbackPath = @"C:\Program Files\7-Zip\";

    public static string Locate() =>
        LocateFromRegistry() ?? LocateFromPath() ?? FallbackPath;

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

- [ ] **Step 4: Run — verify pass**

```
dotnet test tests/SmartExtract.Tests/SmartExtract.Tests.csproj --filter "FullyQualifiedName~SevenZipLocator"
```

Expected: 4 tests passing, output pristine.

- [ ] **Step 5: Full suite + commit**

```
dotnet test SmartExtract.sln
git add src/SmartExtract/SevenZipLocator.cs tests/SmartExtract.Tests/SevenZipLocatorTests.cs
git commit -m "feat: add SevenZipLocator with registry and PATH fallback"
```

---

### Task 5: ExtractionRunner

**Files:**
- Create: `src/SmartExtract/ExtractionRunner.cs`
- Create: `tests/SmartExtract.Tests/ExtractionRunnerTests.cs`

**Interfaces:**
- `public static string ExtractionRunner.BuildArguments(string archivePath, string outputDir)`
  - Returns exactly: `x "<archivePath>" -o"<outputDir>" -y`
- `public static void ExtractionRunner.Extract(string sevenZipDir, string archivePath, string outputDir)`
  - Spawns `7zG.exe` with `BuildArguments` result; waits for exit

- [ ] **Step 1: Write failing tests `tests/SmartExtract.Tests/ExtractionRunnerTests.cs`**

```csharp
namespace SmartExtract.Tests;

public class ExtractionRunnerTests
{
    [Xunit.Theory]
    [Xunit.InlineData(
        @"C:\downloads\project.zip",
        @"C:\downloads",
        @"x ""C:\downloads\project.zip"" -o""C:\downloads"" -y")]
    [Xunit.InlineData(
        @"C:\downloads\project.zip",
        @"C:\downloads\project",
        @"x ""C:\downloads\project.zip"" -o""C:\downloads\project"" -y")]
    [Xunit.InlineData(
        @"C:\path with spaces\archive.7z",
        @"C:\path with spaces\archive",
        @"x ""C:\path with spaces\archive.7z"" -o""C:\path with spaces\archive"" -y")]
    public void BuildArguments_ReturnsCorrectCommandString(
        string archivePath, string outputDir, string expected)
    {
        Xunit.Assert.Equal(expected, ExtractionRunner.BuildArguments(archivePath, outputDir));
    }
}
```

- [ ] **Step 2: Run — verify fail**

```
dotnet test tests/SmartExtract.Tests/SmartExtract.Tests.csproj --filter "FullyQualifiedName~ExtractionRunnerTests"
```

Expected: build errors.

- [ ] **Step 3: Implement `src/SmartExtract/ExtractionRunner.cs`**

```csharp
using System.Diagnostics;

namespace SmartExtract;

public static class ExtractionRunner
{
    public static string BuildArguments(string archivePath, string outputDir) =>
        $"x \"{archivePath}\" -o\"{outputDir}\" -y";

    public static void Extract(string sevenZipDir, string archivePath, string outputDir)
    {
        var sevenZipGui = Path.Combine(sevenZipDir, "7zG.exe");
        var psi = new ProcessStartInfo(sevenZipGui, BuildArguments(archivePath, outputDir))
        {
            UseShellExecute = false
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start: {sevenZipGui}");

        process.WaitForExit();
    }
}
```

- [ ] **Step 4: Run — verify pass**

```
dotnet test tests/SmartExtract.Tests/SmartExtract.Tests.csproj --filter "FullyQualifiedName~ExtractionRunnerTests"
```

Expected: 3 tests passing, output pristine.

- [ ] **Step 5: Full suite + commit**

```
dotnet test SmartExtract.sln
git add src/SmartExtract/ExtractionRunner.cs tests/SmartExtract.Tests/ExtractionRunnerTests.cs
git commit -m "feat: add ExtractionRunner wrapping 7zG.exe"
```

---

### Task 6: ArchiveInspector + Program.cs Integration

**Files:**
- Create: `src/SmartExtract/ArchiveInspector.cs`
- Modify: `src/SmartExtract/Program.cs` (replace stub with full implementation)

**Interfaces:**
- Consumes: `NameHelper.GetBaseName`, `SevenZipLocator.Locate`, `ArchiveListParser.Parse`, `SmartExtractLogic.Determine`, `ExtractionRunner.Extract`, `ExtractionMode`
- `public static ExtractionMode ArchiveInspector.Analyze(string sevenZipDir, string archivePath, string archiveBaseName)`
  - Shells out: `7z.exe l -slt "<archivePath>"`, captures stdout (UTF-8), calls `ArchiveListParser.Parse` then `SmartExtractLogic.Determine`
- `Program.Main` flow (in order):
  1. `args.Length != 1` → MessageBox("Usage: SmartExtract <archive-path>"), return 1
  2. `!File.Exists(args[0])` → MessageBox("File not found:\n<path>"), return 1
  3. `SevenZipLocator.Locate()` → `sevenZipDir`
  4. Missing `7z.exe` or `7zG.exe` → MessageBox("7-Zip executables not found..."), return 1
  5. `NameHelper.GetBaseName(archivePath)` → `baseName`
  6. `Path.GetDirectoryName(archivePath) ?? Directory.GetCurrentDirectory()` → `parentDir`
  7. `ArchiveInspector.Analyze(sevenZipDir, archivePath, baseName)` → `mode`
  8. `outputDir = mode == Direct ? parentDir : Path.Combine(parentDir, baseName)`
  9. `ExtractionRunner.Extract(sevenZipDir, archivePath, outputDir)`
  10. return 0. Wrap steps 3-10 in try/catch → MessageBox("Extraction failed:\n<msg>"), return 1

No new unit tests (ArchiveInspector and Program wrap live process calls). Verified by smoke tests.

- [ ] **Step 1: Implement `src/SmartExtract/ArchiveInspector.cs`**

```csharp
using System.Diagnostics;
using System.Text;

namespace SmartExtract;

public static class ArchiveInspector
{
    public static ExtractionMode Analyze(string sevenZipDir, string archivePath, string archiveBaseName)
    {
        var output = ListArchive(sevenZipDir, archivePath);
        var entries = ArchiveListParser.Parse(output);
        return SmartExtractLogic.Determine(archiveBaseName, entries);
    }

    private static string ListArchive(string sevenZipDir, string archivePath)
    {
        var sevenZip = Path.Combine(sevenZipDir, "7z.exe");
        var psi = new ProcessStartInfo(sevenZip, $"l -slt \"{archivePath}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start: {sevenZip}");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
```

- [ ] **Step 2: Replace `src/SmartExtract/Program.cs`**

```csharp
using System.Windows.Forms;
using SmartExtract;

class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            MessageBox.Show("Usage: SmartExtract <archive-path>",
                "SmartExtract", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        var archivePath = args[0];

        if (!File.Exists(archivePath))
        {
            MessageBox.Show($"File not found:\n{archivePath}",
                "SmartExtract", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        try
        {
            var sevenZipDir = SevenZipLocator.Locate();

            if (!File.Exists(Path.Combine(sevenZipDir, "7z.exe"))
             || !File.Exists(Path.Combine(sevenZipDir, "7zG.exe")))
            {
                MessageBox.Show(
                    $"7-Zip executables not found in:\n{sevenZipDir}\n\nPlease install 7-Zip.",
                    "SmartExtract", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            var baseName  = NameHelper.GetBaseName(archivePath);
            var parentDir = Path.GetDirectoryName(archivePath)
                         ?? Directory.GetCurrentDirectory();
            var mode      = ArchiveInspector.Analyze(sevenZipDir, archivePath, baseName);
            var outputDir = mode == ExtractionMode.Direct
                          ? parentDir
                          : Path.Combine(parentDir, baseName);

            ExtractionRunner.Extract(sevenZipDir, archivePath, outputDir);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Extraction failed:\n{ex.Message}",
                "SmartExtract", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
```

- [ ] **Step 3: Build Release**

```
dotnet build SmartExtract.sln -c Release
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 4: Full test suite**

```
dotnet test SmartExtract.sln
```

- [ ] **Step 5: Smoke test (well-wrapped archive)**

```powershell
$tmp = "$env:TEMP\SmartExtract_smoke"
New-Item -ItemType Directory -Path "$tmp\project" -Force | Out-Null
Set-Content "$tmp\project\readme.txt" "hello"
& "C:\Program Files\7-Zip\7z.exe" a "$tmp\project.zip" "$tmp\project" -w"$tmp" | Out-Null
$exe = "src\SmartExtract\bin\Release\net10.0-windows\SmartExtract.exe"
& $exe "$tmp\project.zip"
$ok = Test-Path "$tmp\project\readme.txt"
Write-Host "Smoke test (well-wrapped): $(if ($ok) { 'PASS' } else { 'FAIL' })"
Remove-Item $tmp -Recurse -Force
```

Expected: `PASS` (readme.txt is at `$tmp\project\readme.txt`, not `$tmp\project\project\readme.txt`)

- [ ] **Step 6: Smoke test (flat archive)**

```powershell
$tmp = "$env:TEMP\SmartExtract_smoke2"
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
Set-Content "$tmp\readme.txt" "hello"
Set-Content "$tmp\main.py" "print('hi')"
& "C:\Program Files\7-Zip\7z.exe" a "$tmp\flat.zip" "$tmp\readme.txt" "$tmp\main.py" | Out-Null
$exe = "src\SmartExtract\bin\Release\net10.0-windows\SmartExtract.exe"
& $exe "$tmp\flat.zip"
$ok = (Test-Path "$tmp\flat\readme.txt") -and (Test-Path "$tmp\flat\main.py")
Write-Host "Smoke test (flat): $(if ($ok) { 'PASS' } else { 'FAIL' })"
Remove-Item $tmp -Recurse -Force
```

Expected: `PASS`

- [ ] **Step 7: Commit**

```
git add src/SmartExtract/ArchiveInspector.cs src/SmartExtract/Program.cs
git commit -m "feat: add ArchiveInspector and wire up Program.cs entry point"
```

---

### Task 7: Install and Uninstall Scripts

**Files:**
- Create: `install/install.ps1`
- Create: `install/uninstall.ps1`

**Interfaces:**
- `install.ps1` top-level vars (single source of truth for future MSI migration):
  `$InstallDir`, `$ExeName`, `$MenuLabel`, `$MenuKey`, `$Extensions`
- Copies `$PSScriptRoot\SmartExtract.exe` to `$InstallDir\SmartExtract.exe`
- Writes for each extension: `HKCU:\Software\Classes\SystemFileAssociations\.<ext>\shell\SmartExtract`
  with `(Default)="Smart Extract"`, `Icon="<ExePath>,0"`, and `command\(Default)='"<ExePath>" "%1"'`
- `uninstall.ps1`: removes same registry keys and `$InstallDir`

- [ ] **Step 1: Write `install/install.ps1`**

```powershell
# SmartExtract Install Script
# Per-user — no administrator privileges required.

$InstallDir = "C:\Program Files\SmartExtract"
$ExeName    = "SmartExtract.exe"
$MenuLabel  = "Smart Extract"
$MenuKey    = "SmartExtract"
$Extensions = @(".zip", ".7z", ".rar", ".gz", ".bz2", ".tar")

$ExePath   = Join-Path $InstallDir $ExeName
$SourceExe = Join-Path $PSScriptRoot $ExeName

if (-not (Test-Path $SourceExe)) {
    Write-Error "Cannot find '$ExeName' at: $PSScriptRoot`nBuild first: dotnet publish src/SmartExtract -c Release -o install/"
    exit 1
}

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Write-Host "Created: $InstallDir"
}

Copy-Item -Path $SourceExe -Destination $ExePath -Force
Write-Host "Installed: $ExePath"

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

- [ ] **Step 2: Write `install/uninstall.ps1`**

```powershell
# SmartExtract Uninstall Script

$InstallDir = "C:\Program Files\SmartExtract"
$MenuKey    = "SmartExtract"
$Extensions = @(".zip", ".7z", ".rar", ".gz", ".bz2", ".tar")

foreach ($ext in $Extensions) {
    $shellKey = "HKCU:\Software\Classes\SystemFileAssociations\$ext\shell\$MenuKey"
    if (Test-Path $shellKey) {
        Remove-Item -Path $shellKey -Recurse -Force
        Write-Host "Removed: $ext"
    } else {
        Write-Host "Not found (skipping): $shellKey"
    }
}

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
    Write-Host "Removed: $InstallDir"
} else {
    Write-Host "Not found (skipping): $InstallDir"
}

Write-Host ""
Write-Host "Uninstall complete."
```

- [ ] **Step 3: Build and publish**

```
dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o install/ --self-contained false
```

- [ ] **Step 4: Run install and verify registry**

```powershell
PowerShell -ExecutionPolicy Bypass -File install/install.ps1
Get-ItemProperty "HKCU:\Software\Classes\SystemFileAssociations\.zip\shell\SmartExtract"
```

Expected: `(Default)` = `Smart Extract`, `Icon` value present.

- [ ] **Step 5: Run uninstall and verify cleanup**

```powershell
PowerShell -ExecutionPolicy Bypass -File install/uninstall.ps1
$key = Get-Item "HKCU:\Software\Classes\SystemFileAssociations\.zip\shell\SmartExtract" -ErrorAction SilentlyContinue
Write-Host $(if ($null -eq $key) { "PASS: registry key removed" } else { "FAIL: key still present" })
```

Expected: `PASS: registry key removed`

- [ ] **Step 6: Commit**

```
git add install/install.ps1 install/uninstall.ps1
git commit -m "feat: add PowerShell install/uninstall scripts"
```

---

## Execution Notes for Orchestrator

### Parallelism Strategy (dispatching-parallel-agents)

After Task 1 completes, use parallel dispatch:
- **Wave 2:** Tasks 2 + 3 simultaneously (no file overlap)
- **Wave 3:** Tasks 4 + 5 simultaneously (no file overlap)
- **Wave 4:** Tasks 6 + 7 simultaneously (Task 7 has no source dependencies)

### SDD Script Equivalents (Windows PowerShell)

**task-brief** — extract Task N from plan:
```powershell
$planFile = "docs\superpowers\plans\2026-06-20-smart-unzip.md"
$n = 1   # change per task
$content = Get-Content $planFile -Raw
if ($content -match "(?s)(### Task ${n}:.*?)(?=\n### Task |\z)") {
    $outFile = "$env:TEMP\task-${n}-brief.md"
    $Matches[1] | Set-Content $outFile -Encoding UTF8
    Write-Host "wrote $outFile"
}
```

**review-package** — generate diff for reviewer:
```powershell
$base = "abc1234"   # SHA recorded before task started
$out  = "$env:TEMP\review-${base}.md"
@(
    "# Review package: ${base}..HEAD",
    "",
    "## Commits",
    (git log --oneline "${base}..HEAD"),
    "",
    "## Files changed",
    (git diff --stat "${base}..HEAD"),
    "",
    "## Diff",
    (git diff -U10 "${base}..HEAD")
) -join "`n" | Set-Content $out -Encoding UTF8
Write-Host "wrote $out"
```
