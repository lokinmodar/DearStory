$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$readVersionScript = Join-Path $repositoryRoot 'eng\read-version.ps1'
$releaseScript = Join-Path $repositoryRoot 'eng\release.ps1'
$manifestScript = Join-Path $repositoryRoot 'eng\generate-release-manifest.ps1'
$versionInfo = & $readVersionScript

if (-not (Test-Path -LiteralPath $releaseScript)) {
    throw "Expected release orchestrator '$releaseScript' to exist."
}

if (-not (Test-Path -LiteralPath $manifestScript)) {
    throw "Expected release manifest generator '$manifestScript' to exist."
}

$sampleRoot = Join-Path $repositoryRoot '.artifacts\release-script-test'
$manifestPath = Join-Path $sampleRoot 'release-manifest.json'
$dotnetDirectory = Join-Path $sampleRoot 'dotnet'
$cppDirectory = Join-Path $sampleRoot 'cpp'
try {
Remove-Item -LiteralPath $sampleRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dotnetDirectory, $cppDirectory | Out-Null

$packageIds = @(
    'DearStory.Protocol',
    'DearStory.Core',
    'DearStory.Sdk',
    'DearStory.Sdk.Generator'
)

foreach ($packageId in $packageIds) {
    Set-Content -LiteralPath (Join-Path $dotnetDirectory "$packageId.$($versionInfo.Version).nupkg") -Value $packageId -NoNewline
}

Set-Content -LiteralPath (Join-Path $cppDirectory "DearStory-cpp-$($versionInfo.Version)-windows-msvc-x64.zip") -Value 'zip' -NoNewline

& $manifestScript -Version $versionInfo.Version -SourceCommit '0123456789abcdef0123456789abcdef01234567' -SourceRef 'refs/heads/test' -ReleaseMode Local -ArtifactsRoot $sampleRoot -OutputPath $manifestPath
$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json
if ($manifest.packages.Count -ne 4) {
    throw 'Expected release-manifest.json to contain the four public .NET packages.'
}

$manifestPackageIds = @($manifest.packages | ForEach-Object { $_.id } | Sort-Object)
$expectedPackageIds = @($packageIds | Sort-Object)
if (($manifestPackageIds -join '|') -ne ($expectedPackageIds -join '|')) {
    throw 'Expected release-manifest.json to preserve the four public .NET package IDs.'
}

$unexpectedPackagePath = Join-Path $dotnetDirectory "DearStory.Unintended.$($versionInfo.Version).nupkg"
Remove-Item -LiteralPath (Join-Path $dotnetDirectory "DearStory.Protocol.$($versionInfo.Version).nupkg") -Force
Set-Content -LiteralPath $unexpectedPackagePath -Value 'unexpected' -NoNewline
try {
    & $manifestScript -Version $versionInfo.Version -SourceCommit '0123456789abcdef0123456789abcdef01234567' -SourceRef 'refs/heads/test' -ReleaseMode Local -ArtifactsRoot $sampleRoot -OutputPath $manifestPath
    throw 'Expected release-manifest.json generation to reject an unexpected public package set.'
}
catch {
    $expectedMessageFragment = 'Expected the canonical public .NET package set'
    if ($_.Exception.Message -notlike "*$expectedMessageFragment*") {
        throw "Expected manifest validation message containing '$expectedMessageFragment', got: $($_.Exception.Message)"
    }
}
finally {
    Remove-Item -LiteralPath $unexpectedPackagePath -Force -ErrorAction SilentlyContinue
    Set-Content -LiteralPath (Join-Path $dotnetDirectory "DearStory.Protocol.$($versionInfo.Version).nupkg") -Value 'DearStory.Protocol' -NoNewline
}

$additionalCppArchivePath = Join-Path $cppDirectory "DearStory-cpp-$($versionInfo.Version)-linux-clang-x64.zip"
Set-Content -LiteralPath $additionalCppArchivePath -Value 'zip' -NoNewline
try {
    & $manifestScript -Version $versionInfo.Version -SourceCommit '0123456789abcdef0123456789abcdef01234567' -SourceRef 'refs/heads/test' -ReleaseMode Local -ArtifactsRoot $sampleRoot -OutputPath $manifestPath
    throw 'Expected release-manifest.json generation to reject multiple public C++ archives.'
}
catch {
    $expectedMessageFragment = 'Expected exactly one public C++ archive'
    if ($_.Exception.Message -notlike "*$expectedMessageFragment*") {
        throw "Expected manifest validation message containing '$expectedMessageFragment', got: $($_.Exception.Message)"
    }
}
finally {
    Remove-Item -LiteralPath $additionalCppArchivePath -Force -ErrorAction SilentlyContinue
}

$releaseOutput = & pwsh -NoProfile -File $releaseScript -ReleaseMode Local -ExpectedVersion $versionInfo.Version -SourceRef 'refs/heads/test' -SourceCommit '0123456789abcdef0123456789abcdef01234567' -SkipBuild -SkipTest -WhatIf 2>&1
$releaseLines = @($releaseOutput | ForEach-Object { $_.ToString() })
if (@($releaseLines | Select-String -SimpleMatch ("pwsh -NoProfile -File {0} -Configuration Release" -f (Join-Path $repositoryRoot 'eng\pack.ps1'))).Count -ne 1) {
    throw 'Expected release WhatIf output to include the package step exactly once.'
}

$cmakeInstallLines = @($releaseLines | Select-String -SimpleMatch ("cmake --install {0} --config Release --prefix" -f (Join-Path $repositoryRoot 'build\windows-msvc-debug')))
if ($cmakeInstallLines.Count -ne 1 -or $cmakeInstallLines[0].Line -notmatch 'dearstory-release-[0-9a-f]{32}$') {
    throw 'Expected release WhatIf output to install C++ artifacts to a unique staging prefix.'
}

if (@($releaseLines | Select-String -SimpleMatch 'New-DearStoryDeterministicArchive').Count -ne 1) {
    throw 'Expected release WhatIf output to include deterministic public C++ archive creation exactly once.'
}

if (@($releaseLines | Select-String -SimpleMatch 'Compress-Archive').Count -ne 0) {
    throw 'Expected release WhatIf output not to use timestamp-bearing Compress-Archive output.'
}

$customOutputRoot = Join-Path $repositoryRoot '.artifacts\custom-release-output'
$customReleaseOutput = & pwsh -NoProfile -File $releaseScript -ReleaseMode Local -ExpectedVersion $versionInfo.Version -SourceRef 'refs/heads/test' -SourceCommit '0123456789abcdef0123456789abcdef01234567' -OutputRoot $customOutputRoot -SkipBuild -SkipTest -WhatIf 2>&1
$customReleaseLines = @($customReleaseOutput | ForEach-Object { $_.ToString() })
$expectedCustomReleaseDirectory = Join-Path $customOutputRoot $versionInfo.Version
if (@($customReleaseLines | Select-String -SimpleMatch $expectedCustomReleaseDirectory).Count -eq 0) {
    throw "Expected release WhatIf output to use custom OutputRoot '$customOutputRoot'."
}

$canonicalTag = "v$($versionInfo.Version)"
$canonicalTagRef = "refs/tags/$canonicalTag"
$isolatedReleaseRepositoryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dearstory-release-source-test-{0}" -f [guid]::NewGuid().ToString('N'))
$foreignRepositoryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dearstory-release-caller-test-{0}" -f [guid]::NewGuid().ToString('N'))
$foreignRepositoryLocationPushed = $false
try {
    & git clone --quiet $repositoryRoot $isolatedReleaseRepositoryRoot
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to create the isolated DearStory release repository.'
    }

    $isolatedReleaseScript = Join-Path $isolatedReleaseRepositoryRoot 'eng\release.ps1'
    Copy-Item -LiteralPath $releaseScript -Destination $isolatedReleaseScript -Force
    & git -C $isolatedReleaseRepositoryRoot config user.name 'DearStory Release Test'
    & git -C $isolatedReleaseRepositoryRoot config user.email 'release-test@dearstory.invalid'
    & git -C $isolatedReleaseRepositoryRoot config core.autocrlf false
    $tagCommit = (& git -C $isolatedReleaseRepositoryRoot rev-parse HEAD).Trim()
    & git -C $isolatedReleaseRepositoryRoot tag --force $canonicalTag $tagCommit | Out-Null

    & git clone --quiet $isolatedReleaseRepositoryRoot $foreignRepositoryRoot
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to create the unrelated caller repository.'
    }

    & git -C $foreignRepositoryRoot config user.name 'DearStory Release Test'
    & git -C $foreignRepositoryRoot config user.email 'release-test@dearstory.invalid'
    & git -C $foreignRepositoryRoot config core.autocrlf false
    Set-Content -LiteralPath (Join-Path $foreignRepositoryRoot 'foreign-provenance.txt') -Value 'unrelated repository commit' -NoNewline
    & git -C $foreignRepositoryRoot add foreign-provenance.txt
    & git -C $foreignRepositoryRoot commit --quiet -m 'test: create unrelated repository commit'
    $foreignCommit = (& git -C $foreignRepositoryRoot rev-parse HEAD).Trim()
    & git -C $foreignRepositoryRoot tag --force $canonicalTag $foreignCommit | Out-Null

    Push-Location $foreignRepositoryRoot
    $foreignRepositoryLocationPushed = $true
    $tagReleaseOutput = & pwsh -NoProfile -File $isolatedReleaseScript -ReleaseMode Tag -ExpectedVersion $versionInfo.Version -SourceRef $canonicalTagRef -SourceCommit $tagCommit -SkipBuild -SkipTest -WhatIf 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected Tag release to resolve provenance from the DearStory repository when invoked from an unrelated repository. Output: $($tagReleaseOutput -join [Environment]::NewLine)"
    }

    $tagReleaseLines = @($tagReleaseOutput | ForEach-Object { $_.ToString() })
    foreach ($expectedCommandPath in @(
        (Join-Path $isolatedReleaseRepositoryRoot 'eng\pack.ps1'),
        (Join-Path $isolatedReleaseRepositoryRoot 'eng\assert-public-package-boundaries.ps1'),
        (Join-Path $isolatedReleaseRepositoryRoot 'eng\generate-release-manifest.ps1'),
        (Join-Path $isolatedReleaseRepositoryRoot 'build\windows-msvc-debug')
    )) {
        if (@($tagReleaseLines | Select-String -SimpleMatch $expectedCommandPath).Count -ne 1) {
            throw "Expected release WhatIf output to anchor external invocation path '$expectedCommandPath' to the DearStory repository."
        }
    }

    & git -C $foreignRepositoryRoot tag --force $canonicalTag $tagCommit | Out-Null
    Set-Content -LiteralPath (Join-Path $isolatedReleaseRepositoryRoot 'head-provenance.txt') -Value 'checkout moved beyond canonical tag' -NoNewline
    & git -C $isolatedReleaseRepositoryRoot add head-provenance.txt
    & git -C $isolatedReleaseRepositoryRoot commit --quiet -m 'test: move DearStory HEAD beyond canonical tag'

    $headMismatchOutput = & pwsh -NoProfile -File $isolatedReleaseScript -ReleaseMode Tag -ExpectedVersion $versionInfo.Version -SourceRef $canonicalTagRef -SourceCommit $tagCommit -SkipBuild -SkipTest -WhatIf 2>&1
    if ($LASTEXITCODE -eq 0) {
        throw 'Expected Tag release to reject a DearStory checkout whose HEAD differs from the canonical tag commit.'
    }

    $headMismatchLines = @($headMismatchOutput | ForEach-Object { $_.ToString() })
    if (@($headMismatchLines | Select-String -SimpleMatch 'repository HEAD').Count -eq 0) {
        throw 'Expected Tag release HEAD mismatch failure to identify repository HEAD provenance.'
    }
}
finally {
    if ($foreignRepositoryLocationPushed) {
        Pop-Location
    }

    Remove-Item -LiteralPath $foreignRepositoryRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $isolatedReleaseRepositoryRoot -Recurse -Force -ErrorAction SilentlyContinue
}

