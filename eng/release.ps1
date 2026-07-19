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
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:DearStoryRepositoryRoot = $repositoryRoot
$buildScript = Join-Path $repositoryRoot 'eng\build.ps1'
$testScript = Join-Path $repositoryRoot 'eng\test.ps1'
$packScript = Join-Path $repositoryRoot 'eng\pack.ps1'
$packageBoundaryScript = Join-Path $repositoryRoot 'eng\assert-public-package-boundaries.ps1'
$manifestScript = Join-Path $repositoryRoot 'eng\generate-release-manifest.ps1'
$cmakeBuildDirectory = Join-Path $repositoryRoot 'build\windows-msvc-debug'

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

    Push-Location $script:DearStoryRepositoryRoot
    try {
        & $Executable @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        throw "Command failed: $commandText (exit code $exitCode)."
    }
}

function Resolve-DearStoryCommit {
    param(
        [Parameter(Mandatory)][string]$Revision,
        [Parameter(Mandatory)][string]$FailureMessage
    )

    $resolvedCommitOutput = & git -C $script:DearStoryRepositoryRoot rev-parse --verify "$Revision^{commit}" 2>$null
    $gitExitCode = $LASTEXITCODE
    $resolvedCommit = $resolvedCommitOutput | Select-Object -First 1
    if ($gitExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($resolvedCommit)) {
        throw $FailureMessage
    }

    $resolvedCommit.Trim()
}

function New-DearStoryDeterministicArchive {
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    $sourceRootPath = [System.IO.Path]::GetFullPath($SourceRoot)
    $relativePaths = [System.Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $sourceRootPath -File -Recurse) {
        $relativePaths.Add([System.IO.Path]::GetRelativePath($sourceRootPath, $file.FullName).Replace('\', '/'))
    }
    $relativePaths.Sort([System.StringComparer]::Ordinal)

    $archiveTimestamp = [System.DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [System.TimeSpan]::Zero)
    $archiveStream = [System.IO.File]::Open(
        $DestinationPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None
    )
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $archiveStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $true,
            [System.Text.Encoding]::UTF8
        )
        try {
            foreach ($relativePath in $relativePaths) {
                $entry = $archive.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $archiveTimestamp
                $sourcePath = Join-Path $sourceRootPath $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
                $sourceStream = [System.IO.File]::OpenRead($sourcePath)
                $entryStream = $entry.Open()
                try {
                    $sourceStream.CopyTo($entryStream)
                }
                finally {
                    $entryStream.Dispose()
                    $sourceStream.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $archiveStream.Dispose()
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

    $canonicalTagCommit = Resolve-DearStoryCommit `
        -Revision $expectedTagRef `
        -FailureMessage "Tag releases require '$expectedTagRef' to resolve to a commit in the DearStory repository."

    if ([string]::IsNullOrWhiteSpace($SourceCommit)) {
        $SourceCommit = $canonicalTagCommit
    }

    $resolvedSourceCommit = Resolve-DearStoryCommit `
        -Revision $SourceCommit `
        -FailureMessage "Tag release source commit '$SourceCommit' does not resolve to a commit in the DearStory repository."
    $repositoryHeadCommit = Resolve-DearStoryCommit `
        -Revision 'HEAD' `
        -FailureMessage 'Unable to resolve DearStory repository HEAD to a commit.'

    if ($resolvedSourceCommit -ne $canonicalTagCommit) {
        throw "Tag release source commit '$resolvedSourceCommit' does not match '$expectedTagRef' commit '$canonicalTagCommit'."
    }

    if ($repositoryHeadCommit -ne $canonicalTagCommit) {
        throw "Tag release repository HEAD commit '$repositoryHeadCommit' does not match '$expectedTagRef' commit '$canonicalTagCommit'."
    }

    $SourceCommit = $canonicalTagCommit
}
elseif ([string]::IsNullOrWhiteSpace($SourceCommit)) {
    $SourceCommit = Resolve-DearStoryCommit `
        -Revision 'HEAD' `
        -FailureMessage 'Unable to resolve DearStory repository HEAD to a commit.'
}

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

if (-not $WhatIfPreference -and (Test-Path -LiteralPath $releaseRoot)) {
    throw "Release output directory '$releaseRoot' already exists. Refusing to replace an existing release unit."
}

if (-not $SkipBuild) {
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', $buildScript, '-Configuration', $Configuration)
}

if (-not $SkipTest) {
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', $testScript, '-Configuration', $Configuration)
}

Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', $packScript, '-Configuration', $Configuration)

try {
if ($script:DearStoryCmdlet.ShouldProcess($stagingReleaseRoot, 'Prepare release staging tree')) {
    New-Item -ItemType Directory -Force -Path $dotnetReleaseDirectory, $cppReleaseDirectory | Out-Null
    Remove-Item -LiteralPath $stagingInstallPrefix -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output ("Copy-Item -Path {0} -Destination {1} -Force" -f (Join-Path $repositoryRoot 'artifacts\packages\dotnet\*.nupkg'), $dotnetReleaseDirectory)
if ($script:DearStoryCmdlet.ShouldProcess($dotnetReleaseDirectory, 'Copy public .NET packages into release unit')) {
    Copy-Item -Path (Join-Path $repositoryRoot 'artifacts\packages\dotnet\*.nupkg') -Destination $dotnetReleaseDirectory -Force
}

Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--install', $cmakeBuildDirectory, '--config', $Configuration, '--prefix', $stagingInstallPrefix)
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', $packageBoundaryScript, '-CppInstallPrefix', $stagingInstallPrefix)
Write-Output ("New-DearStoryDeterministicArchive -SourceRoot {0} -DestinationPath {1}" -f $stagingInstallPrefix, $cppArchivePath)
if ($script:DearStoryCmdlet.ShouldProcess($cppArchivePath, 'Create public C++ release archive')) {
    New-DearStoryDeterministicArchive -SourceRoot $stagingInstallPrefix -DestinationPath $cppArchivePath
}

Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @(
    '-NoProfile',
    '-File',
    $manifestScript,
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

if ($script:DearStoryCmdlet.ShouldProcess($checksumPath, 'Write SHA256SUMS')) {
    $checksumRelativePaths = [System.Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $stagingReleaseRoot -File -Recurse) {
        $checksumRelativePaths.Add([System.IO.Path]::GetRelativePath($stagingReleaseRoot, $file.FullName).Replace('\', '/'))
    }
    $checksumRelativePaths.Sort([System.StringComparer]::Ordinal)

    $checksumLines = @(
        foreach ($relativePath in $checksumRelativePaths) {
            $artifactPath = Join-Path $stagingReleaseRoot $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifactPath).Hash.ToLowerInvariant()
            '{0} *{1}' -f $hash, $relativePath
        }
    )

    [System.IO.File]::WriteAllLines($checksumPath, $checksumLines, [System.Text.UTF8Encoding]::new($false))
}

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
