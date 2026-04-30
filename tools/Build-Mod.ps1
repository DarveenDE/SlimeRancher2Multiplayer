param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$NoSync
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "DevSettings.ps1")

if (-not $NoSync) {
    & (Join-Path $PSScriptRoot "Sync-GameRefs.ps1")
}

$dotnet = Get-DevDotnet
New-Item -ItemType Directory -Force -Path $Script:ArtifactsDir | Out-Null

& $dotnet restore $Script:ProjectPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed."
}

& $dotnet build $Script:ProjectPath --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed."
}

$dll = Join-Path $Script:ProjectDir "bin\$Configuration\net6.0\SR2MP.dll"
if (-not (Test-Path -LiteralPath $dll)) {
    throw "Build succeeded but SR2MP.dll was not found at $dll"
}

$artifactName = "SR2MP-$Configuration.dll"
$artifactPath = Join-Path $Script:ArtifactsDir $artifactName
Copy-Item -LiteralPath $dll -Destination $artifactPath -Force

Write-Host "Built $artifactPath"
