param(
    [string]$Name = "snapshot"
)

$root     = Split-Path $PSScriptRoot -Parent
$src      = "$root\publish\api\epic-tracker.$Name.db"
$dest     = "$root\publish\api\epic-tracker.db"

if (-not (Test-Path $src)) {
    Write-Error "Snapshot not found: $src"
    exit 1
}

# Kill process on :6790 so the DB file isn't locked
try {
    $conn = Get-NetTCPConnection -LocalPort 6790 -State Listen -ErrorAction Stop
    Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
} catch {}

Copy-Item $src $dest -Force
Write-Host "Restored: $src -> $dest"

& "$PSScriptRoot\start.ps1"
Write-Host "Service restarted."
