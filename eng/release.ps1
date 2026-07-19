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

if ($ReleaseMode -eq 'Tag') {
    $expectedTagRef = "refs/tags/v$($versionInfo.Version)"
    if ($SourceRef -ne $expectedTagRef) {
        throw "Tag releases require SourceRef '$expectedTagRef'."
    }

    $canonicalTagCommit = (& git rev-parse --verify "$expectedTagRef^{commit}" 2>$null).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Tag releases require '$expectedTagRef' to resolve to a commit."
    }

    if ([string]::IsNullOrWhiteSpace($SourceCommit)) {
        $SourceCommit = $canonicalTagCommit
    }

    $resolvedSourceCommit = (& git rev-parse --verify "$SourceCommit^{commit}" 2>$null).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Tag release source commit '$SourceCommit' does not resolve to a commit."
    }

    if ($resolvedSourceCommit -ne $canonicalTagCommit) {
        throw "Tag release source commit '$resolvedSourceCommit' does not match '$expectedTagRef' commit '$canonicalTagCommit'."
    }

    $SourceCommit = $canonicalTagCommit
}
elseif ([string]::IsNullOrWhiteSpace($SourceCommit)) {
    $SourceCommit = (git rev-parse HEAD).Trim()
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
}
else {
    Join-Path $repositoryRoot $OutputRoot
}
$releaseRoot = Join-Path ([System.IO.Path]::GetFullPath($outputRootPath)) $versionInfo.Version
$stagingReleaseRoot = Join-Path ([System.IO.Path]::GetDirectoryName($releaseRoot)) (".{0}.staging.{1}" -f $versionInfo.Version, [guid]::NewGuid().ToString('N'))
$dotnetReleaseDirectory = Join-Path $stagingReleaseRoot 'dotnet'
$cppReleaseDirectory = Join-Path $stagingReleaseRoot 'cpp'
$stagingInstallPrefix = Join-Path $repositoryRoot ("artifacts\install\dearstory-release-{0}" -f [guid]::NewGuid().ToString('N'))
$cppArchivePath = Join-Path $cppReleaseDirectory ("DearStory-cpp-{0}-windows-msvc-x64.zip" -f $versionInfo.Version)
$checksumPath = Join-Path $stagingReleaseRoot 'SHA256SUMS'
$manifestPath = Join-Path $stagingReleaseRoot 'release-manifest.json'
$releasePromoted = $false

if (-not $SkipBuild) {
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\build.ps1', '-Configuration', $Configuration)
}

if (-not $SkipTest) {
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\test.ps1', '-Configuration', $Configuration)
}

Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\pack.ps1', '-Configuration', $Configuration)

if (-not $WhatIfPreference -and (Test-Path -LiteralPath $releaseRoot)) {
    throw "Release output directory '$releaseRoot' already exists. Refusing to replace an existing release unit."
}

try {
if ($script:DearStoryCmdlet.ShouldProcess($stagingReleaseRoot, 'Prepare release staging tree')) {
    New-Item -ItemType Directory -Force -Path $dotnetReleaseDirectory, $cppReleaseDirectory | Out-Null
    Remove-Item -LiteralPath $stagingInstallPrefix -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output ("Copy-Item -Path {0} -Destination {1} -Force" -f (Join-Path $repositoryRoot 'artifacts\packages\dotnet\*.nupkg'), $dotnetReleaseDirectory)
if ($script:DearStoryCmdlet.ShouldProcess($dotnetReleaseDirectory, 'Copy public .NET packages into release unit')) {
    Copy-Item -Path (Join-Path $repositoryRoot 'artifacts\packages\dotnet\*.nupkg') -Destination $dotnetReleaseDirectory -Force
}

Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--install', '.\build\windows-msvc-debug', '--config', $Configuration, '--prefix', $stagingInstallPrefix)
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\assert-public-package-boundaries.ps1', '-CppInstallPrefix', $stagingInstallPrefix)
Write-Output ("Compress-Archive -Path {0}\* -DestinationPath {1} -Force" -f $stagingInstallPrefix, $cppArchivePath)
if ($script:DearStoryCmdlet.ShouldProcess($cppArchivePath, 'Create public C++ release archive')) {
    Compress-Archive -Path (Join-Path $stagingInstallPrefix '*') -DestinationPath $cppArchivePath -Force
}

if ($script:DearStoryCmdlet.ShouldProcess($checksumPath, 'Write SHA256SUMS')) {
    $checksumLines = Get-ChildItem -Path $stagingReleaseRoot -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($stagingReleaseRoot, $_.FullName).Replace('\', '/')
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
    $stagingReleaseRoot,
    '-OutputPath',
    $manifestPath
)

if (-not $WhatIfPreference -and (Test-Path -LiteralPath $releaseRoot)) {
    throw "Release output directory '$releaseRoot' already exists. Refusing to replace an existing release unit."
}

Write-Output ("[System.IO.Directory]::Move('{0}', '{1}')" -f $stagingReleaseRoot, $releaseRoot)
if ($script:DearStoryCmdlet.ShouldProcess($releaseRoot, 'Promote completed release unit')) {
    [System.IO.Directory]::Move($stagingReleaseRoot, $releaseRoot)

    $requiredReleasePaths = @(
        (Join-Path $releaseRoot 'dotnet'),
        (Join-Path $releaseRoot 'cpp'),
        (Join-Path $releaseRoot 'SHA256SUMS'),
        (Join-Path $releaseRoot 'release-manifest.json')
    )
    $missingReleasePaths = @($requiredReleasePaths | Where-Object { -not (Test-Path -LiteralPath $_) })
    if ((Test-Path -LiteralPath $stagingReleaseRoot) -or
        -not (Test-Path -LiteralPath $releaseRoot -PathType Container) -or
        $missingReleasePaths.Count -ne 0) {
        throw "Unable to verify promoted release unit at '$releaseRoot'."
    }

    $releasePromoted = $true
}
}
finally {
    if (-not $releasePromoted -and (Test-Path -LiteralPath $stagingReleaseRoot)) {
        Remove-Item -LiteralPath $stagingReleaseRoot -Recurse -Force
    }

    if (Test-Path -LiteralPath $stagingInstallPrefix) {
        Remove-Item -LiteralPath $stagingInstallPrefix -Recurse -Force
    }
}
