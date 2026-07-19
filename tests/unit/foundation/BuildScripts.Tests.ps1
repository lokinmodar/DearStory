$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$buildScript = Join-Path $repositoryRoot 'eng\build.ps1'
$testScript = Join-Path $repositoryRoot 'eng\test.ps1'
$testScriptContent = Get-Content -Raw $testScript
$consumerProject = Join-Path $repositoryRoot 'tests\consumers\dotnet\DearStory.Consumer.Smoke\DearStory.Consumer.Smoke.csproj'
$consumerProjectContent = Get-Content -Raw $consumerProject
$consumerNuGetConfig = Join-Path $repositoryRoot 'tests\consumers\dotnet\DearStory.Consumer.Smoke\NuGet.config'

if ($testScriptContent -notmatch 'tests\\unit\\core\\dotnet\\DearStory\.Core\.Tests') {
    throw 'eng/test.ps1 must run the managed core tests.'
}

if ($testScriptContent -notmatch 'tests\\unit\\sdk\\dotnet\\DearStory\.Sdk\.Tests') {
    throw 'eng/test.ps1 must run the managed SDK tests.'
}

$installClearIndex = $testScriptContent.IndexOf('Remove-Item -LiteralPath $installPrefix -Recurse -Force', [StringComparison]::Ordinal)
$installCommandIndex = $testScriptContent.IndexOf("Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--install'", [StringComparison]::Ordinal)
if ($installClearIndex -lt 0 -or $installClearIndex -gt $installCommandIndex) {
    throw 'eng/test.ps1 must clear the C++ install prefix before cmake --install.'
}

if ($consumerProjectContent -match 'PackageReference Include="DearStory\.(Protocol|Core)"') {
    throw 'The SDK package smoke consumer must rely on transitive Protocol and Core dependencies.'
}

if ($consumerProjectContent -notmatch 'PackageReference Include="DearStory\.Sdk" Version="\$\(DearStoryPackageVersion\)"') {
    throw 'The package smoke consumer must receive an exact DearStory package version from the test flow.'
}

if (-not (Test-Path -LiteralPath $consumerNuGetConfig)) {
    throw 'The package smoke consumer must define package-source mapping in NuGet.config.'
}

$consumerNuGetConfigContent = Get-Content -Raw $consumerNuGetConfig
if ($consumerNuGetConfigContent -notmatch '<package pattern="DearStory\.\*"\s*/>' -or
    $consumerNuGetConfigContent -notmatch '<packageSource key="DearStoryLocalFeed">') {
    throw 'The package smoke consumer must map DearStory.* packages exclusively to the local feed.'
}

$buildOutput = & pwsh -NoProfile -File $buildScript -Configuration Debug -WhatIf 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Expected build script smoke test to exit 0, got $LASTEXITCODE."
}

$testOutput = & pwsh -NoProfile -File $testScript -Configuration Debug -WhatIf 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Expected test script smoke test to exit 0, got $LASTEXITCODE."
}

$coverageOutput = & pwsh -NoProfile -File $testScript -Configuration Release -Coverage -WhatIf 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Expected coverage test script smoke test to exit 0, got $LASTEXITCODE."
}

$previousCoveragePath = $env:DEARSTORY_OPENCPPCOVERAGE_PATH
$env:DEARSTORY_OPENCPPCOVERAGE_PATH = 'C:\tools\OpenCppCoverage\OpenCppCoverage.exe'
$coverageOverrideOutput = & pwsh -NoProfile -File $testScript -Configuration Release -Coverage -WhatIf 2>&1
$overrideExitCode = $LASTEXITCODE
if ($null -eq $previousCoveragePath) {
    Remove-Item Env:DEARSTORY_OPENCPPCOVERAGE_PATH -ErrorAction SilentlyContinue
}
else {
    $env:DEARSTORY_OPENCPPCOVERAGE_PATH = $previousCoveragePath
}

if ($overrideExitCode -ne 0) {
    throw "Expected coverage override smoke test to exit 0, got $overrideExitCode."
}

