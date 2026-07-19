[CmdletBinding()]
param(
    [string]$Path = (Join-Path $PSScriptRoot 'version.json')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Version file '$Path' was not found."
}

$document = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
$version = [string]$document.version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Version file '$Path' does not contain a non-empty 'version' value."
}

if ($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw "Version '$version' is not a stable SemVer value."
}

[pscustomobject]@{
    Version = $version
    Tag = "v$version"
    IsStableSemVer = $true
}
