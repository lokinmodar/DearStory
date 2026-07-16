$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$buildScript = Join-Path $repositoryRoot 'eng\build.ps1'
$testScript = Join-Path $repositoryRoot 'eng\test.ps1'

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

$buildExpectedCommands = @(
    'cmake --build --preset windows-msvc-debug',
    'dotnet build .\DearStory.slnx --no-restore -warnaserror'
)

foreach ($expectedCommand in $buildExpectedCommands) {
    $matchCount = @($buildLines | Select-String -SimpleMatch $expectedCommand).Count
    if ($matchCount -ne 1) {
        throw "Expected build command '$expectedCommand' exactly once, found $matchCount."
    }
}

$testExpectedCommands = @(
    'ctest --preset windows-msvc-debug --output-on-failure',
    'dotnet test .\DearStory.slnx --no-build -m:1',
    'pwsh -NoProfile -File .\tests\unit\foundation\Doctor.Tests.ps1',
    'pwsh -NoProfile -File .\tests\unit\foundation\BuildScripts.Tests.ps1',
    'pwsh -NoProfile -File .\tests\unit\foundation\CoverageGate.Tests.ps1'
)

foreach ($expectedCommand in $testExpectedCommands) {
    $matchCount = @($testLines | Select-String -SimpleMatch $expectedCommand).Count
    if ($matchCount -ne 1) {
        throw "Expected test command '$expectedCommand' exactly once, found $matchCount."
    }
}

$coverageExpectedCommands = @(
    'ctest --preset windows-msvc-debug --output-on-failure -C Release',
    'dotnet test .\DearStory.slnx --no-build -m:1 -c Release',
    'pwsh -NoProfile -File .\tests\unit\foundation\Doctor.Tests.ps1',
    'pwsh -NoProfile -File .\tests\unit\foundation\BuildScripts.Tests.ps1',
    'pwsh -NoProfile -File .\tests\unit\foundation\CoverageGate.Tests.ps1',
    'cmake --build --preset windows-msvc-debug',
    'OpenCppCoverage.exe --quiet',
    'dotnet test .\DearStory.slnx -c Release --no-build -m:1 --collect:XPlat Code Coverage --results-directory .\artifacts\coverage\managed',
    'pwsh -NoProfile -File .\eng\assert-coverage.ps1'
)

foreach ($expectedCommand in $coverageExpectedCommands) {
    $matchCount = @($coverageLines | Select-String -SimpleMatch $expectedCommand).Count
    if ($matchCount -ne 1) {
        throw "Expected coverage command '$expectedCommand' exactly once, found $matchCount."
    }
}

if (@($coverageOverrideLines | Select-String -SimpleMatch 'C:\tools\OpenCppCoverage\OpenCppCoverage.exe --quiet').Count -ne 1) {
    throw 'Expected coverage override command to use DEARSTORY_OPENCPPCOVERAGE_PATH exactly once.'
}
