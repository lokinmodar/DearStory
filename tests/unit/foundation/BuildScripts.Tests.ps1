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

$combined = @($buildOutput + $testOutput) | ForEach-Object { $_.ToString() }

$expectedCommands = @(
    'cmake --build --preset windows-msvc-debug',
    'ctest --preset windows-msvc-debug --output-on-failure',
    'dotnet build .\DearStory.slnx --no-restore -warnaserror',
    'dotnet test .\DearStory.slnx --no-build -m:1'
)

foreach ($expectedCommand in $expectedCommands) {
    $matchCount = @($combined | Select-String -SimpleMatch $expectedCommand).Count
    if ($matchCount -ne 1) {
        throw "Expected '$expectedCommand' exactly once, found $matchCount."
    }
}
