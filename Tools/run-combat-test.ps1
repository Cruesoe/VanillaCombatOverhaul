<#
    Runs the VCO combat test suite headlessly and reports pass/fail.

    RimWorld is launched with -savedatafolder pointing at a throwaway directory, so the run
    gets its own config and mod list. The player's real ModsConfig.xml, prefs and saves are
    never touched, which is what makes this safe to run on a whim or from CI.

    Exit codes:  0 = all checks passed
                 1 = a check failed
                 2 = the run did not produce a report (crash, timeout, or bad setup)

    Usage:
        .\run-combat-test.ps1
        .\run-combat-test.ps1 -Seed 12345      # reproducible, for regression checks
        .\run-combat-test.ps1 -TimeoutMinutes 10
#>
[CmdletBinding()]
param(
    [int]    $Seed = 0,
    [int]    $TimeoutMinutes = 8,
    [string] $RimWorldPath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [switch] $KeepSandbox
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $RimWorldPath 'RimWorldWin64.exe'
if (-not (Test-Path $exe)) { Write-Error "RimWorld not found at $exe"; exit 2 }

$repo = Split-Path $PSScriptRoot -Parent
$dll  = Join-Path $repo '1.6\Assemblies\VanillaCombatOverhaul.dll'
if (-not (Test-Path $dll)) {
    Write-Error "Mod assembly not built. Run: dotnet build $repo\Source\VanillaCombatOverhaul -c Release"
    exit 2
}

# --- throwaway save-data folder -------------------------------------------------
$sandbox   = Join-Path $env:TEMP "vco-test-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$configDir = Join-Path $sandbox 'Config'
New-Item -ItemType Directory -Path $configDir -Force | Out-Null

# Seed prefs from the real install so the run does not hit first-launch prompts.
$realConfig = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config'
foreach ($f in @('Prefs.xml', 'KeyPrefs.xml')) {
    $src = Join-Path $realConfig $f
    if (Test-Path $src) { Copy-Item $src (Join-Path $configDir $f) -Force }
}

@'
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>1.6.4871 rev591</version>
  <activeMods>
    <li>brrainz.harmony</li>
    <li>ludeon.rimworld</li>
    <li>ludeon.rimworld.royalty</li>
    <li>ludeon.rimworld.ideology</li>
    <li>ludeon.rimworld.biotech</li>
    <li>ludeon.rimworld.anomaly</li>
    <li>ludeon.rimworld.odyssey</li>
    <li>cruesoe.vanillacombatoverhaul</li>
  </activeMods>
</ModsConfigData>
'@ | Set-Content (Join-Path $configDir 'ModsConfig.xml') -Encoding utf8

# The trigger file is what the mod watches for; its contents are the RNG seed.
$triggerPath = Join-Path $configDir 'vco_autotest.trigger'
"$Seed" | Set-Content $triggerPath -Encoding utf8
$resultPath = Join-Path $configDir 'vco_autotest_results.txt'

Write-Host "Sandbox : $sandbox"
Write-Host "Seed    : $(if ($Seed -eq 0) { 'unseeded' } else { $Seed })"
Write-Host "Launching RimWorld..."

$exitCode = 2
try {
    $proc = Start-Process -FilePath $exe -ArgumentList "-savedatafolder=$sandbox" -PassThru
    if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        Write-Warning "Timed out after $TimeoutMinutes minutes; terminating."
        try { $proc.Kill() } catch { }
        Start-Sleep -Seconds 2
    }

    if (Test-Path $resultPath) {
        $report = Get-Content $resultPath -Raw
        Write-Host ''
        Write-Host $report
        if ($report -match 'RESULT:\s*PASS') { $exitCode = 0 }
        elseif ($report -match 'RESULT:\s*FAIL') { $exitCode = 1 }
        else { Write-Warning 'Report present but no RESULT line found.'; $exitCode = 2 }
    }
    else {
        Write-Warning "No results file at $resultPath - the run crashed or never reached the map."
        $log = Join-Path $sandbox 'Player.log'
        if (Test-Path $log) { Write-Host "--- tail of Player.log ---"; Get-Content $log -Tail 40 }
        $exitCode = 2
    }
}
finally {
    if ($KeepSandbox) {
        Write-Host "Sandbox kept at $sandbox"
    }
    else {
        try { Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    }
}

Write-Host ''
Write-Host ("Exit code {0} ({1})" -f $exitCode, @{0='all checks passed';1='a check failed';2='no report'}[$exitCode])
exit $exitCode
