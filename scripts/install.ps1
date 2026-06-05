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

# Ask user where epics will live and patch appsettings.json
$defaultEpicsPath = "$env:USERPROFILE\epic-tracker"
$epicsInput = Read-Host "Epics base path (where epics/ folder will be created) [$defaultEpicsPath]"
$epicsBasePath = if ($epicsInput.Trim()) { $epicsInput.Trim() } else { $defaultEpicsPath }

$appSettings = Join-Path $publishDir "appsettings.json"
$cfg = Get-Content $appSettings -Raw | ConvertFrom-Json -AsHashtable
$cfg["EpicTracker"]["EpicsBasePath"] = $epicsBasePath
$cfg | ConvertTo-Json -Depth 10 | Set-Content $appSettings -Encoding UTF8
Write-Host "  EpicsBasePath: $epicsBasePath"

# Register MCP — ask user where their project dir is
& (Join-Path $root "scripts\install-mcp.ps1")

# Add to Windows startup
$pwsh = (Get-Command pwsh).Source
$startupDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"
$startupScript = "$startupDir\epic-tracker.bat"
$startPs1 = (Resolve-Path (Join-Path $root "scripts\start.ps1")).Path

$startupContent = "@echo off`r`nstart `"`" `"$pwsh`" -WindowStyle Hidden -File `"$startPs1`"`r`n"
Set-Content $startupScript -Value $startupContent -Encoding ASCII

Write-Host "Installed successfully."
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
