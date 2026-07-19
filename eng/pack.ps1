[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageOutput = Join-Path $repositoryRoot 'artifacts\packages\dotnet'
$localFeed = Join-Path $repositoryRoot 'artifacts\packages\local-feed'
$packages = @(
    '.\src\protocol\dotnet\DearStory.Protocol\DearStory.Protocol.csproj',
    '.\src\core\dotnet\DearStory.Core\DearStory.Core.csproj',
    '.\sdk\dotnet\DearStory.Sdk\DearStory.Sdk.csproj',
    '.\sdk\dotnet\DearStory.Sdk.Generator\DearStory.Sdk.Generator.csproj'
)

New-Item -ItemType Directory -Force -Path $packageOutput, $localFeed | Out-Null
Remove-Item -Path (Join-Path $packageOutput '*.nupkg'), (Join-Path $localFeed '*.nupkg') -Force -ErrorAction SilentlyContinue

foreach ($package in $packages) {
    & dotnet pack $package -c $Configuration --no-restore "-p:PackageOutputPath=$packageOutput"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for '$package' (exit code $LASTEXITCODE)."
    }
}

$packageArtifacts = Get-ChildItem -Path $packageOutput -Filter '*.nupkg' -File
if ($packageArtifacts.Count -ne $packages.Count) {
    throw "Expected $($packages.Count) package artifacts in '$packageOutput', found $($packageArtifacts.Count)."
}

Copy-Item -Path $packageArtifacts.FullName -Destination $localFeed -Force
