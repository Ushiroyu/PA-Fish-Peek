#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputPath) { $OutputPath = Join-Path $repoRoot 'artifacts/PA-Fish-Peek-direct-install.zip' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) { throw "Output already exists; choose a new -OutputPath: $OutputPath" }
$files = [Collections.Generic.List[IO.FileInfo]]::new()
foreach ($relative in @('.doorstop_version', 'doorstop_config.ini', 'winhttp.dll',
        'BepInEx/config/BepInEx.cfg', 'BepInEx/plugins/PAFishPeek/PAFishPeekPlugin.dll')) {
    $files.Add((Get-Item -LiteralPath (Join-Path $repoRoot $relative)))
}
foreach ($relative in @('BepInEx/core', 'BepInEx/interop', 'BepInEx/unity-libs', 'dotnet')) {
    $folder = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { throw "Missing folder: $relative" }
    foreach ($file in Get-ChildItem -LiteralPath $folder -File -Recurse -Force) {
        if ($file.Name -in @('MethodXrefScanCache.db', 'MethodAddressToToken.db', 'assembly-hash.txt') -or
            $file.Extension -in @('.log', '.dmp', '.pdb') -or
            $file.FullName -match '[\\/](cache|logs|_test_plugins_archive)[\\/]') { continue }
        $files.Add($file)
    }
}
$null = New-Item -ItemType Directory -Path (Split-Path $OutputPath -Parent) -Force
$archive = [IO.Compression.ZipFile]::Open($OutputPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $name = [IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\', '/')
        $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $archive.Dispose() }
& (Join-Path $PSScriptRoot 'check-package.ps1') -Path $OutputPath
Write-Output "Install package: $OutputPath"
