$ErrorActionPreference = "Stop"
$root = if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { Get-Location }
$distDir = Join-Path $root "dist"

Write-Host "Building UI..."
Push-Location (Join-Path $root "EpicTracker.UI")
npm run build
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Error "npm build failed."; exit 1 }
Pop-Location

Write-Host "Publishing API (framework-dependent)..."
$publishDir = Join-Path $root "publish-pkg"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root "EpicTracker.Api") -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed."; exit 1 }

Write-Host "Bundling dist..."
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }
New-Item -ItemType Directory $distDir | Out-Null

Copy-Item $publishDir (Join-Path $distDir "publish") -Recurse
Remove-Item $publishDir -Recurse -Force
Copy-Item (Join-Path $root "scripts") (Join-Path $distDir "scripts") -Recurse

$zipPath = Join-Path $root "epic-tracker-win-x64.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $distDir "*") -DestinationPath $zipPath

Remove-Item $distDir -Recurse -Force

Write-Host ""
Write-Host "Done: $zipPath"
Write-Host "Copy this zip to the target machine, extract it, and run: .\scripts\install.ps1"
