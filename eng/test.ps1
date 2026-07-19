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

$managedTestProjects = @(
    '.\tests\unit\protocol\dotnet\DearStory.Protocol.Tests\DearStory.Protocol.Tests.csproj',
    '.\tests\unit\protocol\dotnet\DearStory.ProtocolGenerator.Tests\DearStory.ProtocolGenerator.Tests.csproj',
    '.\tests\integration\protocol\DearStory.Protocol.IntegrationTests\DearStory.Protocol.IntegrationTests.csproj',
    '.\tests\e2e\protocol\DearStory.Protocol.E2ETests\DearStory.Protocol.E2ETests.csproj',
    '.\tests\contract\protocol\DearStory.Protocol.ContractTests\DearStory.Protocol.ContractTests.csproj',
    '.\tests\conformance\hosts\DearStory.HostConformance.Tests\DearStory.HostConformance.Tests.csproj',
    '.\tests\unit\catalog\dotnet\DearStory.Catalog.Tests\DearStory.Catalog.Tests.csproj',
    '.\tests\unit\capture\dotnet\DearStory.Capture.Tests\DearStory.Capture.Tests.csproj',
    '.\tests\unit\core\dotnet\DearStory.Core.Tests\DearStory.Core.Tests.csproj',
    '.\tests\contract\core\DearStory.Core.ContractTests\DearStory.Core.ContractTests.csproj',
    '.\tests\unit\docs\dotnet\DearStory.Docs.Tests\DearStory.Docs.Tests.csproj',
    '.\tests\integration\windows\DearStory.WindowsSlice.Tests\DearStory.WindowsSlice.Tests.csproj',
    '.\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj',
    '.\tests\unit\runner\dotnet\DearStory.Runner.Tests\DearStory.Runner.Tests.csproj',
    '.\tests\unit\sdk\dotnet\DearStory.Sdk.Tests\DearStory.Sdk.Tests.csproj',
    '.\tests\unit\sdk\dotnet\DearStory.Sdk.Generator.Tests\DearStory.Sdk.Generator.Tests.csproj'
)

$managedCoveragePackages = @(
    'DearStory.Catalog',
    'DearStory.Protocol',
    'DearStory.Core',
    'DearStory.Docs',
    'DearStory.Runner',
    'DearStory.Sdk',
    'DearStory.Sdk.Generator',
    'DearStory.Transport.Windows'
)

$nativeCoverageSources = @(
    'src\protocol\cpp\src*',
    'src\core\cpp\src*',
    'sdk\cpp\src*'
)

function Test-DearStoryCoveragePackage {
    param(
        [Parameter(Mandatory)][string]$CoveragePath,
        [Parameter(Mandatory)][string[]]$PackageNames
    )

    [xml]$document = Get-Content -LiteralPath $CoveragePath
    $packages = @($document.coverage.packages.package | Where-Object { $null -ne $_ })
    foreach ($packageName in $PackageNames) {
        if (@($packages | Where-Object { [string]$_.name -eq $packageName }).Count -gt 0) {
            return $true
        }
    }

    return $false
}

