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

$releaseOutput = & pwsh -NoProfile -File $releaseScript -ReleaseMode Local -ExpectedVersion $versionInfo.Version -SourceRef 'refs/heads/test' -SourceCommit '0123456789abcdef0123456789abcdef01234567' -SkipBuild -SkipTest -WhatIf 2>&1
$releaseLines = @($releaseOutput | ForEach-Object { $_.ToString() })
if (@($releaseLines | Select-String -SimpleMatch 'pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release').Count -ne 1) {
    throw 'Expected release WhatIf output to include the package step exactly once.'
}

if (@($releaseLines | Select-String -SimpleMatch 'cmake --install .\build\windows-msvc-debug --config Release --prefix .\artifacts\install\dearstory-release').Count -ne 1) {
    throw 'Expected release WhatIf output to include the staged C++ install exactly once.'
}

if (@($releaseLines | Select-String -SimpleMatch 'Compress-Archive').Count -ne 1) {
    throw 'Expected release WhatIf output to include the public C++ archive creation exactly once.'
}
