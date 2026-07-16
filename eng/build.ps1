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

$cmakeBuildArguments = @('--build', '--preset', 'windows-msvc-debug')
$dotnetBuildArguments = @('build', '.\DearStory.slnx', '--no-restore', '-warnaserror')

if ($Configuration -eq 'Release') {
    $cmakeBuildArguments += @('--config', 'Release')
    $dotnetBuildArguments += @('-c', 'Release')
}

Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--preset', 'windows-msvc-debug')
Invoke-DearStoryCommand -Executable 'cmake' -Arguments $cmakeBuildArguments
Invoke-DearStoryCommand -Executable 'dotnet' -Arguments @('restore', '.\DearStory.slnx', '--locked-mode')
Invoke-DearStoryCommand -Executable 'dotnet' -Arguments $dotnetBuildArguments