$ctestArguments = @('--preset', 'windows-msvc-debug', '--output-on-failure')
$dotnetTestArguments = @('--no-build', '-m:1')
$previousTestConfiguration = $env:DEARSTORY_TEST_CONFIGURATION
$previousLocalFeed = $env:DearStoryLocalFeed
$buildPropertiesPath = Join-Path $PWD 'Directory.Build.props'
[xml]$buildProperties = Get-Content -LiteralPath $buildPropertiesPath
$packageVersion = [string]$buildProperties.Project.PropertyGroup.VersionPrefix
if ([string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "VersionPrefix was not found in '$buildPropertiesPath'."
}

if ($Configuration -eq 'Release') {
    $ctestArguments += @('-C', 'Release')
    $dotnetTestArguments += @('-c', 'Release')
}

try {
    $env:DEARSTORY_TEST_CONFIGURATION = $Configuration

    Invoke-DearStoryCommand -Executable 'ctest' -Arguments $ctestArguments
    foreach ($managedTestProject in $managedTestProjects) {
        Invoke-DearStoryCommand -Executable 'dotnet' -Arguments (@('test', $managedTestProject) + $dotnetTestArguments)
    }
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\Doctor.Tests.ps1')
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\BuildScripts.Tests.ps1')
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\CoverageGate.Tests.ps1')
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-Command', 'Invoke-Pester -Script .\tests\unit\foundation\VisualBaselineWorkflow.Tests.ps1')

    $localFeedPath = [System.IO.Path]::GetFullPath((Join-Path $PWD 'artifacts\packages\local-feed'))
    $installPrefix = [System.IO.Path]::GetFullPath((Join-Path $PWD 'artifacts\install\dearstory'))
    $repositoryRoot = [System.IO.Path]::GetFullPath($PWD.Path)

    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\pack.ps1', '-Configuration', $Configuration)
    $env:DearStoryLocalFeed = $localFeedPath
    Invoke-DearStoryCommand -Executable 'dotnet' -Arguments @('test', '.\tests\consumers\dotnet\DearStory.Consumer.Smoke\DearStory.Consumer.Smoke.csproj', '-c', $Configuration, "-p:DearStoryPackageVersion=$packageVersion")
    if ($script:DearStoryCmdlet.ShouldProcess($installPrefix, 'Clear C++ install prefix')) {
        if (Test-Path -LiteralPath $installPrefix) {
            Remove-Item -LiteralPath $installPrefix -Recurse -Force
        }
    }

    Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--install', '.\build\windows-msvc-debug', '--config', $Configuration, '--prefix', '.\artifacts\install\dearstory')
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\assert-public-package-boundaries.ps1', '-CppInstallPrefix', $installPrefix)
    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\PublicPackageBoundaries.Tests.ps1', '-Configuration', $Configuration)
    Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('-E', 'rm', '-rf', '.\build\consumers\cpp')
    Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('-S', '.\tests\consumers\cpp', '-B', '.\build\consumers\cpp', ("-DCMAKE_PREFIX_PATH:PATH={0}" -f $installPrefix), ("-DCMAKE_TOOLCHAIN_FILE:FILEPATH={0}" -f (Join-Path $env:VCPKG_ROOT 'scripts\buildsystems\vcpkg.cmake')), ("-DVCPKG_MANIFEST_DIR:PATH={0}" -f $repositoryRoot), ("-DCMAKE_CONFIGURATION_TYPES:STRING={0}" -f $Configuration))
    Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--build', '.\build\consumers\cpp', '--config', $Configuration)
    Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('-E', 'chdir', '.\build\consumers\cpp', 'ctest', '-C', $Configuration, '--output-on-failure')

    if (-not $Coverage) {
        return
    }

    if ($Configuration -ne 'Release') {
        throw 'Coverage collection requires -Configuration Release.'
    }

    $artifactsCoverageDirectory = '.\artifacts\coverage'
    $managedCoverageDirectory = Join-Path $artifactsCoverageDirectory 'managed'
    $nativeCoveragePath = Join-Path $artifactsCoverageDirectory 'native.xml'
    $nativeBuildDirectory = [System.IO.Path]::GetFullPath((Join-Path $PWD 'build\windows-msvc-debug'))
    $openCppCoveragePath = if (-not [string]::IsNullOrWhiteSpace($env:DEARSTORY_OPENCPPCOVERAGE_PATH)) {
        $env:DEARSTORY_OPENCPPCOVERAGE_PATH
    }
    else {
        $resolvedOpenCppCoverage = Get-Command 'OpenCppCoverage.exe' -ErrorAction SilentlyContinue
        if ($null -ne $resolvedOpenCppCoverage) {
            $resolvedOpenCppCoverage.Source
        }
        elseif (Test-Path -LiteralPath (Join-Path ${env:ProgramFiles} 'OpenCppCoverage\OpenCppCoverage.exe')) {
            Join-Path ${env:ProgramFiles} 'OpenCppCoverage\OpenCppCoverage.exe'
        }
        else {
            Join-Path ${env:ProgramFiles(x86)} 'OpenCppCoverage\OpenCppCoverage.exe'
        }
    }
    $nativeCoverageArguments = @('--quiet', '--cover_children')
    foreach ($nativeCoverageSource in $nativeCoverageSources) {
        $nativeCoverageArguments += @('--sources', [System.IO.Path]::GetFullPath((Join-Path $PWD $nativeCoverageSource)))
    }

    $nativeCoverageArguments += @(
        '--export_type',
        ('cobertura:{0}' -f [System.IO.Path]::GetFullPath((Join-Path $PWD $nativeCoveragePath))),
        '--',
        'ctest',
        '--test-dir',
        $nativeBuildDirectory,
        '-C',
        'Release',
        '--output-on-failure'
    )

    $managedCoverageArguments = @(
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

    Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--build', '--preset', 'windows-msvc-debug', '--config', 'Release')
    Invoke-DearStoryCommand -Executable $openCppCoveragePath -Arguments $nativeCoverageArguments
    foreach ($managedTestProject in $managedTestProjects) {
        Invoke-DearStoryCommand -Executable 'dotnet' -Arguments (@('test', $managedTestProject) + $managedCoverageArguments)
    }

    $coverageGateArguments = @('-NoProfile', '-File', '.\eng\assert-coverage.ps1', $nativeCoveragePath)
    if ($shouldExecute) {
        $managedCoverageFiles = Get-ChildItem -Path $managedCoverageDirectory -Recurse -Filter 'coverage.cobertura.xml' -File |
            Select-Object -ExpandProperty FullName |
            Where-Object { Test-DearStoryCoveragePackage -CoveragePath $_ -PackageNames $managedCoveragePackages }

        if ($managedCoverageFiles.Count -eq 0) {
            throw "Managed Cobertura output for packages '$($managedCoveragePackages -join ', ')' was not found under '$managedCoverageDirectory'."
        }

        $coverageGateArguments += $managedCoverageFiles
    }
    else {
        $coverageGateArguments += '.\artifacts\coverage\managed\coverage.cobertura.xml'
    }

    Invoke-DearStoryCommand -Executable 'pwsh' -Arguments $coverageGateArguments
}
finally {
    if ($null -eq $previousTestConfiguration) {
        Remove-Item Env:DEARSTORY_TEST_CONFIGURATION -ErrorAction SilentlyContinue
    }
    else {
        $env:DEARSTORY_TEST_CONFIGURATION = $previousTestConfiguration
    }

    if ($null -eq $previousLocalFeed) {
        Remove-Item Env:DearStoryLocalFeed -ErrorAction SilentlyContinue
    }
    else {
        $env:DearStoryLocalFeed = $previousLocalFeed
    }
}
