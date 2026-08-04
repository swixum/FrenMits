# Validates the baked fight sheets in src/Data/Sheets.
#
# The invariant: every DefaultAction names a real boss cast. A sheet's
# Timeline is the authoritative list of what the boss does; an action that
# points at a (Mechanic, Time) pair the Timeline doesn't carry is a typo, a
# stale time, or an invented mechanic, and it silently splits one row into
# two in the UI. The only exemption is an action marked "Hidden": true, which
# names a personal timer (a summoner's pet cycle) rather than a boss cast.
#
# Sheets in $Strict fail the build. Everything else is reported as a count so
# the remaining sheets can be cleaned up one at a time without blocking work.
#
# Run standalone:  powershell -ExecutionPolicy Bypass -File scripts/validate-sheets.ps1

[CmdletBinding()]
param(
    [string] $SheetsPath = '',
    # Sheets held to the full invariant. Add a sheet here once it is clean.
    [string[]] $Strict = @('DancingMad(UMAD)', 'Doomtrain', "Dragonsong'sReprise(DSR)", 'Enuo',
                           'EpicofAlexander(TEA)', 'FuturesRewritten(FRU)',
                           'M10S-RedHot_DeepBlue', 'M11S-TheTyrant', 'M12S-Lindwurm',
                           'M1S-BlackCat', 'M2S-HoneyB.Lovely', 'M3S-BruteBomber',
                           'M4S-WickedThunder', 'M5S-DancingGreen', 'M6S-SugarRiot',
                           'M7S-BruteAbombinator', 'M8S-HowlingBlade', 'M9S-VampFatale',
                           'TheOmegaProtocol(TOP)', 'UnendingCoilofBahamut(UCOB)',
                           "Weapon'sRefrain(UWU)", 'Zelenia')
)

$ErrorActionPreference = 'Stop'
$inv = [System.Globalization.CultureInfo]::InvariantCulture

function Format-Time([double] $t) { $t.ToString('0.###', $inv) }

if ([string]::IsNullOrWhiteSpace($SheetsPath)) {
    $root = Split-Path -Parent $MyInvocation.MyCommand.Definition
    $SheetsPath = Join-Path $root '..\src\Data\Sheets'
}

if (-not (Test-Path $SheetsPath)) {
    Write-Error "validate-sheets: sheets directory not found: $SheetsPath"
    exit 1
}

$failed = $false
$files = Get-ChildItem -Path $SheetsPath -Filter '*.json' | Sort-Object Name

foreach ($file in $files) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $isStrict = $Strict -contains $name

    try {
        $sheet = Get-Content -Path $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        Write-Host "$($file.Name): unparseable JSON - $($_.Exception.Message)"
        $failed = $true
        continue
    }

    # (Mechanic, Time) pairs the boss actually performs.
    $timeline = New-Object 'System.Collections.Generic.HashSet[string]'
    $timelineNames = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($e in @($sheet.Timeline)) {
        if ($null -eq $e) { continue }
        [void] $timeline.Add("$($e.Mechanic)|$(Format-Time ([double] $e.Time))")
        [void] $timelineNames.Add([string] $e.Mechanic)
    }

    $errors = New-Object 'System.Collections.Generic.List[string]'
    $seen = New-Object 'System.Collections.Generic.HashSet[string]'

    foreach ($a in @($sheet.DefaultActions)) {
        if ($null -eq $a) { continue }
        $mech = [string] $a.Mechanic
        $time = Format-Time ([double] $a.Time)
        $slot = if ($null -eq $a.Slot) { '' } else { [string] $a.Slot }
        $act = [string] $a.Action
        $where = "$time $(if ($slot) { $slot } else { '(job)' }) '$act'"

        if ([string]::IsNullOrWhiteSpace($mech)) {
            $errors.Add("$where has no Mechanic")
            continue
        }

        if ($a.Hidden -eq $true) {
            # Hidden is the escape hatch for non-mechanics only. Using it on a
            # real cast would quietly drop that cast off the mechanic list.
            if ($timelineNames.Contains($mech)) {
                $errors.Add("$where is Hidden but '$mech' is a real Timeline mechanic")
            }
            continue
        }

        if (-not $timeline.Contains("$mech|$time")) {
            $near = @($sheet.Timeline | Where-Object { $_.Mechanic -eq $mech } |
                ForEach-Object { Format-Time ([double] $_.Time) }) -join ', '
            $hint = if ($near) { "; '$mech' occurs at $near" } else { "; no Timeline entry named '$mech'" }
            $errors.Add("$where -> '$mech' @$time is not a Timeline mechanic$hint")
        }

        $key = "$time|$mech|$slot|$($act.Trim())|$(@($a.Jobs) -join ',')"
        if (-not $seen.Add($key)) { $errors.Add("$where duplicates an identical action") }
    }

    if ($errors.Count -eq 0) {
        if ($isStrict) { Write-Host "$($file.Name): ok ($(@($sheet.DefaultActions).Count) actions)" }
        continue
    }

    if ($isStrict) {
        $failed = $true
        Write-Host "$($file.Name): $($errors.Count) error(s)"
        foreach ($e in $errors) { Write-Host "    $e" }
    }
    else {
        Write-Host "$($file.Name): $($errors.Count) unvalidated mechanic(s) - not yet strict"
    }
}

if ($failed) {
    Write-Host ''
    Write-Host 'validate-sheets: FAILED. Every DefaultAction must match a Timeline (Mechanic, Time) pair,'
    Write-Host 'or carry "Hidden": true if it names a personal timer rather than a boss cast.'
    exit 1
}

exit 0
