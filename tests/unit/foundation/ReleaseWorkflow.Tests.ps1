$ErrorActionPreference = 'Stop'

Describe 'Release workflow' {
    It 'supports tag and manual release triggers' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $workflow | Should Match "tags:\s*`r?`n\s*-\s*'v\*\.\*\.\*'"
        $workflow | Should Match 'workflow_dispatch:'
        $workflow | Should Match 'ref:'
        $workflow | Should Match 'version:'
    }

    It 'pins every release action to its reviewed commit SHA' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $actionReferences = @([regex]::Matches($workflow, '(?m)^\s*uses:\s*(?<reference>\S+)\s*$') |
            ForEach-Object { $_.Groups['reference'].Value })
        $expectedActionReferences = @(
            'actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0',
            'actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0',
            'actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1',
            'actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a',
            'actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093'
        )

        $actionReferences.Count | Should Be $expectedActionReferences.Count
        ($actionReferences -join "`n") | Should Be ($expectedActionReferences -join "`n")
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

    It 'validates a non-tip manual release ancestor against the complete main history' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $contextScript = [regex]::Match(
            $workflow,
            '(?ms)^\s{6}- name: Resolve release context.*?^\s{8}run: \|\r?\n(?<script>.*?)(?=^\s{2}\S)'
        ).Groups['script'].Value

        $contextScript | Should Match 'git fetch origin main'
        $contextScript | Should Not Match 'git fetch origin main\s+--depth'
        $contextScript | Should Match 'git merge-base --is-ancestor \$sourceCommit origin/main'
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

    It 'fails fast on partial existing NuGet publication while retaining the draft release' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $publishScript = [regex]::Match(
            $workflow,
            '(?ms)^\s{6}- name: Publish NuGet packages and finalize draft release.*?^\s{8}run: \|\r?\n(?<script>.*)$'
        ).Groups['script'].Value

        $publishScript | Should Match '(?s)if \(\$publishedPackages\.Count -gt 0 -and\s*\$publishedPackages\.Count -lt \$packageIds\.Count\)\s*\{\s*throw "Partial NuGet publication already exists'
        $publishScript | Should Not Match '\$missingPackages\s*='
        $publishScript | Should Match 'if \(\$publishedPackages\.Count -eq 0\)'
        $workflow | Should Match '(?s)dotnet nuget push [^\r\n]*\r?\n\s*if \(\$LASTEXITCODE -ne 0\)\s*\{\s*throw'

        $draftCheck = $publishScript.IndexOf('if (-not $release.isDraft)')
        $publishedLookup = $publishScript.IndexOf('$publishedPackages = @(Get-PublishedPackages)')
        $partialPublicationGate = $publishScript.IndexOf('if ($publishedPackages.Count -gt 0 -and')
        $initialContentVerification = $publishScript.IndexOf('foreach ($packageId in $publishedPackages)')
        $packagePublication = $publishScript.IndexOf('dotnet nuget push')

        $draftCheck | Should BeGreaterThan -1
        $publishedLookup | Should BeGreaterThan $draftCheck
        $partialPublicationGate | Should BeGreaterThan $publishedLookup
        $initialContentVerification | Should BeGreaterThan $partialPublicationGate
        $packagePublication | Should BeGreaterThan $partialPublicationGate
    }

    It 'verifies published NuGet package contents against the coordinated release unit' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $publishScript = [regex]::Match(
            $workflow,
            '(?ms)^\s{6}- name: Publish NuGet packages and finalize draft release.*?^\s{8}run: \|\r?\n(?<script>.*)$'
        ).Groups['script'].Value

        $publishScript | Should Match '\$packageUri\s*=\s*''\{0\}/\{1\}/\{2\}/\{1\}\.\{2\}\.nupkg''\s*-f\s*\$packageBaseUrl,\s*\$normalizedPackageId,\s*\$normalizedVersion'
        $publishScript | Should Match 'Invoke-WebRequest -Uri \$packageUri -OutFile \$publishedPackagePath'
        $publishScript | Should Match 'function Get-NormalizedPackageContentHash'
        $publishScript | Should Match '\[System\.IO\.Compression\.ZipFile\]::OpenRead\(\$PackagePath\)'
        $publishScript | Should Match 'Where-Object \{ \$_.FullName -ine ''\.signature\.p7s'' \}'
        $publishScript | Should Match '\$entry\.Open\(\)'
        $publishScript | Should Match 'ComputeHash\(\$entryStream\)'
        $publishScript | Should Match '\$releasePackageContentHash\s*=\s*Get-NormalizedPackageContentHash -PackagePath \$packagePath'
        $publishScript | Should Match '\$publishedPackageContentHash\s*=\s*Get-NormalizedPackageContentHash -PackagePath \$publishedPackagePath'
        $publishScript | Should Match 'if \(\$publishedPackageContentHash -ne \$releasePackageContentHash\)\s*\{\s*throw'
        $publishScript | Should Not Match 'Get-FileHash -LiteralPath \$packagePath -Algorithm SHA256'
        $publishScript | Should Not Match 'Get-FileHash -LiteralPath \$publishedPackagePath -Algorithm SHA256'
        $publishScript | Should Match '(?s)foreach \(\$packageId in \$publishedPackages\)\s*\{\s*Assert-PublishedPackageMatchesReleaseUnit -PackageId \$packageId\s*\}'
        $publishScript | Should Match '(?s)foreach \(\$packageId in \$packageIds\)\s*\{\s*Assert-PublishedPackageMatchesReleaseUnit -PackageId \$packageId\s*\}'

        $initialPublishedLookup = $publishScript.IndexOf('$publishedPackages = @(Get-PublishedPackages)')
        $partialPublicationGate = $publishScript.IndexOf('if ($publishedPackages.Count -gt 0 -and')
        $initialContentVerification = $publishScript.IndexOf('foreach ($packageId in $publishedPackages)')
        $emptySetPublicationGate = $publishScript.IndexOf('if ($publishedPackages.Count -eq 0)')
        $coordinatedPublicationGate = $publishScript.IndexOf('if ($publishedPackages.Count -ne $packageIds.Count)')
        $finalContentVerification = $publishScript.IndexOf('foreach ($packageId in $packageIds)', $coordinatedPublicationGate)
        $assetReconciliation = $publishScript.IndexOf('foreach ($requiredReleaseAsset in $requiredReleaseAssets)')

        $initialPublishedLookup | Should BeGreaterThan -1
        $partialPublicationGate | Should BeGreaterThan $initialPublishedLookup
        $initialContentVerification | Should BeGreaterThan $partialPublicationGate
        $emptySetPublicationGate | Should BeGreaterThan $initialContentVerification
        $coordinatedPublicationGate | Should BeGreaterThan $emptySetPublicationGate
        $finalContentVerification | Should BeGreaterThan $coordinatedPublicationGate
        $finalContentVerification | Should BeGreaterThan $assetReconciliation
    }

    It 'reconciles required draft assets before NuGet publication without overwriting conflicts' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $publishScript = [regex]::Match(
            $workflow,
            '(?ms)^\s{6}- name: Publish NuGet packages and finalize draft release.*?^\s{8}run: \|\r?\n(?<script>.*)$'
        ).Groups['script'].Value

        $publishScript | Should Match 'DearStory-cpp-\$version-windows-msvc-x64\.zip'
        $publishScript | Should Match "'SHA256SUMS'"
        $publishScript | Should Match "'release-manifest\.json'"
        $publishScript | Should Match 'function Sync-ReleaseAsset'
        $publishScript | Should Match 'gh release view \$tag --json assets --repo \$env:GH_REPO'
        $publishScript | Should Match 'gh release download \$tag --pattern \$assetName --dir \$existingAssetRoot --repo \$env:GH_REPO'
        $publishScript | Should Match 'Get-FileHash -LiteralPath \$AssetPath -Algorithm SHA256'
        $publishScript | Should Match 'Get-FileHash -LiteralPath \$downloadedAssetPath -Algorithm SHA256'
        $publishScript | Should Match 'already exists with different content'
        $publishScript | Should Not Match 'gh release upload .*--clobber'

        $assetReconciliation = $publishScript.IndexOf('foreach ($requiredReleaseAsset in $requiredReleaseAssets)')
        $packagePublication = $publishScript.IndexOf('dotnet nuget push')
        $finalPackageVerification = $publishScript.IndexOf('foreach ($packageId in $packageIds)', $packagePublication)
        $releaseFinalization = $publishScript.IndexOf('gh release edit $tag --draft=false')
        $assetReconciliation | Should BeGreaterThan -1
        $packagePublication | Should BeGreaterThan $assetReconciliation
        $finalPackageVerification | Should BeGreaterThan $packagePublication
        $releaseFinalization | Should BeGreaterThan $finalPackageVerification
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

    It 'documents partial NuGet publication as fail-fast and non-resumable' {
        $releaseGuide = Get-Content .\docs\guides\releasing-packages.md -Raw
        $releaseGuide | Should Match 'does not resume a partial NuGet publication'
        $releaseGuide | Should Match 'before\s+pushing any additional package'
    }
}
