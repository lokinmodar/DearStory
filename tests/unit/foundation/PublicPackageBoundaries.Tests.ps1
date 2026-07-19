[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$buildDirectory = Join-Path $repositoryRoot 'build\windows-msvc-debug'
$installPrefix = Join-Path $repositoryRoot '.artifacts\package-boundaries-test\install'
$guardScript = Join-Path $repositoryRoot 'eng\assert-public-package-boundaries.ps1'
$versionInfo = & (Join-Path $repositoryRoot 'eng\read-version.ps1')
$currentVersion = [version]$versionInfo.Version
$nextMinorVersion = '{0}.{1}.{2}' -f $currentVersion.Major, ($currentVersion.Minor + 1), $currentVersion.Build

if (Test-Path -LiteralPath $installPrefix) {
    Remove-Item -LiteralPath $installPrefix -Recurse -Force
}

cmake --install $buildDirectory --config $Configuration --prefix $installPrefix
if ($LASTEXITCODE -ne 0) {
    throw "Expected C++ package install to succeed, got $LASTEXITCODE."
}

$licensePath = Join-Path $installPrefix 'share\licenses\DearStory\LICENSE'
if (-not (Test-Path -LiteralPath $licensePath)) {
    throw "Expected installed C++ package to contain MIT license at '$licensePath'."
}

& pwsh -NoProfile -File $guardScript -CppInstallPrefix $installPrefix
if ($LASTEXITCODE -ne 0) {
    throw "Expected public C++ package boundary guard to accept the installed package, got $LASTEXITCODE."
}

$transportHeaderDirectory = Join-Path $installPrefix 'include\dearstory\transports\windows'
New-Item -ItemType Directory -Force -Path $transportHeaderDirectory | Out-Null
& pwsh -NoProfile -File $guardScript -CppInstallPrefix $installPrefix 2>$null
if ($LASTEXITCODE -eq 0) {
    throw 'Expected public C++ package boundary guard to reject installed transport headers.'
}
Remove-Item -LiteralPath (Join-Path $installPrefix 'include\dearstory\transports') -Recurse -Force

$targetsPath = Join-Path $installPrefix 'lib\cmake\DearStory\DearStoryTargets.cmake'
Add-Content -LiteralPath $targetsPath -Value 'add_library(DearStory::TransportsCpp INTERFACE IMPORTED)'
& pwsh -NoProfile -File $guardScript -CppInstallPrefix $installPrefix 2>$null
if ($LASTEXITCODE -eq 0) {
    throw 'Expected public C++ package boundary guard to reject exported internal transport metadata.'
}

$versionScript = Join-Path $repositoryRoot '.artifacts\package-boundaries-test\version-check.cmake'
@"
set(PACKAGE_FIND_VERSION $nextMinorVersion)
include("$($buildDirectory.Replace('\', '/'))/DearStoryConfigVersion.cmake")
if(PACKAGE_VERSION_COMPATIBLE)
    message(FATAL_ERROR "$nextMinorVersion must not be compatible with DearStory $($versionInfo.Version)")
endif()
"@ | Set-Content -LiteralPath $versionScript -NoNewline

cmake -P $versionScript
if ($LASTEXITCODE -ne 0) {
    throw "Expected newer pre-1.0 CMake package request to be incompatible, got $LASTEXITCODE."
}
