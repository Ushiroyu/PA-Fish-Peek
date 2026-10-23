#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$GameRoot,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$UpdateBundledPlugin
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $GameRoot) { $GameRoot = $repoRoot }
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$project = Join-Path $repoRoot 'src/PAFishPeekPlugin/PAFishPeekPlugin.csproj'
foreach ($relative in @('BepInEx/core/BepInEx.Core.dll', 'BepInEx/interop/UnityEngine.CoreModule.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $GameRoot $relative))) {
        throw "Missing dependency: $relative in $GameRoot"
    }
}
& dotnet build $project -c $Configuration "-p:PA_GameRoot=$GameRoot" '-p:PAAutoDeploy=false' --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
$output = Join-Path $repoRoot "src/PAFishPeekPlugin/bin/$Configuration/net6.0/PAFishPeekPlugin.dll"
if (-not (Test-Path -LiteralPath $output)) { throw "Build output not found: $output" }
if ($UpdateBundledPlugin) {
    $destination = Join-Path $repoRoot 'BepInEx/plugins/PAFishPeek/PAFishPeekPlugin.dll'
    Copy-Item -LiteralPath $output -Destination $destination -Force
    Write-Output "Bundled plugin updated: $destination"
}
Write-Output "Build output: $output"
