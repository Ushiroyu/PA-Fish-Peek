#requires -Version 7.0
[CmdletBinding()]
param([int]$Port = 8768)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$python = Join-Path $repoRoot '.venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $python)) {
    & python -m venv (Join-Path $repoRoot '.venv')
    if ($LASTEXITCODE -ne 0) { throw 'Python environment creation failed.' }
    & $python -m pip install -r (Join-Path $repoRoot 'assistant/requirements.lock')
    if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' }
    & $python -m pip install --no-deps -e (Join-Path $repoRoot 'assistant')
    if ($LASTEXITCODE -ne 0) { throw 'Workbench installation failed.' }
} else {
    & $python -c 'import fishpeek, fastapi, mcp' 2>$null
    if ($LASTEXITCODE -ne 0) {
        & $python -m pip install -r (Join-Path $repoRoot 'assistant/requirements.lock')
        if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' }
        & $python -m pip install --no-deps -e (Join-Path $repoRoot 'assistant')
        if ($LASTEXITCODE -ne 0) { throw 'Workbench installation failed.' }
    }
}
$env:PYTHONUTF8 = '1'
Push-Location (Join-Path $repoRoot 'assistant')
try { & $python -m fishpeek serve --port $Port }
finally { Pop-Location }
