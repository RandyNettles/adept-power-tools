param(
    [string]$ExePath = ".\publish\adept-tool.exe",
    [string]$Server,
    [string]$HttpServer,
    [string]$User = "ADM",
    [string[]]$Backends = @("com"),
    [string]$OutputDir = ".\artifacts\perf",
    [string]$BulkDeleteFilter = "*",
    [int]$BulkDeleteTargetCount = 600,
    [string]$SingleCreateXmlPath,
    [string]$ModifyXmlPath,
    [string]$CreateXmlPath,
    [switch]$IncludeSingleCreateSmoke,
    [switch]$RunLiveMutations,
    [int]$WarmupRuns = 1,
    [int]$MeasuredRuns = 3
)

$ErrorActionPreference = "Stop"

function Assert-PathExists {
    param([string]$PathToCheck, [string]$Label)
    if (-not (Test-Path -LiteralPath $PathToCheck)) {
        throw "$Label not found: $PathToCheck"
    }
}

function Get-Percentile {
    param([double[]]$Values, [double]$Percentile)
    if (-not $Values -or $Values.Count -eq 0) { return 0 }

    $sorted = $Values | Sort-Object
    if ($sorted.Count -eq 1) { return [double]$sorted[0] }

    $rank = ($Percentile / 100.0) * ($sorted.Count - 1)
    $low = [int][Math]::Floor($rank)
    $high = [int][Math]::Ceiling($rank)

    if ($low -eq $high) { return [double]$sorted[$low] }

    $weight = $rank - $low
    return [double]$sorted[$low] + ($weight * ([double]$sorted[$high] - [double]$sorted[$low]))
}

