<#
.SYNOPSIS
    Builds a clean, Steam-uploadable copy of the mod.

.DESCRIPTION
    RimWorld uploads a mod from whatever is in its Mods folder, verbatim. This repo doubles as
    that folder via a directory junction, which means an upload from here would carry the C#
    project, its obj/ and bin/ intermediates, the test tooling and the git history into the
    Workshop item. This produces a folder containing only what a player needs.

    The result is a normal mod folder. To publish it, move or junction it into
    RimWorld\Mods\, tick it in the mod list, and use the in-game Upload button. Steam writes a
    PublishedFileId.txt into the folder on first upload; keep that file, because it is what
    ties later uploads to the same Workshop item.

.PARAMETER OutputPath
    Where to write the package. Defaults to Dist\VanillaCombatOverhaul beside the repo.

.PARAMETER SkipBuild
    Use the assembly already in 1.6\Assemblies rather than rebuilding.
#>
[CmdletBinding()]
param(
    [string] $OutputPath,
    [switch] $SkipBuild
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
    @{ Path = 'LoadFolders.xml';Required = $true  },
    @{ Path = 'README.md';      Required = $false }
)

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
Write-Host ''
Write-Host 'To publish: copy or junction this folder into RimWorld\Mods, enable it in the'
Write-Host 'mod list, and press Upload. Keep About\PublishedFileId.txt afterwards.'
