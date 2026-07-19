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

$invalidVersionDocuments = @(
    @{ Name = 'prerelease'; Content = '{"version":"1.2.3-preview.1"}' },
    @{ Name = 'build metadata'; Content = '{"version":"1.2.3+build.1"}' },
    @{ Name = 'leading-zero major component'; Content = '{"version":"01.2.3"}' },
    @{ Name = 'leading-zero minor component'; Content = '{"version":"1.02.3"}' },
    @{ Name = 'leading-zero patch component'; Content = '{"version":"1.2.03"}' },
    @{ Name = 'unicode decimal digits'; Content = ('{{"version":"{0}.{1}.{2}"}}' -f [char]0x0661, [char]0x0662, [char]0x0663) },
    @{ Name = 'missing version'; Content = '{}' },
    @{ Name = 'empty version'; Content = '{"version":""}' },
    @{ Name = 'malformed JSON'; Content = '{"version":' }
)
$invalidVersionRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dearstory-invalid-version-{0}" -f [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Force -Path $invalidVersionRoot | Out-Null
    foreach ($invalidVersionDocument in $invalidVersionDocuments) {
        $invalidVersionPath = Join-Path $invalidVersionRoot "$($invalidVersionDocument.Name).json"
        Set-Content -LiteralPath $invalidVersionPath -Value $invalidVersionDocument.Content -NoNewline
        $wasRejected = $false
        try {
            & $readVersionScript -Path $invalidVersionPath | Out-Null
        }
        catch {
            $wasRejected = $true
        }

        if (-not $wasRejected) {
            throw "Expected $($invalidVersionDocument.Name) version input to be rejected."
        }
    }
}
finally {
    Remove-Item -LiteralPath $invalidVersionRoot -Recurse -Force -ErrorAction SilentlyContinue
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
