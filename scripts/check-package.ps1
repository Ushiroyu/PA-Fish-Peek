#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$required = @('.doorstop_version', 'doorstop_config.ini', 'winhttp.dll', 'dotnet/coreclr.dll',
    'BepInEx/config/BepInEx.cfg', 'BepInEx/core/BepInEx.Core.dll',
    'BepInEx/core/BepInEx.Unity.IL2CPP.dll', 'BepInEx/interop/UnityEngine.CoreModule.dll',
    'BepInEx/plugins/PAFishPeek/PAFishPeekPlugin.dll')
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Path).Path)
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$config = $null
try {
    foreach ($entry in $archive.Entries) {
        $name = $entry.FullName
        if (-not $seen.Add($name)) { throw "Duplicate ZIP path: $name" }
        if ($name.Contains('\') -or $name.StartsWith('/') -or $name.Contains(':') -or
            $name.Split('/') -contains '..') { throw "Invalid ZIP path: $name" }
        $allowed = $name -in @('.doorstop_version', 'doorstop_config.ini', 'winhttp.dll', 'BepInEx/config/BepInEx.cfg',
            'BepInEx/plugins/PAFishPeek/PAFishPeekPlugin.dll') -or
            $name -match '^(BepInEx/(core|interop|unity-libs)/|dotnet/)'
        if (-not $allowed -or $name -match '(?i)(\.(log|dmp|pdb)$|MethodXrefScanCache\.db|MethodAddressToToken\.db|assembly-hash\.txt|/(cache|logs|_test_plugins_archive)/)') {
            throw "Unexpected package entry: $name"
        }
        $stream = $entry.Open()
        try {
            if ($name -eq 'doorstop_config.ini') {
                $reader = [IO.StreamReader]::new($stream)
                try { $config = $reader.ReadToEnd() } finally { $reader.Dispose() }
            } else { $stream.CopyTo([IO.Stream]::Null) }
        } finally { $stream.Dispose() }
    }
    foreach ($name in $required) {
        if (-not $seen.Contains($name)) { throw "Missing required entry: $name" }
        if ($archive.GetEntry($name).Length -eq 0) { throw "Empty required file: $name" }
    }
    if ($config -notmatch '(?m)^\s*enabled\s*=\s*true\s*$' -or
        $config -notmatch '(?m)^\s*target_assembly\s*=\s*BepInEx\\core\\BepInEx.Unity.IL2CPP.dll\s*$' -or
        $config -notmatch '(?m)^\s*coreclr_path\s*=\s*dotnet\\coreclr.dll\s*$' -or
        $config -notmatch '(?m)^\s*corlib_dir\s*=\s*dotnet\s*$') {
        throw 'Doorstop configuration does not point to the bundled runtime.'
    }
    Write-Output "Package check passed: $($archive.Entries.Count) entries; required files and bootstrap paths present."
} finally { $archive.Dispose() }
