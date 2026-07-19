[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Tag', 'Manual', 'Local')]
    [string]$ReleaseMode = 'Local',

    [string]$ExpectedVersion,
    [string]$SourceRef = 'HEAD',
    [string]$SourceCommit = '',

    [ValidateSet('Release')]
    [string]$Configuration = 'Release',

    [string]$OutputRoot = '.\artifacts\releases',

    [switch]$SkipBuild,
    [switch]$SkipTest
)

$ErrorActionPreference = 'Stop'
$script:DearStoryCmdlet = $PSCmdlet

function Invoke-DearStoryCommand {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $commandText = (@($Executable) + $Arguments) -join ' '
    Write-Output $commandText

    if (-not $script:DearStoryCmdlet.ShouldProcess($Executable, 'Invoke external tool')) {
        return
    }

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $commandText (exit code $LASTEXITCODE)."
    }
}

$versionInfo = & (Join-Path $PSScriptRoot 'read-version.ps1')
if ($ExpectedVersion -and $ExpectedVersion -ne $versionInfo.Version) {
    throw "Expected version '$ExpectedVersion' does not match repository version '$($versionInfo.Version)'."
}

if ([string]::IsNullOrWhiteSpace($SourceCommit)) {
    $SourceCommit = (git rev-parse HEAD).Trim()
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseRoot = Join-Path $repositoryRoot ("artifacts\releases\{0}" -f $versionInfo.Version)
$dotnetReleaseDirectory = Join-Path $releaseRoot 'dotnet'
$cppReleaseDirectory = Join-Path $releaseRoot 'cpp'
$stagingInstallPrefix = Join-Path $repositoryRoot 'artifacts\install\dearstory-release'
$cppArchivePath = Join-Path $cppReleaseDirectory ("DearStory-cpp-{0}-windows-msvc-x64.zip" -f $versionInfo.Version)
$checksumPath = Join-Path $releaseRoot 'SHA256SUMS'
$manifestPath = Join-Path $releaseRoot 'release-manifest.json'

if (-not $SkipBuild) {
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\build.ps1', '-Configuration', $Configuration)
}

if (-not $SkipTest) {
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\test.ps1', '-Configuration', $Configuration)
}

Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\pack.ps1', '-Configuration', $Configuration)

if ($script:DearStoryCmdlet.ShouldProcess($releaseRoot, 'Prepare release output tree')) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stagingInstallPrefix -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $dotnetReleaseDirectory, $cppReleaseDirectory | Out-Null
}

Write-Output ("Copy-Item -Path {0} -Destination {1} -Force" -f (Join-Path $repositoryRoot 'artifacts\packages\dotnet\*.nupkg'), $dotnetReleaseDirectory)
if ($script:DearStoryCmdlet.ShouldProcess($dotnetReleaseDirectory, 'Copy public .NET packages into release unit')) {
    Copy-Item -Path (Join-Path $repositoryRoot 'artifacts\packages\dotnet\*.nupkg') -Destination $dotnetReleaseDirectory -Force
}

Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--install', '.\build\windows-msvc-debug', '--config', $Configuration, '--prefix', '.\artifacts\install\dearstory-release')
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\assert-public-package-boundaries.ps1', '-CppInstallPrefix', $stagingInstallPrefix)
Write-Output ("Compress-Archive -Path {0}\* -DestinationPath {1} -Force" -f $stagingInstallPrefix, $cppArchivePath)
if ($script:DearStoryCmdlet.ShouldProcess($cppArchivePath, 'Create public C++ release archive')) {
    Compress-Archive -Path (Join-Path $stagingInstallPrefix '*') -DestinationPath $cppArchivePath -Force
}

if ($script:DearStoryCmdlet.ShouldProcess($checksumPath, 'Write SHA256SUMS')) {
    $checksumLines = Get-ChildItem -Path $releaseRoot -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($releaseRoot, $_.FullName).Replace('\', '/')
            $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
            '{0} *{1}' -f $hash, $relativePath
        }

    Set-Content -LiteralPath $checksumPath -Value $checksumLines
}

Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @(
    '-NoProfile',
    '-File',
    '.\eng\generate-release-manifest.ps1',
    '-Version',
    $versionInfo.Version,
    '-SourceCommit',
    $SourceCommit,
    '-SourceRef',
    $SourceRef,
    '-ReleaseMode',
    $ReleaseMode,
    '-ArtifactsRoot',
    $releaseRoot,
    '-OutputPath',
    $manifestPath
)
