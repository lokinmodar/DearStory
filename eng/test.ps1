[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
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

$ctestArguments = @('--preset', 'windows-msvc-debug', '--output-on-failure')
$dotnetTestArguments = @('test', '.\DearStory.slnx', '--no-build', '-m:1')

if ($Configuration -eq 'Release') {
    $ctestArguments += @('-C', 'Release')
    $dotnetTestArguments += @('-c', 'Release')
}

Invoke-DearStoryCommand -Executable 'ctest' -Arguments $ctestArguments
Invoke-DearStoryCommand -Executable 'dotnet' -Arguments $dotnetTestArguments
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\Doctor.Tests.ps1')
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\tests\unit\foundation\BuildScripts.Tests.ps1')
