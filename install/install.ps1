# SmartExtract Install Script
# Per-user - no administrator privileges required.
# To produce the publish artifacts: dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o install/

$InstallDir = Join-Path $env:LOCALAPPDATA "SmartExtract"
$ExeName    = "SmartExtract.exe"
$MenuLabel  = "Smart Extract"
$MenuKey    = "SmartExtract"
$Extensions = @(".zip", ".7z", ".rar", ".gz", ".bz2", ".tar")

$ExePath   = Join-Path $InstallDir $ExeName
$SourceExe = Join-Path $PSScriptRoot $ExeName

if (-not (Test-Path $SourceExe)) {
    Write-Error "Cannot find '$ExeName' at: $PSScriptRoot`nRun first: dotnet publish src/SmartExtract/SmartExtract.csproj -c Release -o install/"
    exit 1
}

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Write-Host "Created: $InstallDir"
}

try {
    $files = Get-ChildItem -Path $PSScriptRoot -File |
             Where-Object { $_.Extension -notin @('.ps1', '.pdb') }
    foreach ($file in $files) {
        Copy-Item -Path $file.FullName -Destination (Join-Path $InstallDir $file.Name) -Force -ErrorAction Stop
    }
    Write-Host "Installed $($files.Count) file(s) to: $InstallDir"
} catch {
    Write-Error "Failed to copy files to '$InstallDir': $_"
    exit 1
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