foreach ($invalidTagRef in @('refs/heads/main', "refs/tags/v$($versionInfo.Version).0")) {
    $invalidTagOutput = & pwsh -NoProfile -File $releaseScript -ReleaseMode Tag -ExpectedVersion $versionInfo.Version -SourceRef $invalidTagRef -SourceCommit '0123456789abcdef0123456789abcdef01234567' -SkipBuild -SkipTest -WhatIf 2>&1
    if ($LASTEXITCODE -eq 0) {
        throw "Expected Tag release with SourceRef '$invalidTagRef' to fail."
    }

    $invalidTagLines = @($invalidTagOutput | ForEach-Object { $_.ToString() })
    if (@($invalidTagLines | Select-String -SimpleMatch 'refs/tags/v').Count -eq 0) {
        throw "Expected Tag release failure for '$invalidTagRef' to explain the required stable source tag."
    }
}

$stagingOutputRoot = Join-Path $repositoryRoot '.artifacts\staging-release-output'
$stagingReleaseOutput = & pwsh -NoProfile -File $releaseScript -ReleaseMode Local -ExpectedVersion $versionInfo.Version -SourceRef 'refs/heads/test' -SourceCommit '0123456789abcdef0123456789abcdef01234567' -OutputRoot $stagingOutputRoot -SkipBuild -SkipTest -WhatIf 2>&1
if ($LASTEXITCODE -ne 0) {
    throw 'Expected local release WhatIf invocation for staging coverage to succeed.'
}

