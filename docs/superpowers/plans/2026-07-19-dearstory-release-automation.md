# DearStory Release Automation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one repository-owned version source and one coordinated release pipeline that validates, packages, and publishes the public DearStory C++ and C# library surface as one atomic product unit.

**Architecture:** Keep `eng/version.json` as the only version authority, derive `.NET` and CMake public package metadata directly from it, and assemble release outputs through PowerShell scripts under `artifacts/releases/0.1.0`. Use one GitHub Actions release workflow for tag and manual release paths, keep the GitHub Release in `draft` until NuGet and C++ assets are complete, and prove the full release unit in normal CI without external publication.

**Tech Stack:** PowerShell 7, .NET 10, MSBuild property functions, CMake 3.30+, GitHub Actions on `windows-2022`, NuGet.org, GitHub CLI, Pester, `Get-FileHash`, `Compress-Archive`.

## Global Constraints

- DearStory remains Dear ImGui-first and language-neutral.
- DearStory must remain consumable as a library in both C++ and C#.
- The C++ SDK and the C# SDK remain first-class surfaces.
- Public package/library surfaces must not depend on Windows-first runtime tooling.
- `Runner`, `Catalog`, `Host`, `Transport.Windows`, `Capture`, and `Docs` remain internal unless a later phase explicitly productizes them.
- Build/test/docs verification remains mandatory through:
  - `pwsh -NoProfile -File .\eng\build.ps1 ...`
  - `pwsh -NoProfile -File .\eng\test.ps1 ...`
- Real publication in this phase uses stable SemVer tags of the form `vX.Y.Z`.
- Release publication is atomic at the product level, not per-package.

---

## File structure

### Version authority and metadata derivation

- Create: `eng/version.json`
- Create: `eng/read-version.ps1`
- Create: `tests/unit/foundation/ReleaseVersion.Tests.ps1`
- Modify: `Directory.Build.props`
- Modify: `CMakeLists.txt`
- Modify: `eng/test.ps1`
- Modify: `tests/unit/foundation/BuildScripts.Tests.ps1`
- Modify: `tests/unit/foundation/PublicPackageBoundaries.Tests.ps1`

### Release-unit assembly

- Create: `eng/generate-release-manifest.ps1`
- Create: `eng/release.ps1`
- Create: `tests/unit/foundation/ReleaseScripts.Tests.ps1`
- Modify: `eng/test.ps1`

### Workflow automation

- Create: `.github/workflows/release.yml`
- Create: `tests/unit/foundation/ReleaseWorkflow.Tests.ps1`
- Modify: `.github/workflows/ci.yml`
- Modify: `eng/test.ps1`

### Maintainer documentation

- Modify: `README.md`
- Modify: `docs/guides/releasing-packages.md`

## Task 1: Move public version authority to `eng/version.json`

**Files:**
- Create: `eng/version.json`
- Create: `eng/read-version.ps1`
- Create: `tests/unit/foundation/ReleaseVersion.Tests.ps1`
- Modify: `Directory.Build.props`
- Modify: `CMakeLists.txt`
- Modify: `eng/test.ps1`
- Modify: `tests/unit/foundation/BuildScripts.Tests.ps1`
- Modify: `tests/unit/foundation/PublicPackageBoundaries.Tests.ps1`
- Test: `pwsh -NoProfile -File .\tests\unit\foundation\ReleaseVersion.Tests.ps1`

**Interfaces:**
- Consumes: the current public package version `0.1.0`, existing `eng/build.ps1`, existing `eng/test.ps1`, and the phase-2 public package boundary checks.
- Produces: `eng\read-version.ps1` returning `[pscustomobject]@{ Version = '0.1.0'; Tag = 'v0.1.0'; IsStableSemVer = $true }`, plus `.NET` and CMake public package metadata derived from `eng\version.json` instead of hard-coded literals.

- [ ] **Step 1: Write the failing version-source regression test**

Create `tests/unit/foundation/ReleaseVersion.Tests.ps1` so it proves the new version source does not exist yet and that `.NET`/CMake metadata are still hard-coded.

```powershell
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
```

- [ ] **Step 2: Run the new regression test to confirm it fails**

Run: `pwsh -NoProfile -File .\tests\unit\foundation\ReleaseVersion.Tests.ps1`

Expected: FAIL because `eng\version.json` and `eng\read-version.ps1` do not exist yet, and `Directory.Build.props` plus `CMakeLists.txt` still hard-code `0.1.0`.

