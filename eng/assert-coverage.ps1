[CmdletBinding()]
param(
    [Parameter(Mandatory, ValueFromRemainingArguments)]
    [string[]]$CoberturaPaths
)

$ErrorActionPreference = 'Stop'

if ($CoberturaPaths.Count -eq 0) {
    throw 'Provide at least one Cobertura XML file path.'
}

function Test-PositiveCoveragePercentage {
    param([string]$CoverageText)

    $match = [regex]::Match($CoverageText, '(?<percent>\d+(?:\.\d+)?)%')
    return $match.Success -and ([double]$match.Groups['percent'].Value -gt 0.0)
}

function Get-ConditionCoverageCounts {
    param([string]$CoverageText)

    $match = [regex]::Match($CoverageText, '\((?<covered>\d+)\/(?<valid>\d+)\)')
    if (-not $match.Success) {
        return [pscustomobject]@{
            Covered = 0
            Valid = 0
        }
    }

    return [pscustomobject]@{
        Covered = [int]$match.Groups['covered'].Value
        Valid = [int]$match.Groups['valid'].Value
    }
}

function Test-IncludedCoverageRecord {
    param(
        [string]$PackageName,
        [string]$Filename
    )

    if ([string]::IsNullOrWhiteSpace($Filename)) {
        return $false
    }

    if ($PackageName -eq 'DearStory.ProtocolGenerator') {
        return $false
    }

    return $null -ne (Get-NormalizedCoverageFilename -PackageName $PackageName -Filename $Filename)
}

function Get-NormalizedCoverageFilename {
    param(
        [string]$PackageName,
        [string]$Filename
    )

    if ([string]::IsNullOrWhiteSpace($Filename)) {
        return $null
    }

    $normalized = $Filename.Replace('/', '\').Trim()
    if ($normalized -like '*\src\protocol\cpp\include\*' -or
        $normalized -like '*\src\protocol\cpp\generated\*' -or
        $normalized -like 'Generated\*' -or
        $normalized -like '*\Generated\*' -or
        $normalized -like '*.g.cs') {
        return $null
    }

    $includedRoots = @(
        'src\protocol\cpp\src\',
        'src\core\cpp\src\',
        'sdk\cpp\src\',
        'src\protocol\dotnet\DearStory.Protocol\',
        'src\core\dotnet\DearStory.Core\',
        'sdk\dotnet\DearStory.Sdk\',
        'sdk\dotnet\DearStory.Sdk.Generator\'
    )

    foreach ($root in $includedRoots) {
        $index = $normalized.IndexOf($root, [System.StringComparison]::OrdinalIgnoreCase)
        if ($index -ge 0) {
            return $normalized.Substring($index)
        }
    }

    $legacyRoots = @{
        'protocol\dotnet\DearStory.Protocol\' = 'src\protocol\dotnet\DearStory.Protocol\'
        'core\dotnet\DearStory.Core\'         = 'src\core\dotnet\DearStory.Core\'
    }

    foreach ($legacyRoot in $legacyRoots.Keys) {
        $index = $normalized.IndexOf($legacyRoot, [System.StringComparison]::OrdinalIgnoreCase)
        if ($index -ge 0) {
            return $legacyRoots[$legacyRoot] + $normalized.Substring($index + $legacyRoot.Length)
        }
    }

    if ($normalized -like 'tests\*' -or
        $normalized -like '*\tests\*' -or
        $normalized -like 'tools\*' -or
        $normalized -like '*\tools\*') {
        return $null
    }

    $managedRootByPackage = @{
        'DearStory.Protocol'      = 'src\protocol\dotnet\DearStory.Protocol\'
        'DearStory.Core'          = 'src\core\dotnet\DearStory.Core\'
        'DearStory.Sdk'           = 'sdk\dotnet\DearStory.Sdk\'
        'DearStory.Sdk.Generator' = 'sdk\dotnet\DearStory.Sdk.Generator\'
    }

    if ($managedRootByPackage.ContainsKey($PackageName)) {
        return $managedRootByPackage[$PackageName] + $normalized.TrimStart('\')
    }

    return $null
}

[double]$summaryLinesCovered = 0
[double]$summaryLinesValid = 0
[double]$summaryBranchesCovered = 0
[double]$summaryBranchesValid = 0
$lineCoverageMap = @{}
$branchCoverageMap = @{}

foreach ($path in $CoberturaPaths) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Coverage file not found: $path"
    }

    [xml]$document = Get-Content -LiteralPath $path
    $coverage = $document.coverage
    if ($null -eq $coverage) {
        throw "Coverage file '$path' does not contain a <coverage> root element."
    }

    $detailedLineCount = 0
    $packages = @($coverage.packages.package) | Where-Object { $null -ne $_ }
    foreach ($package in $packages) {
        $packageName = [string]$package.name
        $classes = @($package.classes.class) | Where-Object { $null -ne $_ }
        foreach ($class in $classes) {
            $filename = [string]$class.filename
            if ([string]::IsNullOrWhiteSpace($filename)) {
                $filename = [string]$class.name
            }

            if (-not (Test-IncludedCoverageRecord -PackageName $packageName -Filename $filename)) {
                continue
            }

            $normalizedFilename = Get-NormalizedCoverageFilename -PackageName $packageName -Filename $filename
            if ($null -eq $normalizedFilename) {
                continue
            }

            $classLines = @($class.lines.line) | Where-Object { $null -ne $_ }
            foreach ($line in $classLines) {
                $detailedLineCount++

                $lineKey = "$normalizedFilename|$($line.number)"
                $lineCovered = ([int]$line.hits -gt 0)
                if (-not $lineCoverageMap.ContainsKey($lineKey)) {
                    $lineCoverageMap[$lineKey] = $lineCovered
                }
                elseif ($lineCovered) {
                    $lineCoverageMap[$lineKey] = $true
                }

                if ([string]::Equals([string]$line.branch, 'True', [System.StringComparison]::OrdinalIgnoreCase) -or
                    -not [string]::IsNullOrWhiteSpace([string]$line.'condition-coverage')) {
                    $conditionCoverage = Get-ConditionCoverageCounts -CoverageText ([string]$line.'condition-coverage')
                    if (-not $branchCoverageMap.ContainsKey($lineKey)) {
                        $branchCoverageMap[$lineKey] = [pscustomobject]@{
                            Covered = $conditionCoverage.Covered
                            Valid = $conditionCoverage.Valid
                        }
                    }
                    else {
                        if ($conditionCoverage.Covered -gt $branchCoverageMap[$lineKey].Covered) {
                            $branchCoverageMap[$lineKey].Covered = $conditionCoverage.Covered
                        }

                        if ($conditionCoverage.Valid -gt $branchCoverageMap[$lineKey].Valid) {
                            $branchCoverageMap[$lineKey].Valid = $conditionCoverage.Valid
                        }
                    }
                }
            }
        }
    }

    if ($detailedLineCount -eq 0) {
        $summaryLinesCovered += [double]$coverage.'lines-covered'
        $summaryLinesValid += [double]$coverage.'lines-valid'
        $summaryBranchesCovered += [double]$coverage.'branches-covered'
        $summaryBranchesValid += [double]$coverage.'branches-valid'
    }
}

