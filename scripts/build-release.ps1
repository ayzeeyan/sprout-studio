[CmdletBinding()]
param(
    [switch]$SkipValidation,
    [switch]$SkipPortableArchive,
    [switch]$OfflineRestore
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$releaseDirectory = Join-Path $artifactsRoot 'release'
$publishDirectory = Join-Path $artifactsRoot 'publish\win-x64'
$solution = Join-Path $repositoryRoot 'studio\SproutStudio.sln'
$desktopProject = Join-Path $repositoryRoot 'studio\src\SproutStudio.Desktop\SproutStudio.Desktop.csproj'
$testProject = Join-Path $repositoryRoot 'studio\tests\SproutStudio.Tests\SproutStudio.Tests.csproj'
$nugetConfig = Join-Path $repositoryRoot 'studio\NuGet.Config'
$installerScript = Join-Path $repositoryRoot 'installer\SproutStudio.nsi'
$icon = Join-Path $repositoryRoot 'assets\sprout-studio.ico'
$welcomeBitmap = Join-Path $repositoryRoot 'assets\sprout-installer-welcome.bmp'
$license = Join-Path $repositoryRoot 'LICENSE'
$version = (Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'VERSION')).Trim()

if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "VERSION must be a three-part numeric semantic version; found '$version'."
}
$cargoManifest = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Cargo.toml')
if ($cargoManifest -notmatch "(?m)^version\s*=\s*`"$([regex]::Escape($version))`"\s*$") {
    throw "Cargo workspace version does not match VERSION ($version)."
}

function Assert-GeneratedPath([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $requiredPrefix = $artifactsRoot.TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside artifacts: $fullPath"
    }
    return $fullPath
}

function Reset-GeneratedDirectory([string]$Path) {
    $fullPath = Assert-GeneratedPath $Path
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    Write-Host "> $FilePath $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath exited with code $LASTEXITCODE."
    }
}

function Find-MakeNsis {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'),
        (Join-Path $env:ProgramFiles 'NSIS\makensis.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\NSIS\makensis.exe')
    )
    $candidate = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $candidate) {
        throw 'NSIS 3 is required. Install NSIS, then rerun this script.'
    }
    return $candidate
}

function Find-SignTool {
    if (-not [string]::IsNullOrWhiteSpace($env:SPROUT_SIGNTOOL_PATH)) {
        return $env:SPROUT_SIGNTOOL_PATH
    }
    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (-not (Test-Path -LiteralPath $kitsRoot)) { return $null }
    return Get-ChildItem -LiteralPath $kitsRoot -Directory |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
}

function Invoke-CodeSign([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($env:SPROUT_SIGN_CERT_SHA1)) { return }
    $signTool = Find-SignTool
    if (-not $signTool) { throw 'SPROUT_SIGN_CERT_SHA1 is set, but signtool.exe was not found.' }
    Invoke-Checked $signTool @(
        'sign', '/sha1', $env:SPROUT_SIGN_CERT_SHA1,
        '/fd', 'SHA256', '/td', 'SHA256',
        '/tr', 'http://timestamp.digicert.com', $Path)
}

$toolStateRoot = Assert-GeneratedPath (Join-Path $artifactsRoot 'tool-state')
$nugetAppData = Join-Path $toolStateRoot 'appdata'
$dotnetCliHome = Join-Path $toolStateRoot 'dotnet-home'
$nugetScratch = Join-Path $toolStateRoot 'nuget-scratch'
foreach ($directory in @($nugetAppData, $dotnetCliHome, $nugetScratch)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$previousAppData = $env:APPDATA
$previousAvaloniaTelemetry = $env:AVALONIA_TELEMETRY_OPTOUT
$previousDotnetCliHome = $env:DOTNET_CLI_HOME
$previousDotnetAddToolsToPath = $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH
$previousDotnetGenerateCertificate = $env:DOTNET_GENERATE_ASPNET_CERTIFICATE
$previousDotnetFirstTimeExperience = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
$previousDotnetNoLogo = $env:DOTNET_NOLOGO
$previousDotnetTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$previousNugetPackages = $env:NUGET_PACKAGES
$previousNugetScratch = $env:NUGET_SCRATCH
$previousPath = $env:PATH

$env:APPDATA = $nugetAppData
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = '0'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = if ([string]::IsNullOrWhiteSpace($previousNugetPackages)) {
    Join-Path $env:USERPROFILE '.nuget\packages'
} else {
    $previousNugetPackages
}
$env:NUGET_SCRATCH = $nugetScratch

Reset-GeneratedDirectory $releaseDirectory
Reset-GeneratedDirectory $publishDirectory

Push-Location $repositoryRoot
try {
    $rustup = Get-Command 'rustup' -ErrorAction SilentlyContinue
    if ($rustup) {
        $resolvedCargo = (& $rustup.Source 'which' 'cargo' 2>$null | Select-Object -Last 1)
        if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $resolvedCargo -PathType Leaf)) {
            $resolvedCargoDirectory = Split-Path -Parent $resolvedCargo
            $env:PATH = "$resolvedCargoDirectory;$env:PATH"
        }
    }

    Invoke-Checked 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $repositoryRoot 'tools\generate-app-icon.ps1'))
    $restoreArguments = @('restore', $solution, '--configfile', $nugetConfig, '-p:Platform=x64', '-r', 'win-x64')
    if ($OfflineRestore) {
        $restoreArguments += @('--ignore-failed-sources', '-p:NuGetAudit=false', '-p:NoWarn=NU1801')
    }
    Invoke-Checked 'dotnet' $restoreArguments
    Invoke-Checked 'cargo' @('build', '--workspace', '--release', '--locked')

    if (-not $SkipValidation) {
        Invoke-Checked 'cargo' @('fmt', '--all', '--check')
        Invoke-Checked 'cargo' @('clippy', '--workspace', '--all-targets', '--locked', '--', '-D', 'warnings')
        Invoke-Checked 'cargo' @('test', '--workspace', '--release', '--locked')
        # Performance release gate: verify engine benchmark suite
        Invoke-Checked 'cargo' @('bench', '--bench', 'engine_benches', '--', '--test')
    }

    Invoke-Checked 'dotnet' @('build', $solution, '-c', 'Release', '-p:Platform=x64', '--no-restore')
    if (-not $SkipValidation) {
        Invoke-Checked 'dotnet' @('test', $solution, '-c', 'Release', '-p:Platform=x64', '--no-build', '--no-restore')
        Invoke-Checked 'dotnet' @('run', '--project', $testProject, '-c', 'Release', '-p:Platform=x64', '--no-build', '--no-restore')
    }

    Invoke-Checked 'dotnet' @(
        'publish', $desktopProject,
        '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        '-p:Platform=x64', '--no-restore', '-o', $publishDirectory)

    $requiredPayload = @(
        'SproutStudio.exe', 'sprout_ffi.dll', 'sprout.exe',
        'python313.dll', 'Content\Docs\studio.md', 'Content\Docs\stdlib.md')
    foreach ($relativePath in $requiredPayload) {
        $payloadPath = Join-Path $publishDirectory $relativePath
        if (-not (Test-Path -LiteralPath $payloadPath -PathType Leaf)) {
            throw "Release payload is missing $relativePath."
        }
    }

    Invoke-CodeSign (Join-Path $publishDirectory 'SproutStudio.exe')
    Invoke-CodeSign (Join-Path $publishDirectory 'sprout.exe')
    Invoke-CodeSign (Join-Path $publishDirectory 'sprout_ffi.dll')

    if (-not $SkipPortableArchive) {
        $portableArchive = Join-Path $releaseDirectory 'SproutStudio-Portable-x64.zip'
        Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $portableArchive -CompressionLevel Optimal
    }

    $makeNsis = Find-MakeNsis
    Invoke-Checked $makeNsis @(
        "/DVERSION=$version",
        "/DPUBLISH_DIR=$publishDirectory",
        "/DOUTPUT_DIR=$releaseDirectory",
        "/DAPP_ICON=$icon",
        "/DWELCOME_BITMAP=$welcomeBitmap",
        "/DLICENSE_FILE=$license",
        $installerScript)
    $setup = Join-Path $releaseDirectory 'SproutStudio-Setup-x64.exe'
    if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) {
        throw 'NSIS completed without producing SproutStudio-Setup-x64.exe.'
    }
    Invoke-CodeSign $setup
    $setupAlias = Join-Path $releaseDirectory 'SproutStudio-Setup.exe'
    Copy-Item -LiteralPath $setup -Destination $setupAlias -Force

    $checksumPath = Join-Path $releaseDirectory 'SHA256SUMS.txt'
    $checksumLines = Get-ChildItem -LiteralPath $releaseDirectory -File |
        Where-Object Name -ne 'SHA256SUMS.txt' |
        Sort-Object Name |
        ForEach-Object {
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash *$($_.Name)"
        }
    [System.IO.File]::WriteAllLines($checksumPath, $checksumLines, [System.Text.UTF8Encoding]::new($false))

    Write-Host "Release $version is ready in $releaseDirectory" -ForegroundColor Green
    if ([string]::IsNullOrWhiteSpace($env:SPROUT_SIGN_CERT_SHA1)) {
        Write-Host 'Artifacts are unsigned; set SPROUT_SIGN_CERT_SHA1 for an authorized production certificate.' -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
    $env:APPDATA = $previousAppData
    $env:AVALONIA_TELEMETRY_OPTOUT = $previousAvaloniaTelemetry
    $env:DOTNET_CLI_HOME = $previousDotnetCliHome
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = $previousDotnetAddToolsToPath
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = $previousDotnetGenerateCertificate
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $previousDotnetFirstTimeExperience
    $env:DOTNET_NOLOGO = $previousDotnetNoLogo
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousDotnetTelemetry
    $env:NUGET_PACKAGES = $previousNugetPackages
    $env:NUGET_SCRATCH = $previousNugetScratch
    $env:PATH = $previousPath
}
