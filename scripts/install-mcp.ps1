#requires -Version 7.0
[CmdletBinding()]
param([string]$Name = 'fishpeek', [string]$GameRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$python = Join-Path $repoRoot '.venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $python)) { throw 'Run scripts/start-workbench.ps1 once to prepare the environment.' }
if (-not $GameRoot) { $GameRoot = $repoRoot }
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$existing = & codex mcp get $Name --json 2>$null
if ($LASTEXITCODE -eq 0) {
    $entry = $existing | ConvertFrom-Json
    if ($entry.transport.command -eq $python) {
        Write-Output "MCP server already registered: $Name"
        return
    }
    throw "MCP name already in use: $Name. Choose a different -Name."
}
$state = Join-Path $repoRoot 'assistant/.state'
$events = Join-Path $GameRoot 'BepInEx/fishpeek/encounters.jsonl'
& codex mcp add $Name --env "FISHPEEK_STATE_DIR=$state" --env "FISHPEEK_EVENTS_FILE=$events" --env 'PYTHONUTF8=1' -- $python -m fishpeek mcp
if ($LASTEXITCODE -ne 0) { throw 'MCP registration failed.' }
Write-Output "MCP registered: $Name. Reload MCP servers or open a new Codex session to use it."