- [ ] **Step 3: Add the canonical version file and the PowerShell reader**

Create `eng/version.json` and `eng/read-version.ps1`.

```json
{
  "version": "0.1.0"
}
```

```powershell
[CmdletBinding()]
param(
    [string]$Path = (Join-Path $PSScriptRoot 'version.json')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Version file '$Path' was not found."
}

$document = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
$version = [string]$document.version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Version file '$Path' does not contain a non-empty 'version' value."
}

if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    throw "Version '$version' is not a stable SemVer value."
}

[pscustomobject]@{
    Version = $version
    Tag = "v$version"
    IsStableSemVer = $true
}
```

- [ ] **Step 4: Derive `.NET` and CMake public package metadata from `eng/version.json`**

Update `Directory.Build.props` and `CMakeLists.txt` so they stop owning the version literal and instead read `eng\version.json`.

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <DearStoryVersionJsonPath>$(MSBuildThisFileDirectory)eng\version.json</DearStoryVersionJsonPath>
    <DearStoryVersionJson>$([System.IO.File]::ReadAllText('$(DearStoryVersionJsonPath)'))</DearStoryVersionJson>
    <VersionPrefix>$([System.Text.RegularExpressions.Regex]::Match('$(DearStoryVersionJson)', '&quot;version&quot;\s*:\s*&quot;([^&quot;]+)&quot;').Groups[1].Value)</VersionPrefix>
    <Version>$(VersionPrefix)</Version>
    <IsPackable>false</IsPackable>
    <Authors>Dante</Authors>
    <Company>Dante</Company>
    <RepositoryUrl>https://github.com/lokinmodar/DearStory</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageProjectUrl>https://github.com/lokinmodar/DearStory</PackageProjectUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <NoWarn></NoWarn>
  </PropertyGroup>
</Project>
```

```cmake
cmake_minimum_required(VERSION 3.30)

file(READ "${CMAKE_SOURCE_DIR}/eng/version.json" DEARSTORY_VERSION_JSON)
string(JSON DEARSTORY_VERSION ERROR_VARIABLE DEARSTORY_VERSION_ERROR GET "${DEARSTORY_VERSION_JSON}" version)
if(DEARSTORY_VERSION_ERROR)
    message(FATAL_ERROR "Failed to read DearStory version from eng/version.json: ${DEARSTORY_VERSION_ERROR}")
endif()

project(DearStory VERSION ${DEARSTORY_VERSION} LANGUAGES CXX)
```

- [ ] **Step 5: Update the canonical tests and script plumbing to use the new reader**

Make `eng/test.ps1` run the new version test, make `BuildScripts.Tests.ps1` derive `DearStoryPackageVersion` from `eng\read-version.ps1`, and make `PublicPackageBoundaries.Tests.ps1` compute the incompatible next-minor probe dynamically instead of hard-coding `0.2.0`.

```powershell
$readVersionScript = Join-Path $PSScriptRoot 'read-version.ps1'
$packageVersion = (& $readVersionScript).Version

Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\ReleaseVersion.Tests.ps1')
```

```powershell
$readVersionScript = Join-Path $repositoryRoot 'eng\read-version.ps1'
$versionInfo = & $readVersionScript
$packageVersion = $versionInfo.Version
```

```powershell
$versionInfo = & (Join-Path $repositoryRoot 'eng\read-version.ps1')
$currentVersion = [version]$versionInfo.Version
$nextMinorVersion = '{0}.{1}.{2}' -f $currentVersion.Major, ($currentVersion.Minor + 1), $currentVersion.Build

@"
set(PACKAGE_FIND_VERSION $nextMinorVersion)
include("$($buildDirectory.Replace('\', '/'))/DearStoryConfigVersion.cmake")
if(PACKAGE_VERSION_COMPATIBLE)
    message(FATAL_ERROR "$nextMinorVersion must not be compatible with DearStory $($versionInfo.Version)")
