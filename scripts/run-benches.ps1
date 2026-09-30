[CmdletBinding()]
param(
    [string]$Filter = "",
    [string]$SaveBaseline = "",
    [string]$Baseline = "",
    [switch]$VerboseOutput,
    [switch]$OpenReport
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$criterionReportDir = Join-Path $repositoryRoot 'target\criterion\report'
$criterionReportHtml = Join-Path $criterionReportDir 'index.html'

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Sprout Criterion Performance Benchmarking Suite" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Repository: $repositoryRoot" -ForegroundColor DarkGray

$cargoArgs = @("bench", "-p", "sprout-core", "--bench", "engine_benches", "--")

if ($Filter) {
    Write-Host "Filter: $Filter" -ForegroundColor Yellow
    $cargoArgs += $Filter
}

if ($SaveBaseline) {
    Write-Host "Saving baseline: $SaveBaseline" -ForegroundColor Green
    $cargoArgs += @("--save-baseline", $SaveBaseline)
}

if ($Baseline) {
    Write-Host "Comparing against baseline: $Baseline" -ForegroundColor Green
    $cargoArgs += @("--baseline", $Baseline)
}

if ($VerboseOutput) {
    $cargoArgs += "--verbose"
}

Write-Host "`nRunning cargo $($cargoArgs -join ' ')..." -ForegroundColor White

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
& cargo @cargoArgs
$exitCode = $LASTEXITCODE
$stopwatch.Stop()

if ($exitCode -ne 0) {
    Write-Host "`nBenchmarking failed with exit code $exitCode." -ForegroundColor Red
    exit $exitCode
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Benchmark Suite Completed in $([math]::Round($stopwatch.Elapsed.TotalSeconds, 2))s" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

if (Test-Path -LiteralPath $criterionReportHtml) {
    Write-Host "Criterion HTML Report: $criterionReportHtml" -ForegroundColor Cyan
    if ($OpenReport) {
        Start-Process $criterionReportHtml
    }
} else {
    Write-Host "Criterion results generated under target/criterion/" -ForegroundColor DarkGray
}

exit 0
