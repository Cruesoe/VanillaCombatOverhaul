<#
    Runs the in-game combat test suite against the deployed mod on RimWorld 1.7 and reports pass/fail.

    RimWorld starts with -savedatafolder pointing at a throwaway folder, so the run has its own config
    and saves, and with -mod naming the mods to load, so your real ModsConfig.xml is not touched.
    1.7 always writes Player.log to the real LocalLow\...\Logs folder (and renames the previous one to
    Player-prev.log on start), so the run passes -NoFileLog and captures the game's console log from
    standard output into the sandbox instead. Your saves and Player.log are left alone.

    A run fails if any check fails, if the game logs an exception, or if a [UnityStubs] warning names
    this mod's code.

    Exit codes:  0 = all checks passed, no exceptions
                 1 = a check failed or an exception was logged
                 2 = no report (crash, timeout, bad setup or stale deploy)

    Usage:
        .\Run-CombatTest.ps1                  # unseeded
        .\Run-CombatTest.ps1 -Seed 12345      # pinned seed, for regression checks
        .\Run-CombatTest.ps1 -KeepSandbox     # keep the folder with the log and results
        .\Run-CombatTest.ps1 -ExtraMods ferny.ProgressionAmmunition   # also load other installed mods by package ID
#>
[CmdletBinding()]
param(
    [int]    $Seed = 0,
    [int]    $TimeoutMinutes = 12,
    [string] $RimWorldPath = $(if ($env:RimWorldRoot) { $env:RimWorldRoot } elseif ($env:RIMWORLD_DIR) { $env:RIMWORLD_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld' }),
    [switch] $KeepSandbox,
    [string[]] $ExtraMods = @()
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $RimWorldPath 'RimWorld.exe'
if (-not (Test-Path $exe)) { Write-Error "RimWorld 1.7 not found at $exe"; exit 2 }

# --- the deployed copy must be the build in this repo -------------------------
$repo = Split-Path $PSScriptRoot -Parent
$dllName = 'VanillaCombatOverhaul.dll'
# Each version folder with a built assembly must match its deployed copy; 1.7 loads the 1.6 build until the mod has its own.
$builds = @('1.6', '1.7') | ForEach-Object { Join-Path $repo "$_\Assemblies\$dllName" } | Where-Object { Test-Path $_ }
if (-not $builds) { Write-Error "Mod assembly not built under $repo\1.6 or 1.7"; exit 2 }

$packageId = ([xml](Get-Content (Join-Path $repo 'About\About.xml') -Raw)).ModMetaData.packageId
$deployed = Get-ChildItem (Join-Path $RimWorldPath 'Mods') -Directory | Where-Object {
    $about = Join-Path $_.FullName 'About\About.xml'
    (Test-Path $about) -and ([xml](Get-Content $about -Raw)).ModMetaData.packageId -eq $packageId
} | Select-Object -First 1
if (-not $deployed) { Write-Error "No deployed copy of $packageId. Deploy with Deploy-Mod.ps1 first."; exit 2 }
foreach ($dll in $builds) {
    $deployedDll = Join-Path $deployed.FullName $dll.Substring($repo.Length + 1)
    if (-not (Test-Path $deployedDll) -or (Get-FileHash $dll).Hash -ne (Get-FileHash $deployedDll).Hash) {
        Write-Error "The deployed $($dll.Substring($repo.Length + 1)) differs from the repo build. Deploy with Deploy-Mod.ps1 first."
        exit 2
    }
}

# --- throwaway save-data folder -------------------------------------------------
$sandbox = Join-Path $env:TEMP "vco-test-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$configDir = Join-Path $sandbox 'Config'
New-Item -ItemType Directory -Path $configDir -Force | Out-Null

# Prefs from the real install avoid first-launch prompts.
$realConfig = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config'
foreach ($f in @('Prefs.xml', 'KeyPrefs.xml')) {
    $src = Join-Path $realConfig $f
    if (Test-Path $src) { Copy-Item $src (Join-Path $configDir $f) -Force }
}

# -mod takes a comma-separated list; Core and the DLCs are added automatically.
$mods = @('brrainz.harmony') + $ExtraMods + @($packageId)
$modArg = '-mod=' + (($mods | ForEach-Object { $_.ToLowerInvariant() }) -join ',')

# The trigger file is what the mod watches for; its contents are the RNG seed.
[System.IO.File]::WriteAllText((Join-Path $configDir 'vco_autotest.trigger'), "$Seed")
$resultPath = Join-Path $configDir 'vco_autotest_results.txt'
$logPath = Join-Path $sandbox 'game.log'
$errPath = Join-Path $sandbox 'game-stderr.log'

Write-Host "Sandbox : $sandbox"
Write-Host "Seed    : $(if ($Seed -eq 0) { 'unseeded' } else { $Seed })"
Write-Host "Mods    : $($mods -join ', ')"
Write-Host 'Launching RimWorld...'

$exitCode = 2
try {
    $proc = Start-Process -FilePath $exe -ArgumentList "-savedatafolder=`"$sandbox`"", $modArg, '-NoFileLog' `
        -WindowStyle Minimized -RedirectStandardOutput $logPath -RedirectStandardError $errPath -PassThru
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

    $logs = @($logPath, $errPath) | Where-Object { (Test-Path $_) -and (Get-Item $_).Length -gt 0 }
    if (-not $logs) {
        Write-Warning "The game wrote no console log to $logPath, so exceptions could not be checked."
        $exitCode = 2
    }
    else {
        Write-Host "Game log: $(($logs | ForEach-Object { (Get-Content $_).Count } | Measure-Object -Sum).Sum) lines checked."
        # Exceptions anywhere, and stub warnings raised from this mod's code.
        $problems = Select-String -Path $logs -Pattern 'Exception', 'WARN \[UnityStubs\].*VanillaCombatOverhaul' |
            Where-Object { $_.Line -notmatch 'Duplicate stacktrace' } |
            ForEach-Object { $_.Line.Trim() } | Group-Object | Sort-Object Count -Descending
        if ($problems) {
            Write-Host ''
            Write-Host 'Problems logged during the run:'
            $problems | ForEach-Object { Write-Host ("  {0,4} x {1}" -f $_.Count, $_.Name) }
            if ($exitCode -eq 0) { $exitCode = 1 }
        }
        if ($exitCode -eq 2) { Write-Host '--- tail of the game log ---'; Get-Content $logPath -Tail 40 }
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
