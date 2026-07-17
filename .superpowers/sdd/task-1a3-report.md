# Task 1a3 Report

Date: 2026-07-17
Repo: `C:\Dante\DearStory\.worktrees\core-story-model-and-schemas`

## Scope

Implement generated visual metadata parity for canonical corpus stories in the .NET story registry generator, limited to:

- `sdk/dotnet/DearStory.Sdk.Generator/StoryRegistryGenerator.cs`
- `tests/unit/sdk/dotnet/DearStory.Sdk.Generator.Tests/StoryRegistryGeneratorTests.cs`

## TDD cycle

### Red

Added `Generator_emits_visual_metadata_for_canonical_corpus_stories` to assert that generated output contains:

- `Visual = new global::DearStory.Core.StoryVisualDescriptor`
- `SupportsCapture = true`
- `IncludeInCanonicalCorpus = true`

Ran:

```powershell
dotnet test .\tests\unit\sdk\dotnet\DearStory.Sdk.Generator.Tests\DearStory.Sdk.Generator.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~Generator_emits_visual_metadata_for_canonical_corpus_stories
```

Observed expected failure:

- `Assert.Contains() Failure: Sub-string not found`
- Missing substring: `Visual = new global::DearStory.Core.StoryVisualDescriptor`

### Green

Updated `StoryRegistryGenerator` so that:

1. `TransformCandidate(...)` reads the `IncludeInCanonicalCorpus` named argument from `storyAttribute.NamedArguments`.
2. `StoryDefinition` stores the boolean value.
3. Generated descriptor output always emits:

```csharp
Visual = new global::DearStory.Core.StoryVisualDescriptor
{
    SupportsCapture = true,
    IncludeInCanonicalCorpus = true|false,
},
```

The emitted boolean uses lower-case generated literals (`true` / `false`) and is placed inside the existing descriptor `with` block, preserving hierarchy/title/description behavior.

## Verification

Re-ran the exact requested command:

```powershell
dotnet test .\tests\unit\sdk\dotnet\DearStory.Sdk.Generator.Tests\DearStory.Sdk.Generator.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~Generator_emits_visual_metadata_for_canonical_corpus_stories
```

Result:

- Exit code: `0`
- Passed: `1`
- Failed: `0`

## Notes

- No reflection, runner, workspace, examples, or C++ files were changed.
- Existing descriptor metadata generation for hierarchy and optional description remains intact.
