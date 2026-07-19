$ErrorActionPreference = 'Stop'

Describe 'Release workflow' {
    It 'supports tag and manual release triggers' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match "tags:\s*`r?`n\s*-\s*'v\*\.\*\.\*'"
        $workflow | Should Match 'workflow_dispatch:'
        $workflow | Should Match 'ref:'
        $workflow | Should Match 'version:'
    }

    It 'uses the canonical tag name as the release concurrency key for every trigger' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match 'group:\s*dearstory-release-\$\{\{\s*github\.event_name\s*==\s*''workflow_dispatch''\s*&&\s*format\(''v\{0\}'',\s*inputs\.version\)\s*\|\|\s*github\.ref_name\s*\}\}'
    }

    It 'passes manual inputs to PowerShell through the environment' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match 'MANUAL_REF:\s*\$\{\{\s*inputs\.ref\s*\}\}'
        $workflow | Should Match 'MANUAL_VERSION:\s*\$\{\{\s*inputs\.version\s*\}\}'
        $workflow | Should Match '\$env:MANUAL_REF'
        $workflow | Should Match '\$env:MANUAL_VERSION'
        $contextScript = [regex]::Match(
            $workflow,
            '(?ms)^\s{6}- name: Resolve release context.*?^\s{8}run: \|\r?\n(?<script>.*?)(?=^\s{2}\S)'
        ).Groups['script'].Value
        $contextScript | Should Not BeNullOrEmpty
        $contextScript | Should Not Match '\$\{\{\s*inputs\.(ref|version)\s*\}\}'
    }

    It 'passes validated build inputs to PowerShell through the environment' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match 'RELEASE_MODE:\s*\$\{\{\s*needs\.validate\.outputs\.release_mode\s*\}\}'
        $workflow | Should Match 'EXPECTED_VERSION:\s*\$\{\{\s*needs\.validate\.outputs\.version\s*\}\}'
        $workflow | Should Match 'SOURCE_REF:\s*\$\{\{\s*needs\.validate\.outputs\.source_ref\s*\}\}'
        $workflow | Should Match 'SOURCE_COMMIT:\s*\$\{\{\s*needs\.validate\.outputs\.source_commit\s*\}\}'

        $buildScript = [regex]::Match(
            $workflow,
            '(?ms)^\s{6}- name: Build release unit.*?^\s{8}run: \|\r?\n(?<script>.*?)(?=^\s{6}- name: Upload release unit)'
        ).Groups['script'].Value
        $buildScript | Should Not BeNullOrEmpty
        $buildScript | Should Match '-ReleaseMode \$env:RELEASE_MODE'
        $buildScript | Should Match '-ExpectedVersion \$env:EXPECTED_VERSION'
        $buildScript | Should Match '-SourceRef \$env:SOURCE_REF'
        $buildScript | Should Match '-SourceCommit \$env:SOURCE_COMMIT'
        $buildScript | Should Not Match '\$\{\{\s*needs\.validate\.outputs\.'
    }

    It 'keeps an existing GitHub release draft and fails every release command immediately' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match 'environment:\s*release'
        $workflow | Should Match 'contents:\s*write'
        $workflow | Should Match '(?s)\$releaseJson\s*=\s*gh release view \$tag --json isDraft --repo \$env:GH_REPO\r?\n\s*if \(\$LASTEXITCODE -ne 0\)\s*\{\s*throw'
        $workflow | Should Match '(?s)\$release\s*=\s*\$releaseJson \| ConvertFrom-Json.*?if \(-not \$release\.isDraft\)\s*\{\s*throw'
        $workflow | Should Match '(?s)gh release create \$tag [^\r\n]* --draft [^\r\n]*\r?\n\s*if \(\$LASTEXITCODE -ne 0\)\s*\{\s*throw'
        $workflow | Should Match '(?s)gh release upload \$tag [^\r\n]*\r?\n\s*if \(\$LASTEXITCODE -ne 0\)\s*\{\s*throw'
        $workflow | Should Match '(?s)gh release edit \$tag --draft=false [^\r\n]*\r?\n\s*if \(\$LASTEXITCODE -ne 0\)\s*\{\s*throw'
    }

    It 'addresses the repository explicitly for every GitHub CLI operation' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match 'GH_REPO:\s*\$\{\{\s*github\.repository\s*\}\}'

        $publishScript = [regex]::Match(
            $workflow,
            '(?ms)^\s{6}- name: Publish NuGet packages and finalize draft release.*?^\s{8}run: \|\r?\n(?<script>.*)$'
        ).Groups['script'].Value
        $ghCommands = [regex]::Matches($publishScript, '(?m)^\s*gh\s+.+$')
        $ghCommands.Count | Should BeGreaterThan 0
        foreach ($ghCommand in $ghCommands) {
            $ghCommand.Value | Should Match '(--repo\s+\$env:GH_REPO(?:\s|$)|repos/\$env:GH_REPO/)'
        }
    }

    It 'verifies the release tag target unconditionally before package publication' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match 'https://api\.github\.com/repos/\$env:GH_REPO/releases/tags/\$tag'
        $workflow | Should Match 'https://api\.github\.com/repos/\$env:GH_REPO/commits/\$tag'
        $workflow | Should Match 'if \(\$releaseTargetCommit -ne \$sourceCommit\)\s*\{\s*throw'

        $publishScript = [regex]::Match(
            $workflow,
            '(?ms)^\s{6}- name: Publish NuGet packages and finalize draft release.*?^\s{8}run: \|\r?\n(?<script>.*)$'
        ).Groups['script'].Value
        $draftCheck = $publishScript.IndexOf('if (-not $release.isDraft)')
        $targetResolution = $publishScript.IndexOf('$releaseTargetCommit =')
        $targetComparison = $publishScript.IndexOf('if ($releaseTargetCommit -ne $sourceCommit)')
        $packagePublication = $publishScript.IndexOf('$publishedPackages = @(Get-PublishedPackages)')
        $draftCheck | Should BeGreaterThan -1
        $targetResolution | Should BeGreaterThan -1
        $targetComparison | Should BeGreaterThan -1
        $packagePublication | Should BeGreaterThan -1
        $publishScript | Should Not Match '(?s)if \(\$releaseExists\)\s*\{.*?\$releaseTargetCommit'
        $draftCheck | Should BeLessThan $targetResolution
        $targetResolution | Should BeLessThan $targetComparison
        $targetComparison | Should BeLessThan $packagePublication
    }

    It 'resumes partial NuGet publication and verifies the product unit before finalization' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Not Match 'Partial NuGet publication already exists'
        $workflow | Should Match '\$missingPackages\s*=\s*@\(\$packageIds \| Where-Object \{ \$publishedPackages -notcontains \$_ \}\)'
        $workflow | Should Match 'foreach \(\$packageId in \$missingPackages\)'
        $workflow | Should Match '(?s)dotnet nuget push [^\r\n]*\r?\n\s*if \(\$LASTEXITCODE -ne 0\)\s*\{\s*throw'
        $workflow | Should Match '(?s)\$publishedPackages\s*=\s*@\(Get-PublishedPackages.*?if \(\$publishedPackages\.Count -ne \$packageIds\.Count\)\s*\{\s*throw.*?gh release upload'
    }

    It 'contains syntactically valid PowerShell in release state steps' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $stepPatterns = @(
            '(?ms)^\s{6}- name: Resolve release context.*?^\s{8}run: \|\r?\n(?<script>.*?)(?=^\s{2}\S)',
            '(?ms)^\s{6}- name: Publish NuGet packages and finalize draft release.*?^\s{8}run: \|\r?\n(?<script>.*)$'
        )

        foreach ($stepPattern in $stepPatterns) {
            $script = [regex]::Match($workflow, $stepPattern).Groups['script'].Value -replace '(?m)^ {10}', ''
            $tokens = $null
            $parseErrors = $null
            [void][System.Management.Automation.Language.Parser]::ParseInput(
                $script,
                [ref]$tokens,
                [ref]$parseErrors
            )
            $parseErrors.Count | Should Be 0
        }
    }

    It 'makes CI generate the local release unit' {
        $workflow = Get-Content .\.github\workflows\ci.yml -Raw
        $testHarness = Get-Content .\eng\test.ps1 -Raw
        $workflow | Should Match 'eng\\release\.ps1 -ReleaseMode Local'
        $workflow | Should Match 'artifacts/releases'
        $testHarness | Should Match 'Invoke-Pester -Script \.\\tests\\unit\\foundation\\ReleaseWorkflow\.Tests\.ps1 -EnableExit'
    }
}
