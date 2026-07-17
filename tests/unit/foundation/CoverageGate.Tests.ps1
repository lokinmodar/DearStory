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

$generatedFixtureDirectory = Join-Path $repositoryRoot 'artifacts\tests\coverage-generated'
$generatedFixturePath = Join-Path $generatedFixtureDirectory 'generated-filter.xml'

try {
    New-Item -ItemType Directory -Force -Path $generatedFixtureDirectory | Out-Null
    @'
<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="0.0" branch-rate="0.0" lines-covered="0" lines-valid="0" branches-covered="0" branches-valid="0">
  <packages>
    <package name="DearStory.Sdk" line-rate="0.0" branch-rate="0.0" complexity="1">
      <classes>
        <class name="DearStory.Sdk.GeneratedRegistry" filename="Generated/Registry.g.cs" line-rate="0.0" branch-rate="0.0" complexity="1">
          <methods />
          <lines>
            <line number="10" hits="0" />
          </lines>
        </class>
        <class name="DearStory.Sdk.ReflectionStoryRegistry" filename="sdk/dotnet/DearStory.Sdk/ReflectionStoryRegistry.cs" line-rate="1.0" branch-rate="1.0" complexity="1">
          <methods />
          <lines>
            <line number="20" hits="1" branch="True" condition-coverage="100% (1/1)">
              <conditions>
                <condition number="1" type="jump" coverage="100%" />
              </conditions>
            </line>
          </lines>
        </class>
      </classes>
    </package>
    <package name="DearStory.ProtocolGenerator" line-rate="0.0" branch-rate="0.0" complexity="1">
      <classes>
        <class name="DearStory.ProtocolGenerator.Generated" filename="tools/DearStory.ProtocolGenerator/Generated.cs" line-rate="0.0" branch-rate="0.0" complexity="1">
          <methods />
          <lines>
            <line number="30" hits="0" />
          </lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
'@ | Set-Content -LiteralPath $generatedFixturePath -Encoding utf8

    $generatedOutput = & pwsh -NoProfile -File $coverageScript $generatedFixturePath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected generated coverage fixture to ignore generated/runtime-excluded files, got $LASTEXITCODE."
    }

    $generatedLines = @($generatedOutput | ForEach-Object { $_.ToString() })
    if (-not ($generatedLines | Select-String -SimpleMatch 'line coverage: 100.00%').Count) {
        throw 'Expected generated coverage output to ignore generated files and report line coverage at 100.00%.'
    }

    if (-not ($generatedLines | Select-String -SimpleMatch 'branch coverage: 100.00%').Count) {
        throw 'Expected generated coverage output to ignore generated files and report branch coverage at 100.00%.'
    }
}
finally {
    if (Test-Path -LiteralPath $generatedFixtureDirectory) {
        Remove-Item -LiteralPath $generatedFixtureDirectory -Recurse -Force
    }
}

$normalizedFixtureDirectory = Join-Path $repositoryRoot 'artifacts\tests\coverage-normalized'
$normalizedFixtureA = Join-Path $normalizedFixtureDirectory 'normalized-a.xml'
$normalizedFixtureB = Join-Path $normalizedFixtureDirectory 'normalized-b.xml'

try {
    New-Item -ItemType Directory -Force -Path $normalizedFixtureDirectory | Out-Null
    @'
<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="0.5" branch-rate="0.5" lines-covered="1" lines-valid="2" branches-covered="1" branches-valid="2">
  <packages>
    <package name="DearStory.Core" line-rate="0.5" branch-rate="0.5" complexity="1">
      <classes>
        <class name="DearStory.Core.StoryId" filename="src/core/dotnet/DearStory.Core/StoryId.cs" line-rate="0.5" branch-rate="0.5" complexity="1">
          <methods />
          <lines>
            <line number="10" hits="1" branch="True" condition-coverage="100% (1/1)">
              <conditions>
                <condition number="1" type="jump" coverage="100%" />
              </conditions>
            </line>
            <line number="11" hits="0" branch="True" condition-coverage="0% (0/1)">
              <conditions>
                <condition number="2" type="jump" coverage="0%" />
              </conditions>
            </line>
          </lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
'@ | Set-Content -LiteralPath $normalizedFixtureA -Encoding utf8

    @'
<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="0.5" branch-rate="0.5" lines-covered="1" lines-valid="2" branches-covered="1" branches-valid="2">
  <packages>
    <package name="DearStory.Core" line-rate="0.5" branch-rate="0.5" complexity="1">
      <classes>
        <class name="DearStory.Core.StoryId" filename="core/dotnet/DearStory.Core/StoryId.cs" line-rate="0.5" branch-rate="0.5" complexity="1">
          <methods />
          <lines>
            <line number="10" hits="0" branch="True" condition-coverage="0% (0/1)">
              <conditions>
                <condition number="1" type="jump" coverage="0%" />
              </conditions>
            </line>
            <line number="11" hits="1" branch="True" condition-coverage="100% (1/1)">
              <conditions>
                <condition number="2" type="jump" coverage="100%" />
              </conditions>
            </line>
          </lines>
        </class>
      </classes>
    </package>
</packages>
</coverage>
'@ | Set-Content -LiteralPath $normalizedFixtureB -Encoding utf8

    $normalizedOutput = & pwsh -NoProfile -File $coverageScript $normalizedFixtureA $normalizedFixtureB 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected normalized coverage fixtures to merge successfully, got $LASTEXITCODE."
    }

    $normalizedLines = @($normalizedOutput | ForEach-Object { $_.ToString() })
    if (-not ($normalizedLines | Select-String -SimpleMatch 'line coverage: 100.00%').Count) {
        throw 'Expected normalized coverage output to merge equivalent source paths and report line coverage at 100.00%.'
    }

    if (-not ($normalizedLines | Select-String -SimpleMatch 'branch coverage: 100.00%').Count) {
        throw 'Expected normalized coverage output to merge equivalent source paths and report branch coverage at 100.00%.'
    }
}
finally {
    if (Test-Path -LiteralPath $normalizedFixtureDirectory) {
        Remove-Item -LiteralPath $normalizedFixtureDirectory -Recurse -Force
    }
}
