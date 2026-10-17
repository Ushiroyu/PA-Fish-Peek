# PA Fish Peek

BepInEx IL2CPP plugin for inspecting fishing encounters in Party Animals.

The active implementation uses low-frequency fishing-controller discovery and main-thread reads of `fishJustEncounteredParam`. Native callback/setter detours and hot-update metadata probing remain disabled because that path reproduced an access violation in `GameAssembly.dll`.

## Build

The project references the local BepInEx and IL2CPP interop assemblies from the game installation. Set `PA_GameRoot` to the local Party Animals directory and keep deployment disabled during a build:

```powershell
dotnet build .\src\PAFishPeekPlugin\PAFishPeekPlugin.csproj -c Release -p:PA_GameRoot='C:\Path\To\Party Animals' -p:PAAutoDeploy=false
```

The resulting DLL is written to `src/PAFishPeekPlugin/bin/Release/net6.0/`.

## Direct-install package

The repository root is arranged as a direct Party Animals game-root package. It contains the Doorstop bootstrap files, matching CoreCLR runtime, game-specific BepInEx 6 IL2CPP runtime, Unity 2022.3.62 libraries, generated interop assemblies, and the active PA Fish Peek plugin DLL. The interop assemblies are tied to the Party Animals build and should be treated as a matching runtime pack rather than a generic BepInEx download. Logs, caches, disabled test plugins, and generated hash metadata are excluded.

Download the repository ZIP from GitHub, open the outer folder, and copy all of its contents into the Party Animals installation directory. The included `BepInEx`, `dotnet`, `winhttp.dll`, `doorstop_config.ini`, and `.doorstop_version` will then be in the locations expected by Doorstop. Do not overwrite a newer runtime without keeping a backup.

## Runtime

Copy the built DLL to `BepInEx/plugins/PAFishPeek/` in the game installation. In a fishing scene, press `F10` to enable prediction output and `F11` to change the encounter display scope.

The plugin reads current fishing resources from the game installation when prediction is enabled. The repository does not include the game executable, `GameAssembly.dll`, extracted resources, crash dumps, or logs. It does include the game-specific BepInEx and CoreCLR runtime binaries required by the matching installation.

## Validation

The data and weather audit is in [`docs/fishpeek-data-audit-20261017.md`](docs/fishpeek-data-audit-20261017.md). The call flow is in [`docs/fishpeek-callflow.mmd`](docs/fishpeek-callflow.mmd).
