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
    if ($_.Exception.Message -eq 'Expected release-manifest.json generation to reject an unexpected public package set.') {
        throw
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
    if ($_.Exception.Message -eq 'Expected release-manifest.json generation to reject multiple public C++ archives.') {
        throw
    }
}
finally {
    Remove-Item -LiteralPath $additionalCppArchivePath -Force -ErrorAction SilentlyContinue
}

$releaseOutput = & pwsh -NoProfile -File $releaseScript -ReleaseMode Local -ExpectedVersion $versionInfo.Version -SourceRef 'refs/heads/test' -SourceCommit '0123456789abcdef0123456789abcdef01234567' -SkipBuild -SkipTest -WhatIf 2>&1
$releaseLines = @($releaseOutput | ForEach-Object { $_.ToString() })
if (@($releaseLines | Select-String -SimpleMatch 'pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release').Count -ne 1) {
    throw 'Expected release WhatIf output to include the package step exactly once.'
}

$cmakeInstallLines = @($releaseLines | Select-String -SimpleMatch 'cmake --install .\build\windows-msvc-debug --config Release --prefix')
if ($cmakeInstallLines.Count -ne 1 -or $cmakeInstallLines[0].Line -notmatch 'dearstory-release-[0-9a-f]{32}$') {
    throw 'Expected release WhatIf output to install C++ artifacts to a unique staging prefix.'
}

if (@($releaseLines | Select-String -SimpleMatch 'Compress-Archive').Count -ne 1) {
    throw 'Expected release WhatIf output to include the public C++ archive creation exactly once.'
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
$temporaryTagCreated = $false
try {
    $tagCommit = (& git rev-parse -q --verify "$canonicalTag^{commit}" 2>$null | Select-Object -First 1)
    if ($tagCommit) {
        $tagCommit = $tagCommit.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($tagCommit)) {
        $tagCommit = (& git rev-parse HEAD).Trim()
        & git tag $canonicalTag $tagCommit
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to create temporary tag '$canonicalTag'."
        }

        $temporaryTagCreated = $true
    }

    $tagReleaseOutput = & pwsh -NoProfile -File $releaseScript -ReleaseMode Tag -ExpectedVersion $versionInfo.Version -SourceRef $canonicalTagRef -SourceCommit $tagCommit -SkipBuild -SkipTest -WhatIf 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw 'Expected Tag release WhatIf invocation with the canonical stable tag and commit to succeed.'
    }

    $mismatchedCommit = (& git rev-parse HEAD).Trim()
    if ($mismatchedCommit -eq $tagCommit) {
        $mismatchedCommit = (& git rev-parse HEAD^).Trim()
    }

    $mismatchedTagOutput = & pwsh -NoProfile -File $releaseScript -ReleaseMode Tag -ExpectedVersion $versionInfo.Version -SourceRef $canonicalTagRef -SourceCommit $mismatchedCommit -SkipBuild -SkipTest -WhatIf 2>&1
    if ($LASTEXITCODE -eq 0) {
        throw 'Expected Tag release with a commit different from the canonical tag commit to fail.'
    }
}
finally {
    if ($temporaryTagCreated) {
        & git tag -d $canonicalTag | Out-Null
    }
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

if (@($stagingReleaseLines | Select-String -SimpleMatch 'Move-Item -LiteralPath').Count -ne 1) {
    throw 'Expected release WhatIf output to atomically move the completed release unit into place.'
}
