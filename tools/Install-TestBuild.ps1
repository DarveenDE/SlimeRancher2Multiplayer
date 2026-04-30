param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Build,
    [switch]$Launch,
    [int]$WaitSeconds = 90
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "DevSettings.ps1")

Assert-GameInstall

if ($Build) {
    & (Join-Path $PSScriptRoot "Build-Mod.ps1") -Configuration $Configuration
}

$artifactPath = Join-Path $Script:ArtifactsDir "SR2MP-$Configuration.dll"
if (-not (Test-Path -LiteralPath $artifactPath)) {
    throw "No test build found at $artifactPath. Run tools\Build-Mod.ps1 first, or pass -Build."
}

$modsDir = Join-Path $Script:GameDir "Mods"
$target = Join-Path $modsDir "SR2MP.dll"
$backupDir = Join-Path $Script:ArtifactsDir ("installed-backups\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

if (Test-Path -LiteralPath $target) {
    Copy-Item -LiteralPath $target -Destination (Join-Path $backupDir "SR2MP.dll") -Force
}

Copy-Item -LiteralPath $artifactPath -Destination $target -Force
Write-Host "Installed test build to $target"
Write-Host "Previous DLL backup: $backupDir"

if ($Launch) {
    $process = Start-Process -FilePath (Join-Path $Script:GameDir "SlimeRancher2.exe") -WorkingDirectory $Script:GameDir -PassThru
    Start-Sleep -Seconds $WaitSeconds
    $stillRunning = -not $process.HasExited
    if ($stillRunning) {
        Stop-Process -Id $process.Id -Force
        Start-Sleep -Seconds 2
    }

    $latestLog = Join-Path $Script:GameDir "MelonLoader\Latest.log"
    Write-Host "Launch test: StillRunningAfter${WaitSeconds}s=$stillRunning ExitCode=$($process.ExitCode)"
    if (Test-Path -LiteralPath $latestLog) {
        Select-String -LiteralPath $latestLog -Pattern "SR2MP|Slime Rancher 2 Multiplayer|error|exception|failed|crash" -CaseSensitive:$false | Select-Object -Last 40
    }
}
