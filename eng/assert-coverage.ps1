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

    if ($Filename -like '*\src\protocol\cpp\include\*' -or
        $Filename -like '*\src\protocol\cpp\generated\*' -or
        $Filename -like 'Generated/*' -or
        $Filename -like '*.g.cs') {
        return $false
    }

    return $true
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

            $classLines = @($class.lines.line) | Where-Object { $null -ne $_ }
            foreach ($line in $classLines) {
                $detailedLineCount++

                $lineKey = "$packageName|$filename|$($line.number)"
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