function Invoke-TimedCli {
    param(
        [string]$Scenario,
        [string[]]$Arguments,
        [int]$Iteration,
        [string]$OutDir
    )

    $stdoutPath = Join-Path $OutDir ("{0}-run{1}-stdout.txt" -f $Scenario, $Iteration)
    $stderrPath = Join-Path $OutDir ("{0}-run{1}-stderr.txt" -f $Scenario, $Iteration)

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    $process = Start-Process -FilePath $ExePath `
        -ArgumentList $Arguments `
        -NoNewWindow `
        -Wait `
        -PassThru `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $sw.Stop()

    [pscustomobject]@{
        Scenario = $Scenario
        Iteration = $Iteration
        ExitCode = $process.ExitCode
        DurationMs = [Math]::Round($sw.Elapsed.TotalMilliseconds, 2)
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
    }
}

function Summarize-Scenario {
    param(
        [string]$Backend,
        [string]$Scenario,
        [array]$Rows,
        [int]$OperationCount = 1
    )

    $durations = @($Rows | ForEach-Object { [double]$_.DurationMs })
    $failed = @($Rows | Where-Object { $_.ExitCode -ne 0 }).Count

    $p50 = [Math]::Round((Get-Percentile -Values $durations -Percentile 50), 2)
    $p95 = [Math]::Round((Get-Percentile -Values $durations -Percentile 95), 2)
    $max = [Math]::Round((($durations | Measure-Object -Maximum).Maximum), 2)
    $min = [Math]::Round((($durations | Measure-Object -Minimum).Minimum), 2)
    $avg = [Math]::Round((($durations | Measure-Object -Average).Average), 2)

    $opsPerSec = 0
    if ($p95 -gt 0) {
        $opsPerSec = [Math]::Round(($OperationCount / ($p95 / 1000.0)), 3)
    }

    [pscustomobject]@{
        Backend = $Backend
        Scenario = $Scenario
        Runs = $Rows.Count
        Failures = $failed
        MinMs = $min
        AvgMs = $avg
        P50Ms = $p50
        P95Ms = $p95
        MaxMs = $max
        EstimatedOpsPerSecAtP95 = $opsPerSec
    }
}

function Get-DbDirectSignal {
    param([array]$SummaryRows)

    $signals = New-Object System.Collections.Generic.List[string]

    foreach ($row in $SummaryRows) {
        if ($row.Failures -gt 0) {
            $signals.Add("$($row.Backend)/$($row.Scenario): non-zero failures observed; stabilize correctness before DB-direct optimization.")
        }

        if ($row.Scenario -eq "bulk-delete-600" -and $row.P95Ms -gt 180000) {
            $signals.Add("$($row.Backend)/bulk-delete-600: p95 > 180s. Evaluate DB-assisted candidate selection or chunked server-side filtering.")
        }

        if ($row.Scenario -eq "modify-1-replace-60-trustees" -and $row.P95Ms -gt 30000) {
            $signals.Add("$($row.Backend)/modify-1-replace-60-trustees: p95 > 30s. Profile trustee resolution and post-save verification; consider DB-direct read optimization only if backend round-trips dominate.")
        }

        if ($row.Scenario -eq "create-40-batch" -and $row.P95Ms -gt 120000) {
            $signals.Add("$($row.Backend)/create-40-batch: p95 > 120s. Evaluate batched metadata lookup strategy before any DB-direct write path.")
        }
    }

    if ($signals.Count -eq 0) {
        $signals.Add("No coarse threshold breach detected. Keep COM service path and continue deeper profiling before DB-direct work.")
    }

    return $signals
}

function Get-ServerForBackend {
    param([string]$BackendName)

    switch ($BackendName.ToLowerInvariant()) {
        "com" {
            if ([string]::IsNullOrWhiteSpace($Server)) {
                throw "-Server is required when running COM backend"
            }

            return $Server
        }
        "http" {
            if (-not [string]::IsNullOrWhiteSpace($HttpServer)) {
                return $HttpServer
            }

            if ([string]::IsNullOrWhiteSpace($Server)) {
                throw "-HttpServer or -Server is required when running HTTP backend"
            }

            return $Server
        }
        default {
            throw "Unsupported backend '$BackendName'. Allowed values: com, http"
        }
    }
}

function Compare-Backends {
    param([array]$SummaryRows)

    $comparisonRows = New-Object System.Collections.Generic.List[object]
    $scenarioNames = @($SummaryRows | Select-Object -ExpandProperty Scenario -Unique)

    foreach ($scenarioName in $scenarioNames) {
        $scenarioRows = @($SummaryRows | Where-Object { $_.Scenario -eq $scenarioName })
        if ($scenarioRows.Count -lt 2) {
            continue
        }

        $baseline = $scenarioRows | Where-Object { $_.Backend -eq "http" } | Select-Object -First 1
        $candidate = $scenarioRows | Where-Object { $_.Backend -eq "com" } | Select-Object -First 1

        if ($null -eq $baseline -or $null -eq $candidate) {
            continue
        }

        $ratio = if ($baseline.P95Ms -gt 0) { [Math]::Round(($candidate.P95Ms / $baseline.P95Ms), 2) } else { 0 }
        $deltaMs = [Math]::Round(($candidate.P95Ms - $baseline.P95Ms), 2)

        $comparisonRows.Add([pscustomobject]@{
            Scenario = $scenarioName
            HttpP95Ms = $baseline.P95Ms
            ComP95Ms = $candidate.P95Ms
            DeltaMs = $deltaMs
            ComToHttpRatio = $ratio
        }) | Out-Null
    }

    return $comparisonRows
}

Assert-PathExists -PathToCheck $ExePath -Label "CLI executable"
if ($IncludeSingleCreateSmoke.IsPresent) {
    Assert-PathExists -PathToCheck $SingleCreateXmlPath -Label "Single create XML"
}
Assert-PathExists -PathToCheck $ModifyXmlPath -Label "Modify XML"
Assert-PathExists -PathToCheck $CreateXmlPath -Label "Create XML"

if ($MeasuredRuns -lt 1) { throw "MeasuredRuns must be >= 1" }
if ($WarmupRuns -lt 0) { throw "WarmupRuns must be >= 0" }

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backendLabel = (($Backends | ForEach-Object { $_.ToLowerInvariant() }) -join "-")
$runDir = Join-Path $OutputDir ("workflow-perf-{0}-{1}" -f $backendLabel, $timestamp)
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

$normalizedBackends = @($Backends | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
if ($normalizedBackends.Count -eq 0) {
    throw "At least one backend must be supplied to -Backends"
}

$scenarios = @(
    [pscustomobject]@{
        Name = "bulk-delete-600"
        OperationCount = $BulkDeleteTargetCount
        Args = @("workflow", "delete", "--filter", $BulkDeleteFilter, "--status", "all", "--dry-run", "--force")
    },
    [pscustomobject]@{
        Name = "modify-1-replace-60-trustees"
        OperationCount = 1
        Args = @("workflow", "modify", "--xml", $ModifyXmlPath, "--dry-run", "--yes")
    },
    [pscustomobject]@{
        Name = "create-40-batch"
        OperationCount = 40
        Args = @("workflow", "create", "--xml", $CreateXmlPath, "--dry-run", "--yes")
    }
)

if ($IncludeSingleCreateSmoke.IsPresent) {
    $scenarios = @(
        [pscustomobject]@{
            Name = "create-1-smoke"
            OperationCount = 1
            Args = @("workflow", "create", "--xml", $SingleCreateXmlPath, "--dry-run", "--yes")
        }
    ) + $scenarios
}

if ($RunLiveMutations.IsPresent) {
    $scenarios = @(
        [pscustomobject]@{
            Name = "bulk-delete-600"
            OperationCount = $BulkDeleteTargetCount
            Args = @("workflow", "delete", "--filter", $BulkDeleteFilter, "--status", "all", "--force")
        },
        [pscustomobject]@{
            Name = "modify-1-replace-60-trustees"
            OperationCount = 1
            Args = @("workflow", "modify", "--xml", $ModifyXmlPath, "--yes")
        },
        [pscustomobject]@{
            Name = "create-40-batch"
            OperationCount = 40
            Args = @("workflow", "create", "--xml", $CreateXmlPath, "--yes")
        }
    )

    if ($IncludeSingleCreateSmoke.IsPresent) {
        $scenarios = @(
            [pscustomobject]@{
                Name = "create-1-smoke"
                OperationCount = 1
                Args = @("workflow", "create", "--xml", $SingleCreateXmlPath, "--yes")
            }
        ) + $scenarios
    }
}

$measurements = New-Object System.Collections.Generic.List[object]

foreach ($backend in $normalizedBackends) {
    $serverForBackend = Get-ServerForBackend -BackendName $backend
    $commonArgs = @("--server", $serverForBackend, "--user", $User, "--backend", $backend)

    foreach ($scenario in $scenarios) {
        Write-Host "`nBackend: $backend | Scenario: $($scenario.Name)" -ForegroundColor Cyan

        for ($i = 1; $i -le $WarmupRuns; $i++) {
            Write-Host "  Warmup run $i/$WarmupRuns"
            $null = Invoke-TimedCli -Scenario ("{0}-{1}-warmup" -f $backend, $scenario.Name) -Arguments ($commonArgs + $scenario.Args) -Iteration $i -OutDir $runDir
        }

        for ($i = 1; $i -le $MeasuredRuns; $i++) {
            Write-Host "  Measured run $i/$MeasuredRuns"
            $row = Invoke-TimedCli -Scenario ("{0}-{1}" -f $backend, $scenario.Name) -Arguments ($commonArgs + $scenario.Args) -Iteration $i -OutDir $runDir
            $measurements.Add([pscustomobject]@{
                Backend = $backend
                Scenario = $scenario.Name
                Iteration = $row.Iteration
                ExitCode = $row.ExitCode
                DurationMs = $row.DurationMs
                StdoutPath = $row.StdoutPath
                StderrPath = $row.StderrPath
            }) | Out-Null
        }
    }
}

