# Local Development

This repo is set up to build and test against the locally installed Steam copy of Slime Rancher 2.

Expected game folder:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slime Rancher 2
```

The current local target is Slime Rancher 2 `1.2.0`, SR2E `3.7.0`, and MelonLoader `0.7.3-ci.2494 Open-Beta`.

If your Steam library is elsewhere, override the game folder before running the scripts:

```powershell
$env:SR2_GAME_DIR = "D:\SteamLibrary\steamapps\common\Slime Rancher 2"
```

If `dotnet` is not on `PATH`, you can also point the scripts at a local SDK:

```powershell
$env:DOTNET_EXE = "D:\dev\.dotnet\dotnet.exe"
```

## Build

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Mod.ps1 -Configuration Debug
```

This syncs reference DLLs from the installed game into `SR2MP\libraries`, restores packages, builds `SR2MP`, and copies the result to `artifacts\SR2MP-Debug.dll`.

## Install A Test Build

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Install-TestBuild.ps1 -Configuration Debug
```

This backs up the currently installed `Mods\SR2MP.dll` and replaces it with the latest local build.

## Build, Install, And Smoke Test

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Install-TestBuild.ps1 -Build -Configuration Debug -Launch -WaitSeconds 90
```

The smoke test launches the game, waits, then closes it and prints relevant MelonLoader log lines.

## Reference Dumps

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Generate-ReferenceDump.ps1
```

This writes local API reference dumps for `Assembly-CSharp.dll` and `SR2E.dll` to `reference-dumps\api`. The output folder is ignored by Git and can be regenerated whenever the game or SR2E reference DLLs change.

Runtime lookup tables can also be dumped from inside the game after the save context has loaded:

```text
refdump
```

By default this writes to `UserData\SR2MP\reference-dumps`. Set `SR2MP_REFERENCE_DUMP_DIR` or pass an output path to write elsewhere, for example `refdump D:\dev\SlimeRancher2Multiplayer\reference-dumps\runtime`.

## Notes

`SR2MP\libraries`, `artifacts`, and `reference-dumps` are ignored by Git. They are local machine state, not source code.

The local compatibility target has been moved to Slime Rancher 2 `1.2.0`. If Steam updates the game again, run the smoke test and check `MelonLoader\Latest.log` before making gameplay changes.

SR2E can still emit Il2CppInterop warnings on startup. The current SR2MP debug build loads past its startup/UI path on the local Slime Rancher 2 `1.2.0` install.
