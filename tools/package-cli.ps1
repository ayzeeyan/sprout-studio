[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ArtifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot 'artifacts'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ArtifactsRoot 'sprout-cli-win-x64'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith($ArtifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The CLI package destination must remain inside $ArtifactsRoot."
}

$PythonRuntime = Join-Path $RepositoryRoot 'runtime\python-3.13.14-win-x64'
& (Join-Path $PSScriptRoot 'prepare-python-runtime.ps1') -Destination $PythonRuntime

$CargoArguments = @('build', '-p', 'sprout-cli')
$CargoDirectory = 'debug'
if ($Configuration -eq 'Release') {
    $CargoArguments += '--release'
    $CargoDirectory = 'release'
}
& cargo @CargoArguments
if ($LASTEXITCODE -ne 0) {
    throw "Cargo failed to build the Sprout CLI."
}

New-Item -ItemType Directory -Path $ArtifactsRoot -Force | Out-Null
$StagingDirectory = Join-Path $ArtifactsRoot ".sprout-cli-$PID"
if (Test-Path -LiteralPath $StagingDirectory) {
    throw "The staging directory $StagingDirectory already exists."
}
New-Item -ItemType Directory -Path $StagingDirectory | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $RepositoryRoot "target\$CargoDirectory\sprout.exe") -Destination $StagingDirectory
    Copy-Item -Path (Join-Path $PythonRuntime '*') -Destination $StagingDirectory -Recurse
    if (Test-Path -LiteralPath $OutputDirectory) {
        $ResolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
        if (-not $ResolvedOutput.StartsWith($ArtifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to replace a package outside $ArtifactsRoot."
        }
        Remove-Item -LiteralPath $ResolvedOutput -Recurse -Force
    }
    Move-Item -LiteralPath $StagingDirectory -Destination $OutputDirectory
    Write-Host "Packaged self-contained Sprout CLI at $OutputDirectory"
}
finally {
    if (Test-Path -LiteralPath $StagingDirectory) {
        Remove-Item -LiteralPath $StagingDirectory -Recurse -Force
    }
}
