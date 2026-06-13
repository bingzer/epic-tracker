param(
    [string]$Name = "snapshot"
)

$root = Split-Path $PSScriptRoot -Parent
$src  = "$root\publish\api\epic-tracker.db"
$dest = "$root\publish\api\epic-tracker.$Name.db"

if (-not (Test-Path $src)) {
    Write-Error "DB not found at $src"
    exit 1
}

Copy-Item $src $dest -Force
Write-Host "Saved: $dest"
