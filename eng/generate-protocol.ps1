[CmdletBinding()]
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

$arguments = @(
    'run',
    '--project',
    '.\tools\DearStory.ProtocolGenerator',
    '--',
    '--manifest',
    '.\protocol\control\messages.json',
    '--cpp-output',
    '.\src\protocol\cpp\include\dearstory\protocol\generated\messages.hpp',
    '--csharp-output',
    '.\src\protocol\dotnet\DearStory.Protocol\Generated\Messages.g.cs'
)

if ($Check) {
    $arguments += '--check'
}

$commandText = ('dotnet ' + ($arguments -join ' '))
Write-Output $commandText

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Command failed: $commandText (exit code $LASTEXITCODE)."
}
