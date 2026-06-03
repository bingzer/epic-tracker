$ErrorActionPreference = "Stop"
$root = if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { (Get-Location).Path }

function Kill-Port($port) {
    $timeout = 15; $elapsed = 0
    try { $conn = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction Stop
          Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue } catch {}
    while ($elapsed -lt $timeout) {
        $inUse = $null
        try { $inUse = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction Stop } catch {}
        if (-not $inUse) { break }
        Start-Sleep -Seconds 1; $elapsed++
    }
    if ($elapsed -ge $timeout) { Write-Error "Port $port still in use after $timeout seconds. Aborting."; exit 1 }
}

function Wait-Healthy($url) {
    $timeout = 30; $elapsed = 0
    while ($elapsed -lt $timeout) {
        Start-Sleep -Seconds 1; $elapsed++
        try {
            $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200) { return $true }
        } catch {}
    }
    return $false
}

# Kill existing process
Kill-Port 6790

# Build UI
Push-Location "$root\EpicTracker.UI"
npm run build
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Error "npm build failed. Aborting."; exit 1 }
Pop-Location

# Publish API
dotnet publish "$root\EpicTracker.Api" -c Release -o "$root\publish\api"
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish EpicTracker.Api failed. Aborting."; exit 1 }


# Start API
Start-Process "dotnet" -ArgumentList "`"$root\publish\api\EpicTracker.Api.dll`"" -WorkingDirectory "$root\publish\api" -WindowStyle Hidden

# Health check
$apiHealthy = Wait-Healthy "http://127.0.0.1:6790/health"

if ($apiHealthy) { Write-Host "API healthy at http://localhost:6790" } else { Write-Warning "API health check failed." }
