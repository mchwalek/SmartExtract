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