$summary = New-Object System.Collections.Generic.List[object]
foreach ($backend in $normalizedBackends) {
    foreach ($scenario in $scenarios) {
        $rows = @($measurements | Where-Object { $_.Backend -eq $backend -and $_.Scenario -eq $scenario.Name })
        $summary.Add((Summarize-Scenario -Backend $backend -Scenario $scenario.Name -Rows $rows -OperationCount $scenario.OperationCount)) | Out-Null
    }
}

$dbSignals = Get-DbDirectSignal -SummaryRows $summary
$comparison = Compare-Backends -SummaryRows $summary

$measureCsv = Join-Path $runDir "measurements.csv"
$summaryCsv = Join-Path $runDir "summary.csv"
$comparisonCsv = Join-Path $runDir "backend-comparison.csv"
$signalTxt = Join-Path $runDir "db-direct-signals.txt"

$measurements | Export-Csv -NoTypeInformation -Path $measureCsv
$summary | Export-Csv -NoTypeInformation -Path $summaryCsv
$comparison | Export-Csv -NoTypeInformation -Path $comparisonCsv
$dbSignals | Set-Content -Path $signalTxt

Write-Host "`n=== Workflow Coarse Performance Summary ===" -ForegroundColor Green
$summary | Format-Table -AutoSize

if ($comparison.Count -gt 0) {
    Write-Host "`n=== Backend Comparison (COM vs HTTP) ===" -ForegroundColor Green
    $comparison | Format-Table -AutoSize
}

Write-Host "`nDB-direct decision signals:" -ForegroundColor Yellow
$dbSignals | ForEach-Object { Write-Host "- $_" }

Write-Host "`nArtifacts:" -ForegroundColor Gray
Write-Host "- $measureCsv"
Write-Host "- $summaryCsv"
Write-Host "- $comparisonCsv"
Write-Host "- $signalTxt"
