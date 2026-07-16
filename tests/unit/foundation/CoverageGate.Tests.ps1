$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$coverageScript = Join-Path $repositoryRoot 'eng\assert-coverage.ps1'
$validFixture = Join-Path $repositoryRoot 'tests\coverage\valid-cobertura.xml'
$invalidFixture = Join-Path $repositoryRoot 'tests\coverage\invalid-cobertura.xml'
$overlapFixtureA = Join-Path $repositoryRoot 'tests\coverage\overlap-a.xml'
$overlapFixtureB = Join-Path $repositoryRoot 'tests\coverage\overlap-b.xml'

$validOutput = & pwsh -NoProfile -File $coverageScript $validFixture 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Expected valid coverage fixture to exit 0, got $LASTEXITCODE."
}

$validLines = @($validOutput | ForEach-Object { $_.ToString() })
if (-not ($validLines | Select-String -SimpleMatch 'line coverage: 80.00%').Count) {
    throw 'Expected valid coverage output to report line coverage at 80.00%.'
}

if (-not ($validLines | Select-String -SimpleMatch 'branch coverage: 70.00%').Count) {
    throw 'Expected valid coverage output to report branch coverage at 70.00%.'
}

$invalidOutput = & pwsh -NoProfile -File $coverageScript $invalidFixture 2>&1
if ($LASTEXITCODE -ne 1) {
    throw "Expected invalid coverage fixture to exit 1, got $LASTEXITCODE."
}

$invalidLines = @($invalidOutput | ForEach-Object { $_.ToString() })
if (-not ($invalidLines | Select-String -SimpleMatch 'line coverage: 79.00%').Count) {
    throw 'Expected invalid coverage output to report line coverage at 79.00%.'
}

if (-not ($invalidLines | Select-String -SimpleMatch 'branch coverage: 69.00%').Count) {
    throw 'Expected invalid coverage output to report branch coverage at 69.00%.'
}

$mergedOutput = & pwsh -NoProfile -File $coverageScript $overlapFixtureA $overlapFixtureB 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Expected overlapping coverage fixtures to merge successfully, got $LASTEXITCODE."
}

$mergedLines = @($mergedOutput | ForEach-Object { $_.ToString() })
if (-not ($mergedLines | Select-String -SimpleMatch 'line coverage: 100.00%').Count) {
    throw 'Expected overlapping coverage output to report line coverage at 100.00%.'
}

if (-not ($mergedLines | Select-String -SimpleMatch 'branch coverage: 100.00%').Count) {
    throw 'Expected overlapping coverage output to report branch coverage at 100.00%.'
}
