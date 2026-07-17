$ErrorActionPreference = 'Stop'

Describe 'Visual baseline workflow' {
    It 'locks CI to WARP for canonical validation' {
        $workflow = Get-Content .\.github\workflows\ci.yml -Raw
        $workflow | Should Match 'DEARSTORY_VISUAL_BACKEND:\s*warp'
        $workflow | Should Match 'tests/visual/windows/baselines'
    }

    It 'documents the reviewed canonical baseline folder' {
        $readme = Get-Content .\tests\visual\windows\README.md -Raw
        $readme | Should Match 'baselines/buttons'
        $readme | Should Match 'canonical'
    }
}
