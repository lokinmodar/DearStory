[CmdletBinding()]
param(
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot\Doctor.psm1" -Force
$report = @(Get-DearStoryPrerequisiteReport)

if ($Json) {
    $report | ConvertTo-Json -Depth 4
}
else {
    $report | Format-Table Name, Found, MeetsMinimum, Required, Command -AutoSize
}

if ($report.Where({ -not $_.MeetsMinimum }).Count -gt 0) {
    exit 1
}
