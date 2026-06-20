# SmartUnzip Uninstall Script

$InstallDir = "C:\Program Files\SmartUnzip"
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
