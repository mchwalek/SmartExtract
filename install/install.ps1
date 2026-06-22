# SmartUnzip Install Script
# Per-user - no administrator privileges required.

$InstallDir = Join-Path $env:LOCALAPPDATA "SmartUnzip"
$ExeName    = "SmartUnzip.exe"
$MenuLabel  = "Smart Extract"
$MenuKey    = "SmartExtract"
$Extensions = @(".zip", ".7z", ".rar", ".gz", ".bz2", ".tar")

$ExePath   = Join-Path $InstallDir $ExeName
$SourceExe = Join-Path $PSScriptRoot $ExeName

if (-not (Test-Path $SourceExe)) {
    Write-Error "Cannot find '$ExeName' at: $PSScriptRoot`nBuild first: dotnet publish src/SmartUnzip -c Release -o install/"
    exit 1
}

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Write-Host "Created: $InstallDir"
}

try {
    Copy-Item -Path $SourceExe -Destination $ExePath -Force -ErrorAction Stop
    Write-Host "Installed: $ExePath"
} catch {
    Write-Error "Failed to copy '$ExeName' to '$InstallDir': $_"
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