$totalLinesCovered = $summaryLinesCovered + @($lineCoverageMap.Values | Where-Object { $_ }).Count
$totalLinesValid = $summaryLinesValid + $lineCoverageMap.Count
$totalBranchesCovered = $summaryBranchesCovered + (@($branchCoverageMap.Values | Measure-Object -Property Covered -Sum).Sum ?? 0)
$totalBranchesValid = $summaryBranchesValid + (@($branchCoverageMap.Values | Measure-Object -Property Valid -Sum).Sum ?? 0)

$lineCoverage = if ($totalLinesValid -gt 0) { $totalLinesCovered / $totalLinesValid } else { 0.0 }
$branchCoverage = if ($totalBranchesValid -gt 0) { $totalBranchesCovered / $totalBranchesValid } else { 0.0 }

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$linePercent = (($lineCoverage * 100.0).ToString('F2', $culture)) + '%'
$branchPercent = (($branchCoverage * 100.0).ToString('F2', $culture)) + '%'

$lineMessage = "line coverage: $linePercent ($([int]$totalLinesCovered)/$([int]$totalLinesValid))"
$branchMessage = "branch coverage: $branchPercent ($([int]$totalBranchesCovered)/$([int]$totalBranchesValid))"

if ($lineCoverage -lt 0.80 -or $branchCoverage -lt 0.70) {
    [Console]::Error.WriteLine($lineMessage)
    [Console]::Error.WriteLine($branchMessage)
    exit 1
}

Write-Output $lineMessage
Write-Output $branchMessage