$stagingReleaseLines = @($stagingReleaseOutput | ForEach-Object { $_.ToString() })
if (@($stagingReleaseLines | Select-String -SimpleMatch 'Prepare release staging tree').Count -ne 1) {
    throw 'Expected release WhatIf output to prepare a distinct staging tree.'
}

if (@($stagingReleaseLines | Select-String -SimpleMatch 'Promote completed release unit').Count -ne 1) {
    throw 'Expected release WhatIf output to promote the completed staging tree.'
}

if (@($stagingReleaseLines | Select-String -SimpleMatch '[System.IO.Directory]::Move').Count -ne 1) {
    throw 'Expected release WhatIf output to use a no-clobber directory move for promotion.'
}

if (@($stagingReleaseLines | Select-String -SimpleMatch 'Move-Item -LiteralPath').Count -ne 0) {
    throw 'Expected release promotion not to use Move-Item directory destination semantics.'
}

$deterministicRepositoryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dearstory-release-determinism-test-{0}" -f [guid]::NewGuid().ToString('N'))
$deterministicToolRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dearstory-release-tools-{0}" -f [guid]::NewGuid().ToString('N'))
$originalPath = $env:PATH
try {
    & git clone --quiet $repositoryRoot $deterministicRepositoryRoot
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to create the isolated DearStory deterministic-release repository.'
    }

    Copy-Item -LiteralPath $releaseScript -Destination (Join-Path $deterministicRepositoryRoot 'eng\release.ps1') -Force
    @'
