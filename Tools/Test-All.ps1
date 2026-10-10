<#
    Fast static checks, run by Deploy-Mod.ps1 before every deploy. Exits 1 on any failure.

    - Every translation key the code uses exists in the English keyed file, and every key in it is used.
    - XML patches gate optional content by def or package ID, never by mod display name.
    - Every texture the code loads with ContentFinder exists.

    The in-game combat suite is slower and separate: Tools\Run-CombatTest.ps1.
#>
[CmdletBinding()]
param(
    # Passed by Deploy-Mod.ps1; these checks only read the repo.
    [string] $RimWorldRoot
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repo 'Source'
$failures = New-Object System.Collections.Generic.List[string]

$code = Get-ChildItem $source -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }
$allCode = $code -join "`n"

# --- translation keys -----------------------------------------------------------
$keyedPath = Join-Path $repo 'Languages\English\Keyed\VanillaCombatOverhaul.xml'
$keyed = [xml][System.IO.File]::ReadAllText($keyedPath)
$defined = @{}
foreach ($node in $keyed.LanguageData.ChildNodes) {
    if ($node.NodeType -eq 'Element') { $defined[$node.Name] = $true }
}

$used = @{}
foreach ($m in [regex]::Matches($allCode, '"(VCO_[A-Za-z0-9_]+)"\s*\.Translate')) { $used[$m.Groups[1].Value] = $true }
foreach ($m in [regex]::Matches($allCode, '\?\s*"(VCO_[A-Za-z0-9_]+)"\s*:\s*"(VCO_[A-Za-z0-9_]+)"')) {
    $used[$m.Groups[1].Value] = $true
    $used[$m.Groups[2].Value] = $true
}
# Settings helpers translate the key and the key plus "_Tip".
foreach ($m in [regex]::Matches($allCode, '(?:Toggle|Slider|Section)\(l,\s*"(VCO_[A-Za-z0-9_]+)"')) {
    $used[$m.Groups[1].Value] = $true
    if ($m.Value -notmatch '^Section') { $used[$m.Groups[1].Value + '_Tip'] = $true }
}
# Keys stored in a variable and translated later: any VCO_ literal that names a defined key.
foreach ($m in [regex]::Matches($allCode, '"(VCO_[A-Za-z0-9_]+)"')) {
    if ($defined.ContainsKey($m.Groups[1].Value)) { $used[$m.Groups[1].Value] = $true }
}

foreach ($key in $used.Keys) {
    if (-not $defined.ContainsKey($key)) { $failures.Add("Missing translation key: $key") }
}
foreach ($key in $defined.Keys) {
    if (-not $used.ContainsKey($key)) { $failures.Add("Unused translation key: $key") }
}

# --- XML gating -----------------------------------------------------------------
Get-ChildItem (Join-Path $repo '1.6') -Recurse -Filter *.xml | ForEach-Object {
    $text = [System.IO.File]::ReadAllText($_.FullName)
    if ($text -match 'PatchOperationFindMod') {
        $failures.Add("$($_.Name): PatchOperationFindMod matches display names; gate by def (PatchOperationConditional) or package ID")
    }
}

# --- textures -------------------------------------------------------------------
$textureRoots = @('Textures', '1.6\Textures') | ForEach-Object { Join-Path $repo $_ } | Where-Object { Test-Path $_ }
foreach ($m in [regex]::Matches($allCode, 'ContentFinder<Texture2D>\.Get\("([^"]+)"')) {
    $path = $m.Groups[1].Value
    if ($path -notmatch '^UI/Commands/VCO_') { continue }
    $found = $textureRoots | Where-Object { Test-Path (Join-Path $_ ($path.Replace('/', '\') + '.png')) }
    if (-not $found) { $failures.Add("Missing texture: $path") }
}

if ($failures.Count -gt 0) {
    $failures | Sort-Object | ForEach-Object { Write-Host "FAIL  $_" }
    Write-Host "$($failures.Count) check(s) failed."
    exit 1
}
Write-Host "Static checks passed: $($used.Count) translation keys, XML gating, textures."
exit 0
