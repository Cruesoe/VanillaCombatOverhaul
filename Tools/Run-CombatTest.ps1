<#
    Runs the in-game combat test suite headlessly against the deployed mod and reports pass/fail.

    RimWorld starts with -savedatafolder and -logFile pointing at a throwaway folder, so the run has
    its own config, mod list and log. Your real ModsConfig.xml, saves and Player.log are not touched,
    so it is safe to run while you are playing.

    A run fails if any check fails or if the game logs an exception.

    Exit codes:  0 = all checks passed, no exceptions
                 1 = a check failed or an exception was logged
                 2 = no report (crash, timeout, bad setup or stale deploy)

    Usage:
        .\Run-CombatTest.ps1                  # unseeded
        .\Run-CombatTest.ps1 -Seed 12345      # pinned seed, for regression checks
        .\Run-CombatTest.ps1 -KeepSandbox     # keep the folder with Player.log and results
#>
[CmdletBinding()]
param(
    [int]    $Seed = 0,
    [int]    $TimeoutMinutes = 12,
    [string] $RimWorldPath = $(if ($env:RimWorldRoot) { $env:RimWorldRoot } elseif ($env:RIMWORLD_DIR) { $env:RIMWORLD_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld' }),
    [switch] $KeepSandbox
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $RimWorldPath 'RimWorldWin64.exe'
if (-not (Test-Path $exe)) { Write-Error "RimWorld not found at $exe"; exit 2 }

# --- the deployed copy must be the build in this repo -------------------------
$repo = Split-Path $PSScriptRoot -Parent
$dll = Join-Path $repo '1.6\Assemblies\VanillaCombatOverhaul.dll'
if (-not (Test-Path $dll)) { Write-Error "Mod assembly not built: $dll"; exit 2 }

$packageId = ([xml](Get-Content (Join-Path $repo 'About\About.xml') -Raw)).ModMetaData.packageId
$deployed = Get-ChildItem (Join-Path $RimWorldPath 'Mods') -Directory | Where-Object {
    $about = Join-Path $_.FullName 'About\About.xml'
    (Test-Path $about) -and ([xml](Get-Content $about -Raw)).ModMetaData.packageId -eq $packageId
} | Select-Object -First 1
if (-not $deployed) { Write-Error "No deployed copy of $packageId. Deploy with Deploy-Mod.ps1 first."; exit 2 }
$deployedDll = Join-Path $deployed.FullName '1.6\Assemblies\VanillaCombatOverhaul.dll'
if (-not (Test-Path $deployedDll) -or (Get-FileHash $dll).Hash -ne (Get-FileHash $deployedDll).Hash) {
    Write-Error "The deployed assembly differs from the repo build. Deploy with Deploy-Mod.ps1 first."
    exit 2
}

# --- throwaway save-data folder -------------------------------------------------
$sandbox = Join-Path $env:TEMP "vco-test-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$configDir = Join-Path $sandbox 'Config'
New-Item -ItemType Directory -Path $configDir -Force | Out-Null

# Prefs from the real install avoid first-launch prompts; the game version is read from the real mod list.
$realConfig = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config'
foreach ($f in @('Prefs.xml', 'KeyPrefs.xml')) {
    $src = Join-Path $realConfig $f
    if (Test-Path $src) { Copy-Item $src (Join-Path $configDir $f) -Force }
}
$version = '1.6'
$realMods = Join-Path $realConfig 'ModsConfig.xml'
if (Test-Path $realMods) { $version = ([xml](Get-Content $realMods -Raw)).ModsConfigData.version }

$mods = @('brrainz.harmony', 'ludeon.rimworld', 'ludeon.rimworld.royalty', 'ludeon.rimworld.ideology',
          'ludeon.rimworld.biotech', 'ludeon.rimworld.anomaly', 'ludeon.rimworld.odyssey', $packageId.ToLowerInvariant())
$modsXml = "<?xml version=`"1.0`" encoding=`"utf-8`"?>`r`n<ModsConfigData>`r`n  <version>$version</version>`r`n  <activeMods>`r`n"
foreach ($m in $mods) { $modsXml += "    <li>$m</li>`r`n" }
$modsXml += "  </activeMods>`r`n</ModsConfigData>`r`n"
[System.IO.File]::WriteAllText((Join-Path $configDir 'ModsConfig.xml'), $modsXml)

# The trigger file is what the mod watches for; its contents are the RNG seed.
[System.IO.File]::WriteAllText((Join-Path $configDir 'vco_autotest.trigger'), "$Seed")
$resultPath = Join-Path $configDir 'vco_autotest_results.txt'
$logPath = Join-Path $sandbox 'Player.log'

Write-Host "Sandbox : $sandbox"
Write-Host "Seed    : $(if ($Seed -eq 0) { 'unseeded' } else { $Seed })"
Write-Host 'Launching RimWorld...'

$exitCode = 2
try {
    $proc = Start-Process -FilePath $exe -ArgumentList "-savedatafolder=`"$sandbox`"", '-logFile', "`"$logPath`"" -WindowStyle Hidden -PassThru
    if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        Write-Warning "Timed out after $TimeoutMinutes minutes; terminating."
        try { $proc.Kill() } catch { }
        Start-Sleep -Seconds 2
    }

    if (Test-Path $resultPath) {
        $report = Get-Content $resultPath -Raw
        $report -split "`r?`n" | Where-Object { $_ -cmatch '^\s*FAIL\s|^=== |^RESULT' } | ForEach-Object { Write-Host $_ }
        if ($report -match 'RESULT:\s*PASS') { $exitCode = 0 }
        elseif ($report -match 'RESULT:\s*FAIL') { $exitCode = 1 }
        else { Write-Warning 'Report present but no RESULT line found.' }
    }
    else {
        Write-Warning "No results file - the run crashed or never reached the map."
    }

    if (-not (Test-Path $logPath)) {
        Write-Warning "No Player.log at $logPath, so exceptions could not be checked."
        $exitCode = 2
    }
    else {
        Write-Host "Player.log: $((Get-Content $logPath).Count) lines checked for exceptions."
        $exceptions = Select-String -Path $logPath -Pattern 'Exception' |
            Where-Object { $_.Line -notmatch 'Duplicate stacktrace' } |
            ForEach-Object { $_.Line.Trim() } | Group-Object | Sort-Object Count -Descending
        if ($exceptions) {
            Write-Host ''
            Write-Host 'Exceptions logged during the run:'
            $exceptions | ForEach-Object { Write-Host ("  {0,4} x {1}" -f $_.Count, $_.Name) }
            if ($exitCode -eq 0) { $exitCode = 1 }
        }
        if ($exitCode -eq 2) { Write-Host '--- tail of Player.log ---'; Get-Content $logPath -Tail 40 }
    }
}
finally {
    if ($KeepSandbox -or $exitCode -ne 0) {
        Write-Host "Sandbox kept at $sandbox"
    }
    else {
        Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ''
Write-Host ("Exit code {0} ({1})" -f $exitCode, @{ 0 = 'all checks passed'; 1 = 'a check failed or an exception was logged'; 2 = 'no report' }[$exitCode])
exit $exitCode
