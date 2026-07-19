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
$cppArchives = @(Get-ChildItem -LiteralPath $cppDirectory -Filter '*.zip' -File | Sort-Object Name)
$expectedPackageIds = @(
    'DearStory.Protocol',
    'DearStory.Core',
    'DearStory.Sdk',
    'DearStory.Sdk.Generator'
)
$expectedPackageNames = @($expectedPackageIds | ForEach-Object { "$_.$Version.nupkg" } | Sort-Object)
$actualPackageNames = @($packages | ForEach-Object Name)

if ($packages.Count -ne 4) {
    throw "Expected exactly four public .NET packages in '$dotnetDirectory', found $($packages.Count)."
}

if (Compare-Object -ReferenceObject $expectedPackageNames -DifferenceObject $actualPackageNames) {
    throw "Expected the canonical public .NET package set in '$dotnetDirectory': $($expectedPackageNames -join ', ')."
}

if ($cppArchives.Count -ne 1) {
    throw "Expected exactly one public C++ archive in '$cppDirectory', found $($cppArchives.Count)."
}

$cppArchive = $cppArchives[0]
$expectedCppArchiveName = "DearStory-cpp-$Version-windows-msvc-x64.zip"
if ($cppArchive.Name -ne $expectedCppArchiveName) {
    throw "Expected public C++ archive '$expectedCppArchiveName' in '$cppDirectory', found '$($cppArchive.Name)'."
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
                id = $package.Name.Substring(0, $package.Name.Length - (".$Version.nupkg").Length)
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
