[CmdletBinding()]
param(
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
$PythonVersion = '3.13.14'
$PythonArchiveName = "python-$PythonVersion-embed-amd64.zip"
$PythonArchiveUrl = "https://www.python.org/ftp/python/$PythonVersion/$PythonArchiveName"
$PythonArchiveSha256 = '90b4e5b9898b72d744650524bff92377c367f44bd5fbd09e3148656c080ad907'
$PipVersion = '26.2.1'
$PipWheelName = "pip-$PipVersion-py3-none-any.whl"
$PipWheelUrl = 'https://files.pythonhosted.org/packages/f3/6e/1736e5b4ae2b778ef2f81c47d797de9f891d4d8acb047a24ca37a60294dd/pip-26.2.1-py3-none-any.whl'
$PipWheelSha256 = '71138adf1f4ca900cdb7d289c21b7494329f2332b6d85f0e1c42108c0384ed3e'

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$RuntimeRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot 'runtime'))
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $RuntimeRoot "python-$PythonVersion-win-x64"
}
$Destination = [System.IO.Path]::GetFullPath($Destination)
if (-not $Destination.StartsWith($RuntimeRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The Python runtime destination must remain inside $RuntimeRoot."
}

$MarkerPath = Join-Path $Destination 'sprout-python-runtime.json'
if (Test-Path -LiteralPath $MarkerPath -PathType Leaf) {
    $Marker = Get-Content -LiteralPath $MarkerPath -Raw | ConvertFrom-Json
    if ($Marker.pythonVersion -eq $PythonVersion -and
        $Marker.pythonSha256 -eq $PythonArchiveSha256 -and
        $Marker.pipVersion -eq $PipVersion -and
        $Marker.pipSha256 -eq $PipWheelSha256 -and
        (Test-Path -LiteralPath (Join-Path $Destination 'python.exe') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $Destination 'python3.dll') -PathType Leaf)) {
        Write-Host "Pinned Python runtime is ready at $Destination"
        return
    }
    throw "The existing runtime at $Destination does not match the pinned runtime. Remove that exact directory and retry."
}
if (Test-Path -LiteralPath $Destination) {
    throw "The incomplete runtime directory $Destination already exists. Remove that exact directory and retry."
}

New-Item -ItemType Directory -Path $RuntimeRoot -Force | Out-Null
$StagingRoot = Join-Path $RuntimeRoot ".prepare-python-$PythonVersion-$PID"
if (Test-Path -LiteralPath $StagingRoot) {
    throw "The staging directory $StagingRoot already exists."
}
New-Item -ItemType Directory -Path $StagingRoot | Out-Null

function Receive-VerifiedFile {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$ExpectedSha256
    )

    Invoke-WebRequest -Uri $Uri -OutFile $OutputPath
    $ActualSha256 = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($ActualSha256 -ne $ExpectedSha256) {
        throw "Hash verification failed for $Uri. Expected $ExpectedSha256 but received $ActualSha256."
    }
}

try {
    $ArchivePath = Join-Path $StagingRoot $PythonArchiveName
    $WheelPath = Join-Path $StagingRoot $PipWheelName
    $ExpandedPath = Join-Path $StagingRoot 'expanded'
    Receive-VerifiedFile -Uri $PythonArchiveUrl -OutputPath $ArchivePath -ExpectedSha256 $PythonArchiveSha256
    Receive-VerifiedFile -Uri $PipWheelUrl -OutputPath $WheelPath -ExpectedSha256 $PipWheelSha256
    Expand-Archive -LiteralPath $ArchivePath -DestinationPath $ExpandedPath
    Copy-Item -LiteralPath $WheelPath -Destination (Join-Path $ExpandedPath $PipWheelName)

    $PathConfiguration = Join-Path $ExpandedPath 'python313._pth'
    if (-not (Test-Path -LiteralPath $PathConfiguration -PathType Leaf)) {
        throw "The CPython archive did not contain python313._pth."
    }
    $PathLines = [System.Collections.Generic.List[string]]::new()
    foreach ($Line in [System.IO.File]::ReadAllLines($PathConfiguration)) {
        if ($Line -ne '#import site') {
            $PathLines.Add($Line)
        }
    }
    $PathLines.Add($PipWheelName)
    $Utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllLines($PathConfiguration, $PathLines, $Utf8WithoutBom)

    $Marker = [ordered]@{
        pythonVersion = $PythonVersion
        pythonSha256 = $PythonArchiveSha256
        pipVersion = $PipVersion
        pipSha256 = $PipWheelSha256
        architecture = 'x86_64-pc-windows-msvc'
    } | ConvertTo-Json
    [System.IO.File]::WriteAllText((Join-Path $ExpandedPath 'sprout-python-runtime.json'), $Marker, $Utf8WithoutBom)
    Move-Item -LiteralPath $ExpandedPath -Destination $Destination
    Write-Host "Prepared pinned Python $PythonVersion with pip $PipVersion at $Destination"
}
finally {
    $ResolvedStaging = [System.IO.Path]::GetFullPath($StagingRoot)
    if ($ResolvedStaging.StartsWith($RuntimeRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $ResolvedStaging)) {
        Remove-Item -LiteralPath $ResolvedStaging -Recurse -Force
    }
}