endif()
"@ | Set-Content -LiteralPath $versionScript -NoNewline
```

- [ ] **Step 6: Run the focused verification for version derivation**

Run:

```powershell
pwsh -NoProfile -File .\tests\unit\foundation\ReleaseVersion.Tests.ps1
pwsh -NoProfile -File .\tests\unit\foundation\BuildScripts.Tests.ps1
pwsh -NoProfile -File .\tests\unit\foundation\PublicPackageBoundaries.Tests.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
```

Expected: PASS. The new version test exits `0`, the foundation script guards stop hard-coding `0.1.0`, the package-boundary version check remains conservative for pre-`1.0`, and the Release build still succeeds with version metadata coming from `eng\version.json`.

- [ ] **Step 7: Commit**

```bash
git add eng/version.json eng/read-version.ps1 Directory.Build.props CMakeLists.txt eng/test.ps1 tests/unit/foundation/ReleaseVersion.Tests.ps1 tests/unit/foundation/BuildScripts.Tests.ps1 tests/unit/foundation/PublicPackageBoundaries.Tests.ps1
git commit -m "build: derive public version from eng/version.json"
```

## Task 2: Assemble the release unit and machine-readable manifest

**Files:**
- Create: `eng/generate-release-manifest.ps1`
- Create: `eng/release.ps1`
- Create: `tests/unit/foundation/ReleaseScripts.Tests.ps1`
- Modify: `eng/test.ps1`
- Test: `pwsh -NoProfile -File .\tests\unit\foundation\ReleaseScripts.Tests.ps1`

**Interfaces:**
- Consumes: `eng\read-version.ps1`, `eng\build.ps1`, `eng\test.ps1`, `eng\pack.ps1`, `cmake --install`, and the public package set created in phase 2.
- Produces: `eng\release.ps1` with parameters `-ReleaseMode`, `-ExpectedVersion`, `-SourceRef`, `-SourceCommit`, `-Configuration`, `-OutputRoot`, `-SkipBuild`, and `-SkipTest`, plus `release-manifest.json`, `SHA256SUMS`, the copied `.nupkg` set, and the zipped C++ public archive under `artifacts\releases\0.1.0\`.

- [ ] **Step 1: Write the failing release-script regression test**

Create `tests/unit/foundation/ReleaseScripts.Tests.ps1` so it proves the release orchestrator and manifest generator do not exist yet.

```powershell
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
```

- [ ] **Step 2: Run the new regression test to confirm it fails**

Run: `pwsh -NoProfile -File .\tests\unit\foundation\ReleaseScripts.Tests.ps1`

Expected: FAIL because `eng\release.ps1` and `eng\generate-release-manifest.ps1` do not exist yet.

- [ ] **Step 3: Implement the release manifest generator**

Create `eng/generate-release-manifest.ps1`.

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$SourceCommit,
    [Parameter(Mandatory)][string]$SourceRef,
    [Parameter(Mandatory)][ValidateSet('Tag', 'Manual', 'Local')][string]$ReleaseMode,
    [Parameter(Mandatory)][string]$ArtifactsRoot,
    [Parameter(Mandatory)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$dotnetDirectory = Join-Path $ArtifactsRoot 'dotnet'
$cppDirectory = Join-Path $ArtifactsRoot 'cpp'
$packages = Get-ChildItem -LiteralPath $dotnetDirectory -Filter '*.nupkg' -File | Sort-Object Name
$cppArchive = Get-ChildItem -LiteralPath $cppDirectory -Filter '*.zip' -File | Sort-Object Name | Select-Object -First 1

if ($packages.Count -ne 4) {
    throw "Expected exactly four public .NET packages in '$dotnetDirectory', found $($packages.Count)."
}

if (-not $cppArchive) {
    throw "Expected one public C++ archive in '$cppDirectory'."
}

$releaseRoot = [System.IO.Path]::GetFullPath($ArtifactsRoot)
$manifest = [ordered]@{
    version = $Version
    tag = "v$Version"
    releaseMode = $ReleaseMode
    sourceRef = $SourceRef
    sourceCommit = $SourceCommit
    packages = @(
        foreach ($package in $packages) {
            [ordered]@{
                id = [System.IO.Path]::GetFileNameWithoutExtension($package.Name).Substring(0, [System.IO.Path]::GetFileNameWithoutExtension($package.Name).LastIndexOf('.'))
                file = [System.IO.Path]::GetRelativePath($releaseRoot, $package.FullName).Replace('\', '/')
            }
        }
    )
    cppArtifact = [ordered]@{
        file = [System.IO.Path]::GetRelativePath($releaseRoot, $cppArchive.FullName).Replace('\', '/')
    }
    checksumsFile = 'SHA256SUMS'
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath
```

