[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$CppInstallPrefix
)

$ErrorActionPreference = 'Stop'

$installedWindowsTransport = Join-Path $CppInstallPrefix 'include\dearstory\protocol\windows'
if (Test-Path -LiteralPath $installedWindowsTransport) {
    throw "The public C++ package contains the internal Windows transport directory '$installedWindowsTransport'."
}
