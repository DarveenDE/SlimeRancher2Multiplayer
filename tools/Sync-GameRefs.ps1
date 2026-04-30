$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "DevSettings.ps1")

Assert-GameInstall

if (Test-Path -LiteralPath $Script:LibrariesDir) {
    Remove-Item -LiteralPath $Script:LibrariesDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $Script:LibrariesDir | Out-Null

function Copy-DllsFrom {
    param(
        [Parameter(Mandatory=$true)][string]$SourceDir,
        [string[]]$Exclude = @()
    )

    if (-not (Test-Path -LiteralPath $SourceDir)) {
        return
    }

    Get-ChildItem -LiteralPath $SourceDir -Filter "*.dll" -File | Where-Object {
        $Exclude -notcontains $_.Name
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Script:LibrariesDir $_.Name) -Force
    }
}

Copy-DllsFrom -SourceDir (Join-Path $Script:GameDir "MelonLoader\net6") -Exclude @("DiscordRPC.dll")
Copy-DllsFrom -SourceDir (Join-Path $Script:GameDir "MelonLoader\Dependencies\SupportModules")
Copy-DllsFrom -SourceDir (Join-Path $Script:GameDir "MelonLoader\Il2CppAssemblies")
Copy-Item -LiteralPath (Join-Path $Script:GameDir "Mods\SR2E.dll") -Destination (Join-Path $Script:LibrariesDir "SR2E.dll") -Force

$count = (Get-ChildItem -LiteralPath $Script:LibrariesDir -Filter "*.dll" -File).Count
Write-Host "Synced $count reference DLLs to $Script:LibrariesDir"