- [ ] **Step 4: Implement the release-unit orchestrator**

Create `eng/release.ps1` so it can run full verification or reuse an already-built tree through `-SkipBuild` and `-SkipTest`.

```powershell
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
```

- [ ] **Step 5: Run the new release-script test from the canonical test flow**

Update `eng/test.ps1` so it executes the new foundation test.

```powershell
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\ReleaseScripts.Tests.ps1')
```

- [ ] **Step 6: Run the focused verification for release-unit assembly**

Run:

```powershell
pwsh -NoProfile -File .\tests\unit\foundation\ReleaseScripts.Tests.ps1
$version = (& .\eng\read-version.ps1).Version
$commit = (git rev-parse HEAD).Trim()
pwsh -NoProfile -File .\eng\release.ps1 -ReleaseMode Local -ExpectedVersion $version -SourceRef refs/heads/feature/phase-3-release-automation -SourceCommit $commit -SkipBuild -SkipTest
```

Expected: PASS. The foundation test exits `0`, `artifacts\releases\0.1.0\dotnet\` contains the four `.nupkg` files, `artifacts\releases\0.1.0\cpp\DearStory-cpp-0.1.0-windows-msvc-x64.zip` exists, `SHA256SUMS` exists, and `release-manifest.json` enumerates the exact produced files.

- [ ] **Step 7: Commit**

```bash
git add eng/generate-release-manifest.ps1 eng/release.ps1 eng/test.ps1 tests/unit/foundation/ReleaseScripts.Tests.ps1
git commit -m "feat: assemble release unit from canonical version"
```

## Task 3: Add the release workflow and make CI prove the release unit

**Files:**
- Create: `.github/workflows/release.yml`
- Create: `tests/unit/foundation/ReleaseWorkflow.Tests.ps1`
- Modify: `.github/workflows/ci.yml`
- Modify: `eng/test.ps1`
- Test: `pwsh -NoProfile -Command "Invoke-Pester -Script .\tests\unit\foundation\ReleaseWorkflow.Tests.ps1"`

**Interfaces:**
- Consumes: `eng\read-version.ps1`, `eng\release.ps1`, GitHub-hosted `windows-2022` runners, the protected `release` environment, `NUGET_API_KEY`, and the draft-first publication policy from the approved design.
- Produces: one `release.yml` workflow with `validate`, `build_release_unit`, and `publish` jobs; one CI dry-run step that generates `artifacts\releases\0.1.0` without publishing it; and one workflow regression test that guards trigger shape, permissions, draft-first behavior, and CI release-unit generation.

- [ ] **Step 1: Write the failing workflow regression test**

Create `tests/unit/foundation/ReleaseWorkflow.Tests.ps1`.

```powershell
$ErrorActionPreference = 'Stop'

Describe 'Release workflow' {
    It 'supports tag and manual release triggers' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match "tags:\s*`r?`n\s*-\s*'v\*\.\*\.\*'"
        $workflow | Should Match 'workflow_dispatch:'
        $workflow | Should Match 'ref:'
        $workflow | Should Match 'version:'
    }

    It 'uses draft-first publication and the protected release environment' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match 'environment:\s*release'
        $workflow | Should Match 'contents:\s*write'
        $workflow | Should Match '--draft'
        $workflow | Should Match 'dotnet nuget push'
    }

    It 'makes CI generate the local release unit' {
        $workflow = Get-Content .\.github\workflows\ci.yml -Raw
        $workflow | Should Match 'eng\\release\.ps1 -ReleaseMode Local'
        $workflow | Should Match 'artifacts/releases'
    }
}
```

- [ ] **Step 2: Run the workflow regression test to confirm it fails**

Run: `pwsh -NoProfile -Command "Invoke-Pester -Script .\tests\unit\foundation\ReleaseWorkflow.Tests.ps1"`

Expected: FAIL because `.github\workflows\release.yml` does not exist yet and `ci.yml` does not generate the local release unit.

- [ ] **Step 3: Implement the guarded release workflow**

Create `.github/workflows/release.yml`.

```yaml
name: release

on:
  push:
    tags:
      - 'v*.*.*'
  workflow_dispatch:
    inputs:
      ref:
        description: 'Commit, branch or tag to release'
        required: true
        type: string
      version:
        description: 'Version expected in eng/version.json'
        required: true
        type: string

permissions:
  contents: read

