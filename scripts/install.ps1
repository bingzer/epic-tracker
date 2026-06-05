$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Error "PowerShell 7+ is required. Install it from https://aka.ms/powershell and re-run with pwsh."
    exit 1
}
$root = if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { Split-Path (Get-Location) -Parent }

$publishDir = Join-Path $root "publish"
$isPrebuilt = Test-Path $publishDir

if (-not $isPrebuilt) {
    Write-Host "Building UI..."
    Push-Location (Join-Path $root "EpicTracker.UI")
    npm run build
    if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Error "npm build failed."; exit 1 }
    Pop-Location

    Write-Host "Publishing API..."
    dotnet publish (Join-Path $root "EpicTracker.Api") -c Release -o $publishDir
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed."; exit 1 }
} else {
    Write-Host "Pre-built publish folder detected — skipping build steps."
}

# Patch mcpServers into .claude/settings.json (project-level)
$settingsPath = Join-Path $root ".claude\settings.json"
$settings = if (Test-Path $settingsPath) {
    Get-Content $settingsPath -Raw | ConvertFrom-Json -AsHashtable
} else {
    @{}
}

if (-not $settings.ContainsKey("mcpServers")) { $settings["mcpServers"] = @{} }
$settings["mcpServers"]["epic-tracker"] = @{
    type = "http"
    url  = "http://127.0.0.1:6790/mcp"
}

New-Item -ItemType Directory -Force (Split-Path $settingsPath) | Out-Null
$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8

# Add to Windows startup
$pwsh = (Get-Command pwsh).Source
$startupDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"
$startupScript = "$startupDir\epic-tracker.bat"
$startPs1 = (Resolve-Path (Join-Path $root "scripts\start.ps1")).Path

$startupContent = "@echo off`r`nstart `"`" `"$pwsh`" -WindowStyle Hidden -File `"$startPs1`"`r`n"
Set-Content $startupScript -Value $startupContent -Encoding ASCII

Write-Host "Installed successfully."
Write-Host "  MCP:      $settingsPath (project-level)"
Write-Host "  Startup:  $startupScript"

Write-Host ""
Write-Host "Starting Epic Tracker..."
try {
    $conn = Get-NetTCPConnection -LocalPort 6790 -State Listen -ErrorAction Stop
    Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
} catch {}
Start-Sleep -Seconds 1
Start-Process "dotnet" -ArgumentList "`"$publishDir\EpicTracker.Api.dll`"" -WorkingDirectory $publishDir -WindowStyle Hidden

$healthTimeout = 30
$healthElapsed = 0
$healthy = $false
while ($healthElapsed -lt $healthTimeout) {
    Start-Sleep -Seconds 1
    $healthElapsed++
    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:6790/health" -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) { $healthy = $true; break }
    } catch {}
}

if ($healthy) {
    Write-Host "Epic Tracker running and healthy. Restart Claude Code to connect."
} else {
    Write-Warning "Started but health check did not pass within $healthTimeout seconds."
}
