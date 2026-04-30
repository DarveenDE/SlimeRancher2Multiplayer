$Script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Script:ProjectPath = Join-Path $Script:RepoRoot "SR2MP\SR2MP.csproj"
$Script:ProjectDir = Split-Path -Parent $Script:ProjectPath
$Script:LibrariesDir = Join-Path $Script:ProjectDir "libraries"
$Script:ArtifactsDir = Join-Path $Script:RepoRoot "artifacts"
$Script:GameDir = if ($env:SR2_GAME_DIR) {
    $env:SR2_GAME_DIR
} else {
    "C:\Program Files (x86)\Steam\steamapps\common\Slime Rancher 2"
}

$Script:LocalDotnet = if ($env:DOTNET_EXE) {
    $env:DOTNET_EXE
} else {
    Join-Path (Split-Path -Parent $Script:RepoRoot) ".dotnet\dotnet.exe"
}

function Get-DevDotnet {
    if (Test-Path -LiteralPath $Script:LocalDotnet) {
        return $Script:LocalDotnet
    }

    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        return $dotnetCommand.Source
    }

    throw "No dotnet SDK was found. Expected local SDK at $Script:LocalDotnet."
}

function Assert-GameInstall {
    if (-not (Test-Path -LiteralPath (Join-Path $Script:GameDir "SlimeRancher2.exe"))) {
        throw "Slime Rancher 2 was not found at $Script:GameDir"
    }

    if (-not (Test-Path -LiteralPath (Join-Path $Script:GameDir "MelonLoader\net6\MelonLoader.dll"))) {
        throw "MelonLoader net6 files were not found in the game folder."
    }

    if (-not (Test-Path -LiteralPath (Join-Path $Script:GameDir "MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"))) {
        throw "MelonLoader Il2CppAssemblies are missing. Start the game once with the installed MelonLoader version before building."
    }

    if (-not (Test-Path -LiteralPath (Join-Path $Script:GameDir "Mods\SR2E.dll"))) {
        throw "SR2E.dll was not found in the game's Mods folder."
    }
}