concurrency:
  group: dearstory-release-${{ github.event_name == 'workflow_dispatch' && inputs.version || github.ref_name }}
  cancel-in-progress: false

jobs:
  validate:
    runs-on: windows-2022
    outputs:
      version: ${{ steps.context.outputs.version }}
      tag_name: ${{ steps.context.outputs.tag_name }}
      source_ref: ${{ steps.context.outputs.source_ref }}
      source_commit: ${{ steps.context.outputs.source_commit }}
      checkout_ref: ${{ steps.context.outputs.checkout_ref }}
      release_mode: ${{ steps.context.outputs.release_mode }}
    steps:
      - name: Checkout selected ref
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          ref: ${{ github.event_name == 'workflow_dispatch' && inputs.ref || github.ref }}

      - name: Resolve release context
        id: context
        shell: pwsh
        run: |
          $versionInfo = & .\eng\read-version.ps1
          $tagName = $versionInfo.Tag

          if ('${{ github.event_name }}' -eq 'workflow_dispatch') {
            if ('${{ inputs.version }}' -ne $versionInfo.Version) {
              throw "Manual release version '${{ inputs.version }}' does not match repository version '$($versionInfo.Version)'."
            }

            git fetch origin main --depth=1
            $sourceCommit = (git rev-parse '${{ inputs.ref }}').Trim()
            git merge-base --is-ancestor $sourceCommit origin/main
            if ($LASTEXITCODE -ne 0) {
              throw "Manual release ref '${{ inputs.ref }}' is not reachable from origin/main."
            }

            "version=$($versionInfo.Version)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
            "tag_name=$tagName" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
            "source_ref=${{ inputs.ref }}" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
            "source_commit=$sourceCommit" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
            "checkout_ref=$sourceCommit" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
            "release_mode=Manual" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
            exit 0
          }

          if ($env:GITHUB_REF_NAME -ne $tagName) {
            throw "Tag '$env:GITHUB_REF_NAME' does not match repository version tag '$tagName'."
          }

          "version=$($versionInfo.Version)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
          "tag_name=$tagName" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
          "source_ref=$env:GITHUB_REF" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
          "source_commit=$(git rev-parse HEAD)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
          "checkout_ref=$env:GITHUB_SHA" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
          "release_mode=Tag" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append

  build_release_unit:
    needs: validate
    runs-on: windows-2022
    env:
      VCPKG_ROOT: ${{ github.workspace }}\vcpkg
      VCPKG_DEFAULT_BINARY_CACHE: ${{ github.workspace }}\.cache\vcpkg\archives
      NUGET_PACKAGES: ${{ github.workspace }}\.cache\nuget\packages
    steps:
      - name: Checkout validated commit
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          ref: ${{ needs.validate.outputs.checkout_ref }}

      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
          cache: true
          cache-dependency-path: |
            **/packages.lock.json

      - name: Prepare vcpkg
        shell: pwsh
        run: |
          git clone https://github.com/microsoft/vcpkg $env:VCPKG_ROOT
          git -C $env:VCPKG_ROOT checkout 1db84273378ff8e2d30e7bc7fdd5d1cb4f4260fc
          & "$env:VCPKG_ROOT\bootstrap-vcpkg.bat" -disableMetrics
          New-Item -ItemType Directory -Force -Path .\artifacts\logs, .\.cache\vcpkg\archives, .\.cache\nuget\packages | Out-Null

      - name: Doctor
        shell: pwsh
        run: pwsh -NoProfile -File .\eng\doctor.ps1

      - name: Check generated protocol sources
        shell: pwsh
        run: pwsh -NoProfile -File .\eng\generate-protocol.ps1 -Check

      - name: Build release unit
        shell: pwsh
        run: |
          pwsh -NoProfile -File .\eng\release.ps1 `
            -ReleaseMode ${{ needs.validate.outputs.release_mode }} `
            -ExpectedVersion ${{ needs.validate.outputs.version }} `
            -SourceRef "${{ needs.validate.outputs.source_ref }}" `
            -SourceCommit "${{ needs.validate.outputs.source_commit }}"

      - name: Upload release unit
        uses: actions/upload-artifact@v4
        with:
          name: dearstory-release-unit-${{ needs.validate.outputs.version }}
          path: artifacts/releases/${{ needs.validate.outputs.version }}
          if-no-files-found: error

  publish:
    needs: [validate, build_release_unit]
    runs-on: windows-2022
    environment: release
    permissions:
      contents: write
    steps:
      - name: Download release unit
        uses: actions/download-artifact@v4
        with:
          name: dearstory-release-unit-${{ needs.validate.outputs.version }}
          path: artifacts/releases/${{ needs.validate.outputs.version }}

      - name: Publish NuGet packages and finalize draft release
        shell: pwsh
        env:
          GH_TOKEN: ${{ github.token }}
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: |
          $version = '${{ needs.validate.outputs.version }}'
          $tag = '${{ needs.validate.outputs.tag_name }}'
          $releaseRoot = Join-Path $PWD "artifacts\releases\$version"
          $manifestPath = Join-Path $releaseRoot 'release-manifest.json'
          $packageBaseUrl = 'https://api.nuget.org/v3-flatcontainer'
          $packageIds = @(
            'DearStory.Protocol',
            'DearStory.Core',
            'DearStory.Sdk',
            'DearStory.Sdk.Generator'
          )

          $publishedPackages = @()
          foreach ($packageId in $packageIds) {
            $indexUrl = '{0}/{1}/index.json' -f $packageBaseUrl, $packageId.ToLowerInvariant()
            try {
              $index = Invoke-RestMethod -Uri $indexUrl
              if ($index.versions -contains $version) {
                $publishedPackages += $packageId
              }
            }
            catch {
            }
          }

          if ($publishedPackages.Count -gt 0 -and $publishedPackages.Count -lt $packageIds.Count) {
            throw "Partial NuGet publication already exists for version $version: $($publishedPackages -join ', ')."
          }

          gh release view $tag --json isDraft 2>$null
          if ($LASTEXITCODE -ne 0) {
            gh release create $tag --target '${{ needs.validate.outputs.source_commit }}' --draft --title "DearStory $tag" --notes-file $manifestPath
          }

          if ($publishedPackages.Count -eq 0) {
            foreach ($packageId in $packageIds) {
              $packagePath = Join-Path $releaseRoot "dotnet\$packageId.$version.nupkg"
              dotnet nuget push $packagePath --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json
              if ($LASTEXITCODE -ne 0) {
                throw "NuGet publication failed for '$packageId'."
              }
            }
          }

          gh release upload $tag "$releaseRoot\cpp\DearStory-cpp-$version-windows-msvc-x64.zip" "$releaseRoot\SHA256SUMS" "$releaseRoot\release-manifest.json" --clobber
          gh release edit $tag --draft=false --title "DearStory $tag" --notes-file $manifestPath
