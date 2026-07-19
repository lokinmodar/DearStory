$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$versionJsonPath = Join-Path $repositoryRoot 'eng\version.json'
$readVersionScript = Join-Path $repositoryRoot 'eng\read-version.ps1'
$directoryBuildProps = Join-Path $repositoryRoot 'Directory.Build.props'
$cmakeLists = Join-Path $repositoryRoot 'CMakeLists.txt'
$vcpkgManifestPath = Join-Path $repositoryRoot 'vcpkg.json'

if (-not (Test-Path -LiteralPath $versionJsonPath)) {
    throw "Expected canonical version file '$versionJsonPath' to exist."
}

if (-not (Test-Path -LiteralPath $readVersionScript)) {
    throw "Expected version reader '$readVersionScript' to exist."
}

$canonicalVersion = [string](Get-Content -Raw -LiteralPath $versionJsonPath | ConvertFrom-Json).version
$versionInfo = & $readVersionScript
if ($versionInfo.Version -ne $canonicalVersion -or $versionInfo.Tag -ne "v$canonicalVersion" -or -not $versionInfo.IsStableSemVer) {
    throw 'Expected read-version.ps1 to derive Version, Tag, and IsStableSemVer from eng/version.json.'
}

$vcpkgManifest = Get-Content -Raw -LiteralPath $vcpkgManifestPath | ConvertFrom-Json
$vcpkgVersionProperties = @($vcpkgManifest.PSObject.Properties.Name | Where-Object { $_ -match '^version' })
if ($vcpkgVersionProperties.Count -ne 0) {
    throw "vcpkg.json must not declare a repository version; eng/version.json is canonical. Found: $($vcpkgVersionProperties -join ', ')."
}

$directoryBuildPropsContent = Get-Content -Raw $directoryBuildProps
if ($directoryBuildPropsContent -notmatch 'eng\\version\.json') {
    throw 'Directory.Build.props must derive VersionPrefix from eng/version.json.'
}

$cmakeListsContent = Get-Content -Raw $cmakeLists
if ($cmakeListsContent -notmatch 'string\(JSON\s+DEARSTORY_VERSION') {
    throw 'CMakeLists.txt must derive PROJECT_VERSION from eng/version.json.'
}
