[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Coverage
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

function Test-DearStoryCoveragePackage {
    param(
        [Parameter(Mandatory)][string]$CoveragePath,
        [Parameter(Mandatory)][string]$PackageName
    )

    [xml]$document = Get-Content -LiteralPath $CoveragePath
    return @($document.coverage.packages.package | Where-Object { [string]$_.name -eq $PackageName }).Count -gt 0
}

$ctestArguments = @('--preset', 'windows-msvc-debug', '--output-on-failure')
$dotnetTestArguments = @('test', '.\DearStory.slnx', '--no-build', '-m:1')

if ($Configuration -eq 'Release') {
    $ctestArguments += @('-C', 'Release')
    $dotnetTestArguments += @('-c', 'Release')
}

Invoke-DearStoryCommand -Executable 'ctest' -Arguments $ctestArguments
Invoke-DearStoryCommand -Executable 'dotnet' -Arguments $dotnetTestArguments
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\Doctor.Tests.ps1')
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\BuildScripts.Tests.ps1')
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\CoverageGate.Tests.ps1')

if (-not $Coverage) {
    return
}

if ($Configuration -ne 'Release') {
    throw 'Coverage collection requires -Configuration Release.'
}

$artifactsCoverageDirectory = '.\artifacts\coverage'
$managedCoverageDirectory = Join-Path $artifactsCoverageDirectory 'managed'
$nativeCoveragePath = Join-Path $artifactsCoverageDirectory 'native.xml'
$openCppCoveragePath = if ([string]::IsNullOrWhiteSpace($env:DEARSTORY_OPENCPPCOVERAGE_PATH)) {
    Join-Path ${env:ProgramFiles} 'OpenCppCoverage\OpenCppCoverage.exe'
}
else {
    $env:DEARSTORY_OPENCPPCOVERAGE_PATH
}
$nativeCoverageArguments = @(
    '--quiet',
    '--sources',
    [System.IO.Path]::GetFullPath((Join-Path $PWD 'src\protocol\cpp\src*')),
    '--export_type',
    ('cobertura:{0}' -f [System.IO.Path]::GetFullPath((Join-Path $PWD $nativeCoveragePath))),
    '--',
    [System.IO.Path]::GetFullPath((Join-Path $PWD 'artifacts\bin\native\Debug\dearstory-protocol-cpp-tests.exe'))
)

$managedCoverageArguments = @(
    'test',
    '.\DearStory.slnx',
    '-c',
    'Release',
    '--no-build',
    '-m:1',
    '--collect:XPlat Code Coverage',
    '--results-directory',
    '.\artifacts\coverage\managed'
)

$shouldExecute = -not $WhatIfPreference
if ($shouldExecute) {
    if (-not (Test-Path -LiteralPath $openCppCoveragePath)) {
        throw "OpenCppCoverage was not found at '$openCppCoveragePath'."
    }

    if (Test-Path -LiteralPath $managedCoverageDirectory) {
        Remove-Item -LiteralPath $managedCoverageDirectory -Recurse -Force
    }

    if (Test-Path -LiteralPath $nativeCoveragePath) {
        Remove-Item -LiteralPath $nativeCoveragePath -Force
    }

    New-Item -ItemType Directory -Force -Path $artifactsCoverageDirectory, $managedCoverageDirectory | Out-Null
}

Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--build', '--preset', 'windows-msvc-debug')
Invoke-DearStoryCommand -Executable $openCppCoveragePath -Arguments $nativeCoverageArguments
Invoke-DearStoryCommand -Executable 'dotnet' -Arguments $managedCoverageArguments

$coverageGateArguments = @('-NoProfile', '-File', '.\eng\assert-coverage.ps1', $nativeCoveragePath)
if ($shouldExecute) {
    $managedCoverageFiles = Get-ChildItem -Path $managedCoverageDirectory -Recurse -Filter 'coverage.cobertura.xml' -File |
        Select-Object -ExpandProperty FullName |
        Where-Object { Test-DearStoryCoveragePackage -CoveragePath $_ -PackageName 'DearStory.Protocol' }

    if ($managedCoverageFiles.Count -eq 0) {
        throw "Managed Cobertura output for package 'DearStory.Protocol' was not found under '$managedCoverageDirectory'."
    }

    $coverageGateArguments += $managedCoverageFiles
}
else {
    $coverageGateArguments += '.\artifacts\coverage\managed\coverage.cobertura.xml'
}

Invoke-DearStoryCommand -Executable 'pwsh' -Arguments $coverageGateArguments
