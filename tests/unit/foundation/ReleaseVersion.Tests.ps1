$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$versionJsonPath = Join-Path $repositoryRoot 'eng\version.json'
$readVersionScript = Join-Path $repositoryRoot 'eng\read-version.ps1'
$directoryBuildProps = Join-Path $repositoryRoot 'Directory.Build.props'
$cmakeLists = Join-Path $repositoryRoot 'CMakeLists.txt'

if (-not (Test-Path -LiteralPath $versionJsonPath)) {
    throw "Expected canonical version file '$versionJsonPath' to exist."
}

if (-not (Test-Path -LiteralPath $readVersionScript)) {
    throw "Expected version reader '$readVersionScript' to exist."
}

$versionInfo = & $readVersionScript
if ($versionInfo.Version -ne '0.1.0' -or $versionInfo.Tag -ne 'v0.1.0' -or -not $versionInfo.IsStableSemVer) {
    throw 'Expected read-version.ps1 to expose Version, Tag, and IsStableSemVer for DearStory 0.1.0.'
}

$directoryBuildPropsContent = Get-Content -Raw $directoryBuildProps
if ($directoryBuildPropsContent -notmatch 'eng\\version\.json') {
    throw 'Directory.Build.props must derive VersionPrefix from eng/version.json.'
}

$cmakeListsContent = Get-Content -Raw $cmakeLists
if ($cmakeListsContent -notmatch 'string\(JSON\s+DEARSTORY_VERSION') {
    throw 'CMakeLists.txt must derive PROJECT_VERSION from eng/version.json.'
}
