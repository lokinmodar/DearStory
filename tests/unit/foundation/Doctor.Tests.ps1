$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot\..\..\..\eng\Doctor.psm1" -Force

if (-not (Test-DearStoryVersion -Actual 'cmake version 3.31.6' -Minimum ([version]'3.30'))) {
    throw 'Expected CMake 3.31.6 to satisfy the 3.30 floor.'
}

if (Test-DearStoryVersion -Actual 'cmake version 3.29.9' -Minimum ([version]'3.30')) {
    throw 'Expected CMake 3.29.9 to fail the 3.30 floor.'
}

if (Test-DearStoryVersion -Actual 'not-a-version' -Minimum ([version]'3.30')) {
    throw 'Malformed versions must fail closed.'
}
