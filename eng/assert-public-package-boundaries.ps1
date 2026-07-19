[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$CppInstallPrefix
)

$ErrorActionPreference = 'Stop'

$forbiddenIncludeDirectories = @(
    'include\dearstory\protocol\windows',
    'include\dearstory\transports'
)

foreach ($relativePath in $forbiddenIncludeDirectories) {
    $forbiddenDirectory = Join-Path $CppInstallPrefix $relativePath
    if (Test-Path -LiteralPath $forbiddenDirectory) {
        throw "The public C++ package contains the internal transport directory '$forbiddenDirectory'."
    }
}

$exportDirectory = Join-Path $CppInstallPrefix 'lib\cmake\DearStory'
if (-not (Test-Path -LiteralPath $exportDirectory)) {
    throw "The public C++ package is missing exported target metadata at '$exportDirectory'."
}

$exportMetadata = Get-ChildItem -LiteralPath $exportDirectory -Filter '*.cmake' -File -Recurse |
    Get-Content -Raw
if ($exportMetadata -match '(?i)(DearStory::TransportsCpp|dearstory_transports_cpp)') {
    throw 'The public C++ package exported target metadata exposes an internal transport target or dependency.'
}
