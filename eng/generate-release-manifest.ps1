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
