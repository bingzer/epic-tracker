$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Error "PowerShell 7+ is required. Run with pwsh."
    exit 1
}

$default = "$env:USERPROFILE\github\epic-tracker"
$input = Read-Host "Epic Tracker project directory [$default]"
$projectDir = if ($input.Trim()) { $input.Trim() } else { $default }

if (-not (Test-Path $projectDir)) {
    Write-Error "Directory not found: $projectDir"
    exit 1
}

$settingsPath = Join-Path $projectDir ".claude\settings.json"
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

Write-Host "MCP registered at: $settingsPath"
Write-Host "Restart Claude Code to connect."