```

- [ ] **Step 4: Make normal CI generate the dry-run release unit and run the new workflow test**

Update `.github/workflows/ci.yml` and `eng/test.ps1`.

```yaml
      - name: Prepare release unit
        shell: pwsh
        run: |
          $version = (& .\eng\read-version.ps1).Version
          pwsh -NoProfile -File .\eng\release.ps1 -ReleaseMode Local -ExpectedVersion $version -SourceRef $env:GITHUB_REF -SourceCommit $env:GITHUB_SHA -SkipBuild -SkipTest

      - name: Upload release dry-run artifact
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a
        with:
          name: dearstory-release-unit
          path: artifacts/releases
          if-no-files-found: error
```

```powershell
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-Command', 'Invoke-Pester -Script .\tests\unit\foundation\ReleaseWorkflow.Tests.ps1')
```

- [ ] **Step 5: Run the focused workflow verification**

Run:

```powershell
pwsh -NoProfile -Command "Invoke-Pester -Script .\tests\unit\foundation\ReleaseWorkflow.Tests.ps1"
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release
git diff --check
```

Expected: PASS. The workflow regression test exits `0`, `eng\test.ps1` now exercises version, release-script, and workflow guards, and whitespace/conflict-marker checks stay clean.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/ci.yml .github/workflows/release.yml eng/test.ps1 tests/unit/foundation/ReleaseWorkflow.Tests.ps1
git commit -m "ci: automate coordinated release publication"
```

## Task 4: Document the atomic release workflow for maintainers

**Files:**
- Modify: `README.md`
- Modify: `docs/guides/releasing-packages.md`
- Test: `Get-Content .\docs\guides\releasing-packages.md | Select-String 'eng/version.json'`

