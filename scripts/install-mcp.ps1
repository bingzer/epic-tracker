$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Error "PowerShell 7+ is required. Run with pwsh."
    exit 1
}

$projectDir = if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { (Get-Location).Path }

if (-not (Test-Path $projectDir)) {
    Write-Error "Directory not found: $projectDir"
    exit 1
}

$mcpPath = Join-Path $projectDir ".mcp.json"
$mcp = if (Test-Path $mcpPath) {
    Get-Content $mcpPath -Raw | ConvertFrom-Json -AsHashtable
} else {
    @{}
}

if (-not $mcp.ContainsKey("mcpServers")) { $mcp["mcpServers"] = @{} }
$mcp["mcpServers"]["epic-tracker"] = @{
    type = "http"
    url  = "http://127.0.0.1:6790/mcp"
}

$mcp | ConvertTo-Json -Depth 10 | Set-Content $mcpPath -Encoding UTF8

Write-Host "MCP registered at: $mcpPath"
Write-Host "Restart Claude Code to connect."
