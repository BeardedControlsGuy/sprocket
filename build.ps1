# Builds sprocket.exe with the in-box .NET 4 csc (no SDK needed); -Msi also rebuilds the installer.
param([switch]$Msi)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& $csc /nologo /target:winexe /optimize+ `
    /out:"$root\build\sprocket.exe" `
    /win32icon:"$root\assets\sprocket.ico" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll `
    "$root\src\*.cs"
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

if (-not (Test-Path "$root\build\assets")) { New-Item -ItemType Directory "$root\build\assets" | Out-Null }
Copy-Item "$root\assets\*" "$root\build\assets\" -Force
Write-Host "Built $root\build\sprocket.exe"

if ($Msi) {
    wix build "$root\installer\Sprocket.wxs" `
        -d "BuildDir=$root\build" -d "AssetsDir=$root\assets" `
        -o "$root\build\Sprocket.msi"
    if ($LASTEXITCODE -ne 0) { throw "wix failed with exit code $LASTEXITCODE" }
    Write-Host "Built $root\build\Sprocket.msi"
}
