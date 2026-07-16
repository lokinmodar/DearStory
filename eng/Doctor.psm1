Set-StrictMode -Version Latest

class PrerequisiteResult {
    [string]$Name
    [string]$Command
    [bool]$Found
    [bool]$MeetsMinimum
    [string]$Output
    [string]$Required

    PrerequisiteResult(
        [string]$name,
        [string]$command,
        [bool]$found,
        [bool]$meetsMinimum,
        [string]$output,
        [string]$required
    ) {
        $this.Name = $name
        $this.Command = $command
        $this.Found = $found
        $this.MeetsMinimum = $meetsMinimum
        $this.Output = $output
        $this.Required = $required
    }
}

function Test-DearStoryVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Actual,
        [Parameter(Mandatory)][version]$Minimum
    )

    $match = [regex]::Match($Actual, '(?<version>\d+\.\d+(?:\.\d+)?)')
    if (-not $match.Success) {
        return $false
    }

    return ([version]$match.Groups['version'].Value) -ge $Minimum
}

function Get-DearStoryPrerequisiteReport {
    [CmdletBinding()]
    param()

    $checks = @(
        @{ Name = 'PowerShell'; Command = 'pwsh'; Arguments = @('--version'); Minimum = [version]'7.4' },
        @{ Name = '.NET SDK'; Command = 'dotnet'; Arguments = @('--version'); Minimum = [version]'10.0' },
        @{ Name = 'CMake'; Command = 'cmake'; Arguments = @('--version'); Minimum = [version]'3.30' },
        @{ Name = 'Git'; Command = 'git'; Arguments = @('--version'); Minimum = [version]'2.45' }
    )

    foreach ($check in $checks) {
        $resolved = Get-Command $check.Command -ErrorAction SilentlyContinue
        if ($null -eq $resolved) {
            [PrerequisiteResult]::new(
                $check.Name,
                $check.Command,
                $false,
                $false,
                '',
                $check.Minimum.ToString())
            continue
        }

        $output = (& $resolved.Source @($check.Arguments) 2>&1 | Out-String).Trim()
        [PrerequisiteResult]::new(
            $check.Name,
            $resolved.Source,
            $true,
            (Test-DearStoryVersion -Actual $output -Minimum $check.Minimum),
            $output,
            $check.Minimum.ToString())
    }
}

Export-ModuleMember -Function Test-DearStoryVersion, Get-DearStoryPrerequisiteReport