$buildLines = @($buildOutput | ForEach-Object { $_.ToString() })
$testLines = @($testOutput | ForEach-Object { $_.ToString() })
$coverageLines = @($coverageOutput | ForEach-Object { $_.ToString() })
$coverageOverrideLines = @($coverageOverrideOutput | ForEach-Object { $_.ToString() })
$managedTestProjects = @(
    '.\tests\unit\protocol\dotnet\DearStory.Protocol.Tests\DearStory.Protocol.Tests.csproj',
    '.\tests\unit\protocol\dotnet\DearStory.ProtocolGenerator.Tests\DearStory.ProtocolGenerator.Tests.csproj',
    '.\tests\integration\protocol\DearStory.Protocol.IntegrationTests\DearStory.Protocol.IntegrationTests.csproj',
    '.\tests\e2e\protocol\DearStory.Protocol.E2ETests\DearStory.Protocol.E2ETests.csproj',
    '.\tests\contract\protocol\DearStory.Protocol.ContractTests\DearStory.Protocol.ContractTests.csproj',
    '.\tests\unit\core\dotnet\DearStory.Core.Tests\DearStory.Core.Tests.csproj',
    '.\tests\contract\core\DearStory.Core.ContractTests\DearStory.Core.ContractTests.csproj',
    '.\tests\unit\sdk\dotnet\DearStory.Sdk.Tests\DearStory.Sdk.Tests.csproj',
    '.\tests\unit\sdk\dotnet\DearStory.Sdk.Generator.Tests\DearStory.Sdk.Generator.Tests.csproj'
)

$buildExpectedCommands = @(
    'cmake --build --preset windows-msvc-debug',
    'dotnet build .\DearStory.slnx --no-restore -warnaserror',
    'dotnet build .\src\core\dotnet\DearStory.Core\DearStory.Core.csproj --no-restore -warnaserror',
    'dotnet build .\sdk\dotnet\DearStory.Sdk\DearStory.Sdk.csproj --no-restore -warnaserror',
    'dotnet build .\sdk\dotnet\DearStory.Sdk.Generator\DearStory.Sdk.Generator.csproj --no-restore -warnaserror'
)

foreach ($expectedCommand in $buildExpectedCommands) {
    $matchCount = @($buildLines | Select-String -SimpleMatch $expectedCommand).Count
    if ($matchCount -ne 1) {
        throw "Expected build command '$expectedCommand' exactly once, found $matchCount."
    }
}

$testExpectedCommands = @(
    'ctest --preset windows-msvc-debug --output-on-failure',
    'pwsh -NoProfile -File .\tests\unit\foundation\Doctor.Tests.ps1',
    'pwsh -NoProfile -File .\tests\unit\foundation\BuildScripts.Tests.ps1',
    'pwsh -NoProfile -File .\tests\unit\foundation\CoverageGate.Tests.ps1',
    'dotnet test .\tests\consumers\dotnet\DearStory.Consumer.Smoke\DearStory.Consumer.Smoke.csproj -c Debug -p:DearStoryPackageVersion=0.1.0',
    'pwsh -NoProfile -File .\eng\assert-public-package-boundaries.ps1 -CppInstallPrefix'
)

foreach ($managedTestProject in $managedTestProjects) {
    $testExpectedCommands += "dotnet test $managedTestProject --no-build -m:1"
}

foreach ($expectedCommand in $testExpectedCommands) {
    $matchCount = @($testLines | Select-String -SimpleMatch $expectedCommand).Count
    if ($matchCount -ne 1) {
        throw "Expected test command '$expectedCommand' exactly once, found $matchCount."
    }
}

$coverageExpectedCommands = @(
    'ctest --preset windows-msvc-debug --output-on-failure -C Release',
    'pwsh -NoProfile -File .\tests\unit\foundation\Doctor.Tests.ps1',
    'pwsh -NoProfile -File .\tests\unit\foundation\BuildScripts.Tests.ps1',
    'pwsh -NoProfile -File .\tests\unit\foundation\CoverageGate.Tests.ps1',
    'cmake --build --preset windows-msvc-debug --config Release',
    'OpenCppCoverage.exe --quiet --cover_children',
    'ctest --test-dir',
    'pwsh -NoProfile -File .\eng\assert-coverage.ps1'
)

foreach ($managedTestProject in $managedTestProjects) {
    $coverageExpectedCommands += "dotnet test $managedTestProject --no-build -m:1 -c Release"
    $coverageExpectedCommands += "dotnet test $managedTestProject -c Release --no-build -m:1 --collect:XPlat Code Coverage --results-directory .\artifacts\coverage\managed"
}

foreach ($expectedCommand in $coverageExpectedCommands) {
    $matchCount = @($coverageLines | Select-String -SimpleMatch $expectedCommand).Count
    if ($matchCount -ne 1) {
        throw "Expected coverage command '$expectedCommand' exactly once, found $matchCount."
    }
}

if (@($coverageOverrideLines | Select-String -SimpleMatch 'C:\tools\OpenCppCoverage\OpenCppCoverage.exe --quiet --cover_children').Count -ne 1) {
    throw 'Expected coverage override command to use DEARSTORY_OPENCPPCOVERAGE_PATH exactly once.'
}
