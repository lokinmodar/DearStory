$ErrorActionPreference = 'Stop'

function Get-ReleasePublishScript {
    $workflow = Get-Content .\.github\workflows\release.yml -Raw
    $publishScript = [regex]::Match(
        $workflow,
        '(?ms)^\s{6}- name: Publish NuGet packages and finalize draft release.*?^\s{8}run: \|\r?\n(?<script>.*)$'
    ).Groups['script'].Value
    if ([string]::IsNullOrWhiteSpace($publishScript)) {
        throw 'Unable to extract the publish step from the release workflow.'
    }

    return $publishScript
}

function Get-ReleaseAssetReconciliationScript {
    $assetScript = [regex]::Match(
        (Get-ReleasePublishScript),
        '(?ms)(?<script>^\s{10}\$existingAssetRoot = Join-Path .*?^\s{10}\})\r?\n\r?\n\s{10}\$publishedPackages = @\(Get-PublishedPackages\)'
    ).Groups['script'].Value -replace '(?m)^ {10}', ''
    if ([string]::IsNullOrWhiteSpace($assetScript)) {
        throw 'Unable to extract the asset reconciliation block from the release workflow.'
    }

    return $assetScript
}

function Invoke-ReleaseAssetReconciliation {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Absent', 'Identical', 'Conflicting', 'Unexpected')]
        [string] $Scenario
    )

    $assetScript = Get-ReleaseAssetReconciliationScript
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dearstory-release-assets-{0}" -f [guid]::NewGuid().ToString('N'))
    $releaseRoot = Join-Path $fixtureRoot 'release'
    $remoteAssetRoot = Join-Path $fixtureRoot 'remote-assets'
    $version = '0.1.0'
    $tag = "v$version"
    $manifestPath = Join-Path $releaseRoot 'release-manifest.json'
    $commandLog = [System.Collections.Generic.List[string]]::new()
    $originalRunnerTemp = [Environment]::GetEnvironmentVariable('RUNNER_TEMP', 'Process')
    $originalGhRepo = [Environment]::GetEnvironmentVariable('GH_REPO', 'Process')
    try {
        New-Item -ItemType Directory -Force -Path (Join-Path $releaseRoot 'cpp'), $remoteAssetRoot | Out-Null

        $localAssets = [ordered]@{
            "DearStory-cpp-$version-windows-msvc-x64.zip" = Join-Path $releaseRoot "cpp\DearStory-cpp-$version-windows-msvc-x64.zip"
            'SHA256SUMS' = Join-Path $releaseRoot 'SHA256SUMS'
            'release-manifest.json' = $manifestPath
        }

        Set-Content -LiteralPath $localAssets["DearStory-cpp-$version-windows-msvc-x64.zip"] -Value 'local-cpp-archive' -NoNewline
        Set-Content -LiteralPath $localAssets['SHA256SUMS'] -Value 'local-sha256sums' -NoNewline
        Set-Content -LiteralPath $localAssets['release-manifest.json'] -Value '{"version":"0.1.0"}' -NoNewline

        $existingReleaseAssetNames = @()
        switch ($Scenario) {
            'Identical' {
                foreach ($assetName in $localAssets.Keys) {
                    Copy-Item -LiteralPath $localAssets[$assetName] -Destination (Join-Path $remoteAssetRoot $assetName)
                }
                $existingReleaseAssetNames = @($localAssets.Keys)
            }
            'Conflicting' {
                $conflictingAssetName = "DearStory-cpp-$version-windows-msvc-x64.zip"
                Set-Content -LiteralPath (Join-Path $remoteAssetRoot $conflictingAssetName) -Value 'conflicting-cpp-archive' -NoNewline
                $existingReleaseAssetNames = @($conflictingAssetName)
            }
            'Unexpected' {
                $existingReleaseAssetNames = @('unapproved-release-asset.txt')
            }
        }

        function gh {
            param(
                [Parameter(ValueFromRemainingArguments = $true)]
                [string[]] $Arguments
            )

            $commandLog.Add(($Arguments -join ' '))
            $command = ($Arguments[0..1] -join ' ')
            switch ($command) {
                'release view' {
                    $global:LASTEXITCODE = 0
                    return ([pscustomobject]@{
                            assets = @($existingReleaseAssetNames | ForEach-Object {
                                    [pscustomobject]@{ name = $_ }
                                })
                        } | ConvertTo-Json -Compress)
                }
                'release download' {
                    $assetName = $Arguments[[array]::IndexOf($Arguments, '--pattern') + 1]
                    $destinationRoot = $Arguments[[array]::IndexOf($Arguments, '--dir') + 1]
                    Copy-Item -LiteralPath (Join-Path $remoteAssetRoot $assetName) -Destination (Join-Path $destinationRoot $assetName)
                    $global:LASTEXITCODE = 0
                    return
                }
                'release upload' {
                    $assetPath = $Arguments[3]
                    Copy-Item -LiteralPath $assetPath -Destination (Join-Path $remoteAssetRoot ([System.IO.Path]::GetFileName($assetPath)))
                    $global:LASTEXITCODE = 0
                    return
                }
                default {
                    $global:LASTEXITCODE = 1
                    throw "Unhandled fake gh command: $($Arguments -join ' ')"
                }
            }
        }

        [Environment]::SetEnvironmentVariable('RUNNER_TEMP', (Join-Path $fixtureRoot 'runner-temp'), 'Process')
        [Environment]::SetEnvironmentVariable('GH_REPO', 'dearstory/tests', 'Process')
        New-Item -ItemType Directory -Force -Path $env:RUNNER_TEMP | Out-Null
        $global:LASTEXITCODE = 0
        Invoke-Expression $assetScript

        return [pscustomobject]@{
            CommandLog = @($commandLog)
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable('RUNNER_TEMP', $originalRunnerTemp, 'Process')
        [Environment]::SetEnvironmentVariable('GH_REPO', $originalGhRepo, 'Process')
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

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
        $validationScript = [regex]::Match(
            $contextScript,
            '(?ms)(?<validation>^\s{12}git fetch origin main\r?\n\s{12}if \(\$LASTEXITCODE -ne 0\) \{\r?\n\s{14}throw "Manual release ancestry refresh from origin/main failed\."\r?\n\s{12}\}\r?\n\r?\n\s{12}\$sourceCommitOutput = git rev-parse --verify --end-of-options "\$env:MANUAL_REF\^\{commit\}"\r?\n\s{12}if \(\$LASTEXITCODE -ne 0\) \{\r?\n\s{14}throw "Manual release ref.*?^\s{12}\}\r?\n\r?\n\s{12}\$sourceCommit = \$sourceCommitOutput\.Trim\(\)\r?\n\r?\n\s{12}git merge-base --is-ancestor \$sourceCommit origin/main\r?\n\s{12}if \(\$LASTEXITCODE -ne 0\) \{\r?\n\s{14}throw "Manual release ref.*?^\s{12}\})'
        ).Groups['validation'].Value -replace '(?m)^ {12}', ''
        $validationScript | Should Not BeNullOrEmpty

        $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dearstory-manual-release-ancestor-{0}" -f [guid]::NewGuid().ToString('N'))
        $sourceRepository = Join-Path $fixtureRoot 'source'
        $shallowValidationRepository = Join-Path $fixtureRoot 'shallow-validation'
        $validationRepository = Join-Path $fixtureRoot 'validation'
        $shallowValidationRunner = Join-Path $fixtureRoot 'validate-manual-release-shallow.ps1'
        $validationRunner = Join-Path $fixtureRoot 'validate-manual-release.ps1'
        try {
            New-Item -ItemType Directory -Force -Path $sourceRepository | Out-Null
            & git -C $sourceRepository init --quiet --initial-branch main
            if ($LASTEXITCODE -ne 0) {
                throw 'Unable to initialize the manual release ancestry fixture repository.'
            }

            & git -C $sourceRepository config user.name 'DearStory Release Test'
            & git -C $sourceRepository config user.email 'release-test@dearstory.invalid'
            Set-Content -LiteralPath (Join-Path $sourceRepository 'ancestor.txt') -Value 'ancestor' -NoNewline
            & git -C $sourceRepository add ancestor.txt
            & git -C $sourceRepository commit --quiet -m 'test: create release ancestor'
            $nonTipAncestor = (& git -C $sourceRepository rev-parse HEAD).Trim()

            Set-Content -LiteralPath (Join-Path $sourceRepository 'tip.txt') -Value 'tip' -NoNewline
            & git -C $sourceRepository add tip.txt
            & git -C $sourceRepository commit --quiet -m 'test: advance main beyond release ancestor'
            $mainTip = (& git -C $sourceRepository rev-parse HEAD).Trim()
            if ($nonTipAncestor -eq $mainTip) {
                throw 'Expected the manual release fixture ancestor not to be the main tip.'
            }

            & git clone --quiet $sourceRepository $shallowValidationRepository
            if ($LASTEXITCODE -ne 0) {
                throw 'Unable to clone the manual release ancestry fixture repository.'
            }

            $shallowValidationScript = $validationScript -replace '(?m)^git fetch origin main$', 'git fetch origin main --depth=1'
            @(
                'param([Parameter(Mandatory)][string] $ManualRef)',
                '$ErrorActionPreference = ''Stop''',
                '$env:MANUAL_REF = $ManualRef',
                $shallowValidationScript
            ) | Set-Content -LiteralPath $shallowValidationRunner

            $shallowValidationOutput = & pwsh -NoProfile -WorkingDirectory $shallowValidationRepository -File $shallowValidationRunner -ManualRef $nonTipAncestor 2>&1
            if ($LASTEXITCODE -eq 0) {
                throw "Expected depth-limited manual release validation to reject non-tip ancestor '$nonTipAncestor'."
            }

            & git clone --quiet $sourceRepository $validationRepository
            if ($LASTEXITCODE -ne 0) {
                throw 'Unable to clone the manual release ancestry fixture repository.'
            }

            @(
                'param([Parameter(Mandatory)][string] $ManualRef)',
                '$ErrorActionPreference = ''Stop''',
                '$env:MANUAL_REF = $ManualRef',
                $validationScript
            ) | Set-Content -LiteralPath $validationRunner

            $validationOutput = & pwsh -NoProfile -WorkingDirectory $validationRepository -File $validationRunner -ManualRef $nonTipAncestor 2>&1
            if ($LASTEXITCODE -ne 0) {
                throw "Expected manual release validation to accept non-tip ancestor '$nonTipAncestor' reachable from main. Output: $($validationOutput -join [Environment]::NewLine)"
            }
        }
        finally {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'fails immediately when the manual ancestry refresh cannot fetch origin main' {
        $workflow = Get-Content .\.github\workflows\release.yml -Raw
        $contextScript = [regex]::Match(
            $workflow,
            '(?ms)^\s{6}- name: Resolve release context.*?^\s{8}run: \|\r?\n(?<script>.*?)(?=^\s{2}\S)'
        ).Groups['script'].Value

        $contextScript | Should Match '(?s)git fetch origin main\r?\n\s*if \(\$LASTEXITCODE -ne 0\) \{\r?\n\s*throw "Manual release ancestry refresh from origin/main failed\."\r?\n\s*\}\r?\n\r?\n\s*\$sourceCommitOutput = git rev-parse'
        $sourceResolution = $contextScript.IndexOf('$sourceCommitOutput = git rev-parse')
        $invalidRefFailure = $contextScript.IndexOf("throw `"Manual release ref '")
        $sourceCommitTrim = $contextScript.IndexOf('$sourceCommit = $sourceCommitOutput.Trim()')
        $sourceResolution | Should BeGreaterThan -1
        $invalidRefFailure | Should BeGreaterThan $sourceResolution
        $sourceCommitTrim | Should BeGreaterThan $invalidRefFailure
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
        $publishScript | Should Match 'has unexpected draft asset\(s\)'
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

    It 'uploads required release assets when the draft does not already contain them' {
        $result = Invoke-ReleaseAssetReconciliation -Scenario Absent

        $result.CommandLog.Count | Should Be 4
        @($result.CommandLog | Where-Object { $_ -match '^release upload ' }).Count | Should Be 3
        ($result.CommandLog -join "`n") | Should Match 'release upload v0\.1\.0 .*DearStory-cpp-0\.1\.0-windows-msvc-x64\.zip --repo dearstory/tests'
        ($result.CommandLog -join "`n") | Should Match 'release upload v0\.1\.0 .*SHA256SUMS --repo dearstory/tests'
        ($result.CommandLog -join "`n") | Should Match 'release upload v0\.1\.0 .*release-manifest\.json --repo dearstory/tests'
    }

    It 'reuses identical existing draft assets without uploading replacements' {
        $result = Invoke-ReleaseAssetReconciliation -Scenario Identical

        $result.CommandLog.Count | Should Be 4
        @($result.CommandLog | Where-Object { $_ -match '^release download ' }).Count | Should Be 3
        @($result.CommandLog | Where-Object { $_ -match '^release upload ' }).Count | Should Be 0
    }

    It 'fails when an existing draft asset has conflicting content' {
        { Invoke-ReleaseAssetReconciliation -Scenario Conflicting } |
            Should Throw "GitHub release asset 'DearStory-cpp-0.1.0-windows-msvc-x64.zip' already exists with different content."
    }

    It 'rejects unexpected draft assets before NuGet publication or release finalization' {
        { Invoke-ReleaseAssetReconciliation -Scenario Unexpected } |
            Should Throw "GitHub release 'v0.1.0' has unexpected draft asset(s): unapproved-release-asset.txt."
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
        $releaseGuide | Should Match 'does not resume a partial NuGet\s+publication'
        $releaseGuide | Should Match 'before\s+pushing any additional package'
    }

    It 'documents draft asset reconciliation before NuGet publication and uses a neutral local SourceRef example' {
        $releaseGuide = Get-Content .\docs\guides\releasing-packages.md -Raw
        $draftIndex = $releaseGuide.IndexOf('create or reuse the draft GitHub Release')
        $assetIndex = $releaseGuide.IndexOf('reconcile or upload the required GitHub release assets')
        $nugetIndex = $releaseGuide.IndexOf('publish the four NuGet packages')
        $verificationIndex = $releaseGuide.IndexOf('verify the full public package set')
        $publishIndex = $releaseGuide.IndexOf('publish the GitHub Release')

        $draftIndex | Should BeGreaterThan -1
        $assetIndex | Should BeGreaterThan $draftIndex
        $nugetIndex | Should BeGreaterThan $assetIndex
        $verificationIndex | Should BeGreaterThan $nugetIndex
        $publishIndex | Should BeGreaterThan $verificationIndex
        $releaseGuide | Should Not Match 'refs/heads/feature/phase-3-release-automation'
        $releaseGuide | Should Match '-SourceRef refs/heads/main'
    }
}