[CmdletBinding()]
param([string]$Configuration = 'Release')

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = (& (Join-Path $PSScriptRoot 'read-version.ps1')).Version
$packageDirectory = Join-Path $repositoryRoot 'artifacts\packages\dotnet'
Remove-Item -LiteralPath $packageDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null
foreach ($packageId in @('DearStory.Protocol', 'DearStory.Core', 'DearStory.Sdk', 'DearStory.Sdk.Generator')) {
    Set-Content -LiteralPath (Join-Path $packageDirectory "$packageId.$version.nupkg") -Value $packageId -NoNewline
}
'@ | Set-Content -LiteralPath (Join-Path $deterministicRepositoryRoot 'eng\pack.ps1')

    @'
[CmdletBinding()]
param([Parameter(Mandatory)][string]$CppInstallPrefix)

if (-not (Test-Path -LiteralPath $CppInstallPrefix -PathType Container)) {
    throw "Expected fixture C++ install prefix '$CppInstallPrefix'."
}
'@ | Set-Content -LiteralPath (Join-Path $deterministicRepositoryRoot 'eng\assert-public-package-boundaries.ps1')

    New-Item -ItemType Directory -Force -Path $deterministicToolRoot | Out-Null
    @'
@echo off
setlocal
set "prefix="
:parse
if "%~1"=="" goto install
if /I "%~1"=="--prefix" set "prefix=%~2"
shift
goto parse
:install
if "%prefix%"=="" exit /b 2
mkdir "%prefix%\include\dearstory" >nul 2>nul
mkdir "%prefix%\lib\cmake\DearStory" >nul 2>nul
> "%prefix%\include\dearstory\fixture.hpp" echo // deterministic fixture
> "%prefix%\lib\cmake\DearStory\DearStoryConfig.cmake" echo # deterministic fixture
exit /b 0
'@ | Set-Content -LiteralPath (Join-Path $deterministicToolRoot 'cmake.cmd') -Encoding ascii

    $env:PATH = "$deterministicToolRoot;$originalPath"
    $deterministicCommit = (& git -C $deterministicRepositoryRoot rev-parse HEAD).Trim()
    $firstOutputRoot = Join-Path $deterministicRepositoryRoot 'first-release'
    $secondOutputRoot = Join-Path $deterministicRepositoryRoot 'second-release'

    $firstReleaseOutput = & pwsh -NoProfile -File (Join-Path $deterministicRepositoryRoot 'eng\release.ps1') -ReleaseMode Local -ExpectedVersion $versionInfo.Version -SourceRef 'refs/heads/test' -SourceCommit $deterministicCommit -OutputRoot $firstOutputRoot -SkipBuild -SkipTest 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected first deterministic release fixture to succeed. Output: $($firstReleaseOutput -join [Environment]::NewLine)"
    }

    Start-Sleep -Seconds 3

    $secondReleaseOutput = & pwsh -NoProfile -File (Join-Path $deterministicRepositoryRoot 'eng\release.ps1') -ReleaseMode Local -ExpectedVersion $versionInfo.Version -SourceRef 'refs/heads/test' -SourceCommit $deterministicCommit -OutputRoot $secondOutputRoot -SkipBuild -SkipTest 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected second deterministic release fixture to succeed. Output: $($secondReleaseOutput -join [Environment]::NewLine)"
    }

    $archiveName = "DearStory-cpp-$($versionInfo.Version)-windows-msvc-x64.zip"
    $firstReleaseRoot = Join-Path $firstOutputRoot $versionInfo.Version
    $secondReleaseRoot = Join-Path $secondOutputRoot $versionInfo.Version
    $firstArchiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $firstReleaseRoot "cpp\$archiveName")).Hash
    $secondArchiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $secondReleaseRoot "cpp\$archiveName")).Hash
    if ($firstArchiveHash -ne $secondArchiveHash) {
        throw "Expected same-commit C++ release archives to be byte-for-byte deterministic, got '$firstArchiveHash' and '$secondArchiveHash'."
    }

    $firstChecksumsHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $firstReleaseRoot 'SHA256SUMS')).Hash
    $secondChecksumsHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $secondReleaseRoot 'SHA256SUMS')).Hash
    if ($firstChecksumsHash -ne $secondChecksumsHash) {
        throw "Expected same-commit SHA256SUMS files to be byte-for-byte deterministic, got '$firstChecksumsHash' and '$secondChecksumsHash'."
    }

    $firstManifestHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $firstReleaseRoot 'release-manifest.json')).Hash
    $secondManifestHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $secondReleaseRoot 'release-manifest.json')).Hash
    if ($firstManifestHash -ne $secondManifestHash) {
        throw "Expected same-commit release-manifest.json files to be byte-for-byte deterministic, got '$firstManifestHash' and '$secondManifestHash'."
    }
}
finally {
    $env:PATH = $originalPath
    Remove-Item -LiteralPath $deterministicToolRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $deterministicRepositoryRoot -Recurse -Force -ErrorAction SilentlyContinue
}
}
finally {
    Remove-Item -LiteralPath $sampleRoot -Recurse -Force -ErrorAction SilentlyContinue
}
