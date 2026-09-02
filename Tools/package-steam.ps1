<#
.SYNOPSIS
    Builds a clean, Steam-uploadable copy of the mod.

.DESCRIPTION
    RimWorld uploads a mod from whatever is in its Mods folder, verbatim. Pointing that folder
    at the working tree is convenient while developing but would push the C# project, its obj/
    and bin/ intermediates, the test tooling and the git history straight into the Workshop
    item. This produces a folder containing only what a player needs.

    With -InstallToMods it also replaces the copy under RimWorld\Mods, so what sits there is
    always a distributable mod rather than a view of the source. Steam writes a
    PublishedFileId.txt into that folder on first upload; it is preserved across reinstalls,
    because losing it orphans the Workshop item and the next upload creates a duplicate.

.PARAMETER OutputPath
    Where to write the package. Defaults to Dist\VanillaCombatOverhaul beside the repo.

.PARAMETER SkipBuild
    Use the assembly already in 1.6\Assemblies rather than rebuilding.

.PARAMETER InstallToMods
    Also replace the copy in RimWorld\Mods with the freshly packaged one. The Mods folder then
    holds a distributable mod rather than a link to the working tree, so it carries no source,
    no build intermediates and no git history -- but it also stops tracking rebuilds, so rerun
    this after any code change you want to test in game.

.PARAMETER ModsPath
    RimWorld's Mods folder. Defaults to the usual Steam location.
#>
[CmdletBinding()]
param(
    [string] $OutputPath,
    [switch] $SkipBuild,
    [switch] $InstallToMods,
    [string] $ModsPath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'Dist\VanillaCombatOverhaul'
}

$project = Join-Path $repoRoot 'Source\VanillaCombatOverhaul\VanillaCombatOverhaul.csproj'
$assembly = Join-Path $repoRoot '1.6\Assemblies\VanillaCombatOverhaul.dll'

if (-not $SkipBuild) {
    Write-Host 'Building release assembly...'
    & dotnet build $project -c Release -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path $assembly)) {
    throw "No assembly at $assembly. Run without -SkipBuild."
}

# A stale DLL from an earlier name would be loaded too -- RimWorld loads every assembly in the
# folder, and both would apply their patches.
$strays = Get-ChildItem (Join-Path $repoRoot '1.6\Assemblies') -Filter *.dll |
          Where-Object { $_.Name -ne 'VanillaCombatOverhaul.dll' }
if ($strays) {
    throw "Unexpected assemblies alongside the mod DLL: $($strays.Name -join ', '). Remove them before packaging."
}

# Preserve PublishedFileId.txt across repackaging: losing it orphans the Workshop item and the
# next upload creates a duplicate rather than updating the original.
$publishedIdPath = Join-Path $OutputPath 'About\PublishedFileId.txt'
$publishedId = if (Test-Path $publishedIdPath) { Get-Content $publishedIdPath -Raw } else { $null }

if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$payload = @(
    @{ Path = 'About';          Required = $true  },
    @{ Path = '1.6';            Required = $true  },
    @{ Path = 'Languages';      Required = $true  },
    @{ Path = 'LoadFolders.xml';Required = $true  }
)

# README.md is deliberately absent. It is developer documentation -- build steps, test harness
# notes, and a critique of another author's mod -- none of which belongs in a Workshop item.

foreach ($item in $payload) {
    $source = Join-Path $repoRoot $item.Path
    if (-not (Test-Path $source)) {
        if ($item.Required) { throw "Missing required item: $($item.Path)" }
        continue
    }
    Copy-Item $source -Destination $OutputPath -Recurse -Force
}

# Anything that is build cruft, source control, or editor leftovers has no business in a
# Workshop item.
$purge = @('bin', 'obj', '.vs', '.git')
foreach ($name in $purge) {
    Get-ChildItem $OutputPath -Recurse -Force -Directory -Filter $name -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force }
}
Get-ChildItem $OutputPath -Recurse -Force -Include '*.pdb', '*.bak', '*.orig', '*.tmp', '*.user' -ErrorAction SilentlyContinue |
    Remove-Item -Force

if ($publishedId) {
    Set-Content -Path $publishedIdPath -Value $publishedId -NoNewline -Encoding ascii
    Write-Host 'Restored PublishedFileId.txt (updates the existing Workshop item).'
}

# Steam rejects preview images over 1 MB.
$preview = Join-Path $OutputPath 'About\Preview.png'
if (Test-Path $preview) {
    $kb = [math]::Round((Get-Item $preview).Length / 1KB, 1)
    if ((Get-Item $preview).Length -gt 1MB) {
        throw "Preview.png is $kb KB; Steam's limit is 1024 KB."
    }
    Write-Host "Preview.png: $kb KB (limit 1024 KB)"
} else {
    Write-Warning 'No About\Preview.png - the Workshop item will have no thumbnail.'
}

$size = [math]::Round(((Get-ChildItem $OutputPath -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 2)
Write-Host ''
Write-Host "Packaged to: $OutputPath"
Write-Host "Total size : $size MB"
if ($InstallToMods) {
    if (-not (Test-Path $ModsPath)) {
        throw "No Mods folder at $ModsPath. Pass -ModsPath."
    }

    $installed = Join-Path $ModsPath 'VanillaCombatOverhaul'

    if (Test-Path $installed) {
        $existing = Get-Item $installed -Force

        # Never Remove-Item a junction: depending on the PowerShell version it can follow the
        # link and delete the target, which here would be the working tree. rmdir removes the
        # reparse point itself and nothing behind it.
        if ($existing.LinkType) {
            Write-Host "Removing existing $($existing.LinkType) at $installed"
            & cmd.exe /c rmdir "`"$installed`""
            if (Test-Path $installed) { throw "Could not remove the link at $installed." }
        }
        else {
            # Carry the Workshop id across a reinstall, same as for the staging folder.
            $installedId = Join-Path $installed 'About\PublishedFileId.txt'
            if ((Test-Path $installedId) -and -not $publishedId) {
                $publishedId = Get-Content $installedId -Raw
            }
            Remove-Item $installed -Recurse -Force
        }
    }

    Copy-Item $OutputPath -Destination $installed -Recurse -Force

    if ($publishedId) {
        Set-Content -Path (Join-Path $installed 'About\PublishedFileId.txt') `
                    -Value $publishedId -NoNewline -Encoding ascii
    }

    Write-Host "Installed to: $installed"
}

Write-Host ''
Write-Host 'To publish: enable the mod in the in-game mod list and press Upload.'
Write-Host 'Keep About\PublishedFileId.txt afterwards -- it ties later uploads to the same item.'
if (-not $InstallToMods) {
    Write-Host 'Re-run with -InstallToMods to refresh the copy in RimWorld\Mods.'
}
