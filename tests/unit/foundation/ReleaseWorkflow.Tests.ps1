$ErrorActionPreference = 'Stop'

Describe 'Release workflow' {
    It 'supports tag and manual release triggers' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match "tags:\s*`r?`n\s*-\s*'v\*\.\*\.\*'"
        $workflow | Should Match 'workflow_dispatch:'
        $workflow | Should Match 'ref:'
        $workflow | Should Match 'version:'
    }

    It 'uses draft-first publication and the protected release environment' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match 'environment:\s*release'
        $workflow | Should Match 'contents:\s*write'
        $workflow | Should Match '--draft'
        $workflow | Should Match 'dotnet nuget push'
    }

    It 'makes CI generate the local release unit' {
        $workflow = Get-Content .\.github\workflows\ci.yml -Raw
        $workflow | Should Match 'eng\\release\.ps1 -ReleaseMode Local'
        $workflow | Should Match 'artifacts/releases'
    }
}