**Interfaces:**
- Consumes: `eng\version.json`, `eng\release.ps1`, `.github\workflows\release.yml`, the public package set, and the draft-first publication policy.
- Produces: maintainer-facing documentation that explains the repository-owned version file, the automatic tag path, the manual `workflow_dispatch` path, the atomic release unit, and the expectation that partial publication leaves the GitHub Release in `draft`.

- [ ] **Step 1: Write the missing release-automation documentation expectations**

Add the missing headings and checklist lines to `docs/guides/releasing-packages.md` so the current doc must mention the new version source and both release paths.

```markdown
## Canonical version source

The public release version is declared only in `eng/version.json`.

## Release workflow entrypoints

- Automatic tag release: `vX.Y.Z`
- Manual release: `workflow_dispatch` with `ref` and `version`

## Atomic release behavior

The GitHub Release remains in `draft` until the four NuGet packages, the
public C++ archive, `SHA256SUMS`, and `release-manifest.json` are all present.
```

- [ ] **Step 2: Confirm the current documentation still reflects the pre-automation release flow**

Run:

```powershell
Get-Content .\docs\guides\releasing-packages.md | Select-String 'The current CMake project version is the source of the C\+\+ package version'
Get-Content .\README.md | Select-String 'tagged-release workflow'
```

Expected: PASS, showing the guide still treats CMake as the source of truth and does not yet describe `eng\version.json`, the manual workflow, or the atomic draft-first publication rule.

- [ ] **Step 3: Update the maintainer documentation to match the implemented release policy**

Update `README.md` and `docs/guides/releasing-packages.md`.

```markdown
DearStory release automation now uses `eng/version.json` as the only public
version source. Maintainers update that file in a reviewed PR, then release the
coordinated public package surface through `.github/workflows/release.yml`.
```

```markdown
1. Update `eng/version.json` and merge the change to `main`.
2. Release through either:
   - a tag named `v0.1.0`; or
   - the manual `release` workflow with `ref` and `version`.
3. The workflow validates that the trigger version matches `eng/version.json`.
4. The workflow keeps the GitHub Release in `draft` until:
   - `DearStory.Protocol.0.1.0.nupkg`
   - `DearStory.Core.0.1.0.nupkg`
   - `DearStory.Sdk.0.1.0.nupkg`
   - `DearStory.Sdk.Generator.0.1.0.nupkg`
   - `DearStory-cpp-0.1.0-windows-msvc-x64.zip`
   - `SHA256SUMS`
   - `release-manifest.json`
   are all present.
```

- [ ] **Step 4: Run the final verification for the documented release flow**

Run:

```powershell
$version = (& .\eng\read-version.ps1).Version
$commit = (git rev-parse HEAD).Trim()
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\release.ps1 -ReleaseMode Local -ExpectedVersion $version -SourceRef refs/heads/feature/phase-3-release-automation -SourceCommit $commit -SkipBuild -SkipTest
Get-Content .\README.md | Select-String 'eng/version.json'
Get-Content .\docs\guides\releasing-packages.md | Select-String 'workflow_dispatch'
git diff --check
```

Expected: PASS. The repository still clears the canonical Release build/test gates, the local release unit can be regenerated from the current commit, the docs mention `eng\version.json` and `workflow_dispatch`, and the working tree has no whitespace or conflict-marker problems.

- [ ] **Step 5: Commit**

```bash
git add README.md docs/guides/releasing-packages.md
git commit -m "docs: document release automation workflow"
```

## Self-review

- Spec coverage: Task 1 implements the canonical version file and metadata derivation; Task 2 implements the release-unit and manifest scripts; Task 3 implements the release workflow plus CI dry-run proof; Task 4 documents the maintainer-facing release flow and verifies it against the implemented commands.
- Placeholder scan: The plan uses concrete file names, concrete commands, and concrete `0.1.0` release-unit examples instead of `<version>` placeholders because the current repository version is `0.1.0`.
- Type consistency: `eng\read-version.ps1` always produces `Version`, `Tag`, and `IsStableSemVer`; `eng\release.ps1` always consumes `-ExpectedVersion`, `-SourceRef`, `-SourceCommit`, `-ReleaseMode`, `-SkipBuild`, and `-SkipTest`; `release-manifest.json` always contains four public package entries, one `cppArtifact`, and `checksumsFile`.

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-19-dearstory-release-automation.md`. Two execution options:

1. Subagent-Driven (recommended) - I dispatch a fresh subagent per task, review between tasks, fast iteration
2. Inline Execution - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
