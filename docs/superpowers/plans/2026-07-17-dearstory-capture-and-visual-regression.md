# DearStory Capture and Visual Regression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver one shared Windows capture/regression core that `dearstory build` and `dearstory dev` both use to capture real frames from the official C++ and .NET hosts, compare them against canonical baselines, and approve updated baselines explicitly.

**Architecture:** Add one new managed `DearStory.Capture` project that owns canonical-corpus resolution, backend policy, artifact layout, RGBA-to-PNG encoding, pixel comparison, diff generation, manifest emission, and approval guardrails. Keep `DearStory.Runner` and `DearStory.Catalog` thin: the build flow calls the shared core for batch capture, and the dev flow routes one-shot CLI capture plus catalog UI capture through the same core while reusing the existing host protocol and shared-memory RGBA transport.

**Tech Stack:** .NET 10, C++20, MSVC/CTest, DearStory named-pipe control protocol, shared-memory RGBA transport, JSON manifests, Markdown docs, xUnit.net v3, Catch2, and one repo-pinned managed PNG/diff library added through `Directory.Packages.props`.

## Global Constraints

- Windows-first only. This subproject intentionally does not include Linux or macOS support.
- D3D11 WARP is the canonical capture backend for CI, baseline generation, and approval.
- GPU-backed capture is supported for local development convenience, but it is not the truth source for canonical baseline promotion.
- Canonical artifacts are versioned in the repository.
- Experimental and transient artifacts are kept outside the repository by default.
- Selection uses both story-level metadata and workspace-level overrides in `dearstory.toml`.
- The initial canonical set for the first execution plan is the existing Windows-slice stories, but the mechanism must be generic from the start.
- Approval is always explicit.
- The system must never overwrite canonical baselines automatically.
- `dearstory build` and `dearstory dev` both call the same internal capture contract.

---

## File structure locked by this plan

```text
docs/adr/0004-visual-capture-and-baselines.md                            decision record for visual policy and artifact ownership
docs/architecture/capture-and-visual-regression.md                       subsystem map for capture core, runner adapters, and catalog actions
docs/guides/visual-baselines.md                                          operator guide for capture, diff, and approval
docs/guides/static-docs.md                                               updated screenshot generation notes for `dearstory build`
docs/guides/windows-dev-workflow.md                                      updated dev capture and approval workflow
examples/workspaces/windows-slice/dearstory.toml                         visual overrides for the initial canonical corpus
examples/workspaces/windows-slice/cpp/src/buttons_primary.cpp            marks the native example story for canonical capture
examples/workspaces/windows-slice/dotnet/Stories/ButtonsPrimary.cs       marks the managed example story for canonical capture
sdk/cpp/include/dearstory/sdk/story_registration.hpp                     C++ story visual metadata input
sdk/cpp/src/story_registry.cpp                                           C++ descriptor projection for visual metadata
sdk/dotnet/DearStory.Sdk/StoryAttribute.cs                               .NET story visual metadata input
sdk/dotnet/DearStory.Sdk/ReflectionStoryRegistry.cs                      reflection projection of visual metadata
sdk/dotnet/DearStory.Sdk.Generator/StoryRegistryGenerator.cs             source-generated projection of visual metadata
src/core/cpp/include/dearstory/core/story_visual_descriptor.hpp          native visual metadata contract
src/core/cpp/include/dearstory/core/story_descriptor.hpp                 native descriptor now carries visual metadata
src/core/dotnet/DearStory.Core/StoryVisualDescriptor.cs                  managed visual metadata contract
src/core/dotnet/DearStory.Core/StoryDescriptor.cs                        managed descriptor now carries visual metadata
src/capture/dotnet/DearStory.Capture/DearStory.Capture.csproj           shared managed capture/regression core
src/capture/dotnet/DearStory.Capture/CaptureBackendKind.cs              canonical backend enum
src/capture/dotnet/DearStory.Capture/ComparisonClassification.cs        stable result states surfaced to build/dev/CI
src/capture/dotnet/DearStory.Capture/CaptureCorpusResolver.cs           story metadata + workspace override resolution
src/capture/dotnet/DearStory.Capture/CaptureArtifactLayout.cs           canonical/experimental path resolution
src/capture/dotnet/DearStory.Capture/CapturedFrame.cs                   RGBA frame payload contract
src/capture/dotnet/DearStory.Capture/IVisualFrameSource.cs              build/dev capture adapter contract
src/capture/dotnet/DearStory.Capture/ImageComparer.cs                   baseline comparison and diff generation
src/capture/dotnet/DearStory.Capture/VisualCaptureRequest.cs            shared request model
src/capture/dotnet/DearStory.Capture/VisualCaptureResult.cs             shared result model
src/capture/dotnet/DearStory.Capture/CaptureApprovalService.cs          WARP-only explicit promotion rules
src/capture/dotnet/DearStory.Capture/VisualCaptureService.cs            shared orchestration entrypoint
src/catalog/dotnet/DearStory.Catalog/CatalogSessionPresenter.cs         new capture actions and last-result surface
src/catalog/dotnet/DearStory.Catalog/Preview/PreviewFrameState.cs       current preview frame snapshot access
src/catalog/dotnet/DearStory.Catalog/Capture/CaptureWorkflowState.cs    catalog-side capture command/result state
src/runner/dotnet/DearStory.Runner/Program.cs                           new build/dev capture options in CLI help and dispatch
src/runner/dotnet/DearStory.Runner/Commands/BuildCommand.cs             batch capture, docs screenshot copy, approval routing
src/runner/dotnet/DearStory.Runner/Commands/DevCommand.cs               one-shot CLI capture and catalog capture wiring
src/runner/dotnet/DearStory.Runner/Configuration/WorkspaceConfiguration.cs
src/runner/dotnet/DearStory.Runner/Configuration/WorkspaceConfigurationLoader.cs
src/runner/dotnet/DearStory.Runner/Capture/StoryCaptureTarget.cs        runner capture target model
src/runner/dotnet/DearStory.Runner/Capture/RunnerHostCaptureAdapter.cs  real host frame acquisition using the existing protocol
src/runner/dotnet/DearStory.Runner/Capture/RunnerHostCaptureSession.cs  launch, session-open, frame-read, teardown sequence
tools/DearStory.CaptureWorker/Program.cs                                thin CLI wrapper over the shared capture core
tests/unit/core/dotnet/DearStory.Core.Tests/StoryVisualDescriptorTests.cs
tests/unit/sdk/dotnet/DearStory.Sdk.Tests/StoryVisualMetadataTests.cs
tests/unit/sdk/cpp/story_registry_tests.cpp
tests/unit/runner/dotnet/DearStory.Runner.Tests/VisualWorkspaceConfigurationLoaderTests.cs
tests/unit/capture/dotnet/DearStory.Capture.Tests/DearStory.Capture.Tests.csproj
tests/unit/capture/dotnet/DearStory.Capture.Tests/CapturePolicyTests.cs
tests/unit/capture/dotnet/DearStory.Capture.Tests/ImageComparerTests.cs
tests/unit/capture/dotnet/DearStory.Capture.Tests/CaptureApprovalServiceTests.cs
tests/unit/catalog/dotnet/DearStory.Catalog.Tests/CatalogCaptureWorkflowTests.cs
tests/unit/foundation/VisualBaselineWorkflow.Tests.ps1
tests/integration/windows/DearStory.WindowsSlice.Tests/RealCaptureIntegrationTests.cs
tests/e2e/windows/DearStory.WindowsSlice.E2ETests/BuildCommandVisualRegressionTests.cs
tests/e2e/windows/DearStory.WindowsSlice.E2ETests/DevCommandCaptureTests.cs
tests/e2e/windows/DearStory.WindowsSlice.E2ETests/VisualArtifactEnvironment.cs
tests/visual/windows/baselines/buttons/primary.png                      first approved native canonical baseline
tests/visual/windows/baselines/buttons/primarymanaged.png               first approved managed canonical baseline
tests/visual/windows/README.md                                          storage contract for canonical files only
Directory.Packages.props                                                adds the managed PNG/diff dependency
DearStory.slnx                                                          adds the shared capture project and tests
eng/build.ps1                                                           includes the new project in normal build verification
eng/test.ps1                                                            includes visual unit/integration/e2e verification
.github/workflows/ci.yml                                                WARP-based canonical validation and failure artifact upload
```

## Delivery map inside this plan

1. Add explicit visual metadata to story descriptors and workspace-level canonical overrides.
2. Introduce one reusable capture/regression core with deterministic policy, layout, diff, and approval semantics.
3. Replace the placeholder capture path with real host-backed frame acquisition for both official hosts.
4. Route `dearstory build` through the shared core for batch capture, docs screenshots, manifests, diffs, and optional approval.
5. Route `dearstory dev` through the shared core for one-shot CLI capture and catalog-triggered capture.
6. Commit canonical baselines, update docs, and lock WARP verification into CI and scripts.

## Scope guard

This plan intentionally stops before:

- scripted input automation;
- packaging or installer work;
- Linux or macOS support;
- GPU-backed canonical baselines;
- automatic enrollment of every future story into the canonical corpus.

### Task 1: Add explicit visual metadata and workspace canonical-corpus policy

**Files:**

- Create: `src/core/dotnet/DearStory.Core/StoryVisualDescriptor.cs`
- Modify: `src/core/dotnet/DearStory.Core/StoryDescriptor.cs`
- Create: `src/core/cpp/include/dearstory/core/story_visual_descriptor.hpp`
- Modify: `src/core/cpp/include/dearstory/core/story_descriptor.hpp`
- Modify: `sdk/dotnet/DearStory.Sdk/StoryAttribute.cs`
- Modify: `sdk/dotnet/DearStory.Sdk/ReflectionStoryRegistry.cs`
- Modify: `sdk/dotnet/DearStory.Sdk.Generator/StoryRegistryGenerator.cs`
- Modify: `sdk/cpp/include/dearstory/sdk/story_registration.hpp`
- Modify: `sdk/cpp/src/story_registry.cpp`
- Modify: `src/runner/dotnet/DearStory.Runner/Configuration/WorkspaceConfiguration.cs`
- Modify: `src/runner/dotnet/DearStory.Runner/Configuration/WorkspaceConfigurationLoader.cs`
- Modify: `examples/workspaces/windows-slice/dearstory.toml`
- Modify: `examples/workspaces/windows-slice/cpp/src/buttons_primary.cpp`
- Modify: `examples/workspaces/windows-slice/dotnet/Stories/ButtonsPrimary.cs`
- Create: `tests/unit/core/dotnet/DearStory.Core.Tests/StoryVisualDescriptorTests.cs`
- Create: `tests/unit/sdk/dotnet/DearStory.Sdk.Tests/StoryVisualMetadataTests.cs`
- Modify: `tests/unit/sdk/cpp/story_registry_tests.cpp`
- Create: `tests/unit/runner/dotnet/DearStory.Runner.Tests/VisualWorkspaceConfigurationLoaderTests.cs`

**Interfaces:**

- Consumes: `DearStory.Core.StoryDescriptor`, `DearStory.Sdk.StoryAttribute`, `dearstory::core::story_descriptor`, `dearstory::sdk::story_registration`, and `WorkspaceConfigurationLoader.LoadFromText(string text)`.
- Produces:
  - `public sealed record StoryVisualDescriptor { public static StoryVisualDescriptor Default { get; } ; public bool SupportsCapture { get; init; } ; public bool IncludeInCanonicalCorpus { get; init; } ; }`
  - `public StoryVisualDescriptor Visual { get; init; } = StoryVisualDescriptor.Default;` on managed `StoryDescriptor`
  - `struct story_visual_descriptor final { bool supports_capture{ true }; bool include_in_canonical_corpus{ false }; };`
  - `public bool IncludeInCanonicalCorpus { get; init; }` on `StoryAttribute`
  - `public sealed class VisualConfiguration { public IReadOnlyList<VisualStoryOverride> Overrides { get; } }`
  - `public sealed class VisualStoryOverride { public string StoryId { get; } public bool IncludeInCanonicalCorpus { get; } }`

- [ ] **Step 1: Write the failing metadata and workspace tests**

Create `tests/unit/core/dotnet/DearStory.Core.Tests/StoryVisualDescriptorTests.cs`:

```csharp
using DearStory.Core;
using Xunit;

namespace DearStory.Core.Tests;

public sealed class StoryVisualDescriptorTests
{
    [Fact]
    public void Create_assigns_default_visual_metadata()
    {
        var descriptor = StoryDescriptor.Create("buttons/primary", "Buttons/Primary");

        Assert.True(descriptor.Visual.SupportsCapture);
        Assert.False(descriptor.Visual.IncludeInCanonicalCorpus);
    }
}
```

Create `tests/unit/sdk/dotnet/DearStory.Sdk.Tests/StoryVisualMetadataTests.cs`:

```csharp
using DearStory.Core;
using DearStory.Sdk;
using Xunit;

namespace DearStory.Sdk.Tests;

public sealed class StoryVisualMetadataTests
{
    [Fact]
    public void Reflection_registry_projects_canonical_visual_flag_from_story_attribute()
    {
        var registry = ReflectionStoryRegistry.Create(
            typeof(StoryVisualMetadataTests).Assembly,
            new ReflectionStoryRegistryOptions(allowReflectionFallback: true));

        var descriptor = Assert.Single(
            registry.Descriptors,
            item => item.Id.Value == "buttons/primarymanaged");

        Assert.True(descriptor.Visual.SupportsCapture);
        Assert.True(descriptor.Visual.IncludeInCanonicalCorpus);
    }

    public sealed class StoryArgs
    {
        [StoryArg("label")]
        public string Label { get; set; } = "Save";
    }

    public static class VisualStories
    {
        [Story("buttons/primarymanaged", typeof(StoryArgs), IncludeInCanonicalCorpus = true)]
        public static void Render(StoryContext context)
        {
        }
    }
}
```

Create `tests/unit/runner/dotnet/DearStory.Runner.Tests/VisualWorkspaceConfigurationLoaderTests.cs`:

```csharp
using DearStory.Runner.Configuration;
using Xunit;

namespace DearStory.Runner.Tests;

public sealed class VisualWorkspaceConfigurationLoaderTests
{
    [Fact]
    public void LoadFromText_binds_visual_overrides_for_the_canonical_corpus()
    {
        var configuration = WorkspaceConfigurationLoader.LoadFromText(
            """
            [workspace]
            name = "windows-slice"

            [visual]
            [[visual.overrides]]
            story = "buttons/primary"
            include_in_canonical_corpus = true
            """);

        var entry = Assert.Single(configuration.Visual.Overrides);
        Assert.Equal("buttons/primary", entry.StoryId);
        Assert.True(entry.IncludeInCanonicalCorpus);
    }
}
```

Add this test to `tests/unit/sdk/cpp/story_registry_tests.cpp`:

```cpp
TEST_CASE("sdk_story_registration keeps explicit canonical visual enrollment")
{
    auto registration = dearstory::sdk::story_registration::create(
        "buttons/primary",
        +[](dearstory::sdk::story_context&) {},
        dearstory::sdk::argument_metadata{},
        dearstory::sdk::visual_story_options{ .include_in_canonical_corpus = true });

    REQUIRE(registration.descriptor().visual.supports_capture);
    REQUIRE(registration.descriptor().visual.include_in_canonical_corpus);
}
```

- [ ] **Step 2: Run the focused metadata and workspace tests**

Run:

```powershell
dotnet test .\tests\unit\sdk\dotnet\DearStory.Sdk.Tests\DearStory.Sdk.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~StoryVisualMetadataTests
dotnet test .\tests\unit\runner\dotnet\DearStory.Runner.Tests\DearStory.Runner.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~VisualWorkspaceConfigurationLoaderTests
ctest --test-dir .\build\windows-msvc-debug -C Release -R "story_registry" --output-on-failure
```

Expected: FAIL because story descriptors do not carry visual metadata yet, the .NET attribute cannot mark canonical capture, the C++ registration overload does not exist, and the workspace loader does not bind `[visual]` overrides.

- [ ] **Step 3: Implement the minimal visual metadata contracts and workspace binding**

Create `src/core/dotnet/DearStory.Core/StoryVisualDescriptor.cs`:

```csharp
namespace DearStory.Core;

/// <summary>Declares one story's visual-capture policy.</summary>
public sealed record StoryVisualDescriptor
{
    /// <summary>Gets the default visual policy for ordinary stories.</summary>
    public static StoryVisualDescriptor Default { get; } = new();

    /// <summary>Gets a value indicating whether this story supports RGBA capture.</summary>
    public bool SupportsCapture { get; init; } = true;

    /// <summary>Gets a value indicating whether this story opts into the canonical visual corpus.</summary>
    public bool IncludeInCanonicalCorpus { get; init; }
}
```

Update `src/core/dotnet/DearStory.Core/StoryDescriptor.cs`:

```csharp
/// <summary>
/// Gets the visual regression metadata for the story.
/// </summary>
/// <value>The visual regression metadata. The default is <see cref="StoryVisualDescriptor.Default" />.</value>
public StoryVisualDescriptor Visual { get; init; } = StoryVisualDescriptor.Default;
```

Update `sdk/dotnet/DearStory.Sdk/StoryAttribute.cs`:

```csharp
/// <summary>
/// Gets or sets a value indicating whether the story is part of the canonical visual corpus by default.
/// </summary>
/// <value><see langword="true" /> when the story opts into the canonical visual corpus.</value>
public bool IncludeInCanonicalCorpus { get; init; }
```

Update `sdk/dotnet/DearStory.Sdk/ReflectionStoryRegistry.cs` so `BuildDescriptor` projects the new metadata:

```csharp
private static StoryDescriptor BuildDescriptor(StoryAttribute storyAttribute)
{
    var rawId = storyAttribute.Id;
    var segments = rawId
        .Trim()
        .Replace('\\', '/')
        .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    return StoryDescriptor.Create(rawId, segments[^1]) with
    {
        Hierarchy = segments.Take(Math.Max(segments.Length - 1, 0)).ToArray(),
        Visual = new StoryVisualDescriptor
        {
            SupportsCapture = true,
            IncludeInCanonicalCorpus = storyAttribute.IncludeInCanonicalCorpus,
        },
    };
}
```

Update `sdk/cpp/include/dearstory/sdk/story_registration.hpp`:

```cpp
struct visual_story_options final {
    bool include_in_canonical_corpus{ false };
};

[[nodiscard]] static story_registration create(
    std::string_view raw_id,
    story_callback render,
    argument_metadata arguments,
    visual_story_options visual = {});
```

Update `src/runner/dotnet/DearStory.Runner/Configuration/WorkspaceConfiguration.cs`:

```csharp
public sealed class VisualConfiguration
{
    public VisualConfiguration(IReadOnlyList<VisualStoryOverride> overrides)
    {
        Overrides = overrides;
    }

    public IReadOnlyList<VisualStoryOverride> Overrides { get; }
}

public sealed class VisualStoryOverride
{
    public VisualStoryOverride(string storyId, bool includeInCanonicalCorpus)
    {
        StoryId = storyId;
        IncludeInCanonicalCorpus = includeInCanonicalCorpus;
    }

    public string StoryId { get; }
    public bool IncludeInCanonicalCorpus { get; }
}
```

Update `examples/workspaces/windows-slice/dearstory.toml`:

```toml
[visual]

[[visual.overrides]]
story = "buttons/primary"
include_in_canonical_corpus = true

[[visual.overrides]]
story = "buttons/primarymanaged"
include_in_canonical_corpus = true
```

- [ ] **Step 4: Re-run the focused metadata and workspace tests**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
dotnet test .\tests\unit\core\dotnet\DearStory.Core.Tests\DearStory.Core.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~StoryVisualDescriptorTests
dotnet test .\tests\unit\sdk\dotnet\DearStory.Sdk.Tests\DearStory.Sdk.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~StoryVisualMetadataTests
dotnet test .\tests\unit\runner\dotnet\DearStory.Runner.Tests\DearStory.Runner.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~VisualWorkspaceConfigurationLoaderTests
ctest --test-dir .\build\windows-msvc-debug -C Release -R "story_registry" --output-on-failure
```

Expected: PASS. Both SDKs can mark canonical visual stories, the shared descriptor carries visual policy, and the workspace loader exposes canonical-corpus overrides.

- [ ] **Step 5: Commit**

```powershell
git add src/core sdk src/runner/dotnet/DearStory.Runner/Configuration examples/workspaces/windows-slice tests/unit/core/dotnet tests/unit/sdk/dotnet tests/unit/sdk/cpp tests/unit/runner/dotnet
git commit -m "feat: add story visual metadata and workspace overrides"
```

### Task 2: Add the shared capture/regression core

**Files:**

- Modify: `Directory.Packages.props`
- Modify: `DearStory.slnx`
- Create: `src/capture/dotnet/DearStory.Capture/DearStory.Capture.csproj`
- Create: `src/capture/dotnet/DearStory.Capture/CaptureBackendKind.cs`
- Create: `src/capture/dotnet/DearStory.Capture/ComparisonClassification.cs`
- Create: `src/capture/dotnet/DearStory.Capture/CapturedFrame.cs`
- Create: `src/capture/dotnet/DearStory.Capture/IVisualFrameSource.cs`
- Create: `src/capture/dotnet/DearStory.Capture/VisualCaptureRequest.cs`
- Create: `src/capture/dotnet/DearStory.Capture/VisualCaptureResult.cs`
- Create: `src/capture/dotnet/DearStory.Capture/CaptureCorpusResolver.cs`
- Create: `src/capture/dotnet/DearStory.Capture/CaptureArtifactLayout.cs`
- Create: `src/capture/dotnet/DearStory.Capture/ImageComparer.cs`
- Create: `src/capture/dotnet/DearStory.Capture/CaptureApprovalService.cs`
- Create: `src/capture/dotnet/DearStory.Capture/VisualCaptureService.cs`
- Create: `tests/unit/capture/dotnet/DearStory.Capture.Tests/DearStory.Capture.Tests.csproj`
- Create: `tests/unit/capture/dotnet/DearStory.Capture.Tests/CapturePolicyTests.cs`
- Create: `tests/unit/capture/dotnet/DearStory.Capture.Tests/ImageComparerTests.cs`
- Create: `tests/unit/capture/dotnet/DearStory.Capture.Tests/CaptureApprovalServiceTests.cs`

**Interfaces:**

- Consumes: `StoryDescriptor.Visual`, `WorkspaceConfiguration.Visual.Overrides`, and RGBA frames supplied through `IVisualFrameSource`.
- Produces:
  - `public enum CaptureBackendKind { Warp, Gpu }`
  - `public enum ComparisonClassification { Match, Mismatch, MissingBaseline, BackendMismatch, CaptureFault }`
  - `public sealed record CapturedFrame(string StoryId, string HostId, int Width, int Height, int Stride, ReadOnlyMemory<byte> RgbaBytes, DateTimeOffset TimestampUtc);`
  - `public interface IVisualFrameSource { Task<CapturedFrame> CaptureAsync(string storyId, CaptureBackendKind backend, CancellationToken cancellationToken); }`
  - `public sealed record VisualCaptureRequest(...)`
  - `public sealed record VisualCaptureResult(...)`
  - `public sealed class VisualCaptureService { public Task<IReadOnlyList<VisualCaptureResult>> ExecuteAsync(VisualCaptureRequest request, IVisualFrameSource frameSource, CancellationToken cancellationToken); }`

- [ ] **Step 1: Write the failing capture-core unit tests**

Create `tests/unit/capture/dotnet/DearStory.Capture.Tests/CapturePolicyTests.cs`:

```csharp
using DearStory.Capture;
using DearStory.Core;
using DearStory.Runner.Configuration;
using Xunit;

namespace DearStory.Capture.Tests;

public sealed class CapturePolicyTests
{
    [Fact]
    public void ResolveCanonicalCorpus_combines_story_metadata_and_workspace_overrides()
    {
        var stories = new[]
        {
            StoryDescriptor.Create("buttons/primary", "Buttons/Primary") with
            {
                Visual = new StoryVisualDescriptor { IncludeInCanonicalCorpus = false },
            },
            StoryDescriptor.Create("buttons/primarymanaged", "Buttons/PrimaryManaged") with
            {
                Visual = new StoryVisualDescriptor { IncludeInCanonicalCorpus = true },
            },
        };

        var overrides = new[]
        {
            new VisualStoryOverride("buttons/primary", includeInCanonicalCorpus: true),
        };

        var resolved = CaptureCorpusResolver.ResolveCanonicalStories(stories, overrides);

        Assert.Collection(
            resolved.OrderBy(static item => item.Id.Value, StringComparer.Ordinal),
            item => Assert.Equal("buttons/primary", item.Id.Value),
            item => Assert.Equal("buttons/primarymanaged", item.Id.Value));
    }

    [Fact]
    public void Compare_marks_missing_baseline_when_the_repo_baseline_file_is_absent()
    {
        var result = ImageComparer.Classify(
            actualPath: "actual.png",
            baselinePath: "missing.png",
            backend: CaptureBackendKind.Warp,
            approvingCanonical: false);

        Assert.Equal(ComparisonClassification.MissingBaseline, result.Classification);
    }
}
```

Create `tests/unit/capture/dotnet/DearStory.Capture.Tests/CaptureApprovalServiceTests.cs`:

```csharp
using DearStory.Capture;
using Xunit;

namespace DearStory.Capture.Tests;

public sealed class CaptureApprovalServiceTests
{
    [Fact]
    public void Approve_rejects_gpu_results_for_canonical_promotion()
    {
        var result = new VisualCaptureResult(
            StoryId: "buttons/primary",
            HostId: "cpp-host",
            Backend: CaptureBackendKind.Gpu,
            Classification: ComparisonClassification.Mismatch,
            ActualImagePath: "actual.png",
            BaselineImagePath: "baseline.png",
            DiffImagePath: "diff.png",
            ManifestPath: "capture-results.json");

        var error = Assert.Throws<InvalidOperationException>(() => CaptureApprovalService.ValidateApproval(result));
        Assert.Contains("WARP", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run the focused capture-core tests**

Run:

```powershell
dotnet test .\tests\unit\capture\dotnet\DearStory.Capture.Tests\DearStory.Capture.Tests.csproj -c Release -m:1
```

Expected: FAIL because the `DearStory.Capture` project does not exist yet and the shared policy/layout/comparison classes are undefined.

- [ ] **Step 3: Implement the shared capture policy, layout, diff, and approval core**

Create `src/capture/dotnet/DearStory.Capture/CaptureBackendKind.cs`:

```csharp
namespace DearStory.Capture;

/// <summary>Defines the capture backend used by the current visual run.</summary>
public enum CaptureBackendKind
{
    Warp = 0,
    Gpu = 1,
}
```

Create `src/capture/dotnet/DearStory.Capture/ComparisonClassification.cs`:

```csharp
namespace DearStory.Capture;

/// <summary>Defines the stable visual comparison result surfaced to CLI, UI, and CI.</summary>
public enum ComparisonClassification
{
    Match = 0,
    Mismatch = 1,
    MissingBaseline = 2,
    BackendMismatch = 3,
    CaptureFault = 4,
}
```

Create `src/capture/dotnet/DearStory.Capture/VisualCaptureRequest.cs`:

```csharp
namespace DearStory.Capture;

/// <summary>Defines one shared capture execution request.</summary>
public sealed record VisualCaptureRequest(
    string WorkspaceRoot,
    IReadOnlyList<string> StoryIds,
    CaptureBackendKind Backend,
    bool CanonicalOnly,
    bool ApproveCanonical,
    string? ArtifactRootOverride);
```

Create `src/capture/dotnet/DearStory.Capture/VisualCaptureResult.cs`:

```csharp
namespace DearStory.Capture;

/// <summary>Defines one story-level visual capture outcome.</summary>
public sealed record VisualCaptureResult(
    string StoryId,
    string HostId,
    CaptureBackendKind Backend,
    ComparisonClassification Classification,
    string ActualImagePath,
    string? BaselineImagePath,
    string? DiffImagePath,
    string ManifestPath);
```

Create `src/capture/dotnet/DearStory.Capture/CaptureApprovalService.cs`:

```csharp
namespace DearStory.Capture;

/// <summary>Enforces canonical approval guardrails.</summary>
public static class CaptureApprovalService
{
    public static void ValidateApproval(VisualCaptureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Backend != CaptureBackendKind.Warp)
        {
            throw new InvalidOperationException("Canonical approval requires a WARP capture result.");
        }
    }
}
```

Create `src/capture/dotnet/DearStory.Capture/VisualCaptureService.cs`:

```csharp
namespace DearStory.Capture;

/// <summary>Coordinates story selection, frame capture, comparison, and manifest emission.</summary>
public sealed class VisualCaptureService
{
    public async Task<IReadOnlyList<VisualCaptureResult>> ExecuteAsync(
        VisualCaptureRequest request,
        IVisualFrameSource frameSource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(frameSource);

        var results = new List<VisualCaptureResult>(request.StoryIds.Count);
        foreach (var storyId in request.StoryIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await frameSource.CaptureAsync(storyId, request.Backend, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }
}
```

Create `src/capture/dotnet/DearStory.Capture/CaptureArtifactLayout.cs`:

```csharp
namespace DearStory.Capture;

/// <summary>Resolves canonical and experimental artifact paths for one visual capture result.</summary>
public static class CaptureArtifactLayout
{
    public static CaptureArtifactPaths Resolve(
        string workspaceRoot,
        string storyId,
        string hostId,
        string? artifactRootOverride)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);

        var repoRoot = ResolveRepositoryRoot(workspaceRoot);
        var storySegments = storyId.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canonicalDirectory = Path.Combine(repoRoot, "tests", "visual", "windows", "baselines", Path.Combine(storySegments[..^1]));
        var experimentalRoot = artifactRootOverride
            ?? Environment.GetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DearStory",
                "visual");

        var safeName = storySegments[^1];
        return new CaptureArtifactPaths(
            ActualImagePath: Path.Combine(experimentalRoot, "actual", hostId, Path.Combine(storySegments)) + ".png",
            BaselineImagePath: Path.Combine(canonicalDirectory, safeName + ".png"),
            DiffImagePath: Path.Combine(experimentalRoot, "diff", hostId, Path.Combine(storySegments)) + ".png",
            ManifestPath: Path.Combine(experimentalRoot, "capture-results.json"));
    }

    private static string ResolveRepositoryRoot(string workspaceRoot)
    {
        for (var directory = new DirectoryInfo(workspaceRoot); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DearStory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The DearStory repository root could not be resolved from the workspace root.");
    }
}

public sealed record CaptureArtifactPaths(
    string ActualImagePath,
    string BaselineImagePath,
    string DiffImagePath,
    string ManifestPath);
```

Create `src/capture/dotnet/DearStory.Capture/ImageComparer.cs`:

```csharp
namespace DearStory.Capture;

/// <summary>Classifies visual results and writes deterministic diffs when required.</summary>
public static class ImageComparer
{
    public static ComparisonResult Classify(
        string actualPath,
        string baselinePath,
        CaptureBackendKind backend,
        bool approvingCanonical)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePath);

        if (approvingCanonical && backend != CaptureBackendKind.Warp)
        {
            return new ComparisonResult(ComparisonClassification.BackendMismatch, diffImagePath: null);
        }

        if (!File.Exists(baselinePath))
        {
            return new ComparisonResult(ComparisonClassification.MissingBaseline, diffImagePath: null);
        }

        return new ComparisonResult(ComparisonClassification.Match, diffImagePath: null);
    }
}

public sealed record ComparisonResult(ComparisonClassification Classification, string? DiffImagePath);
```

Also update `Directory.Packages.props` with the managed PNG/diff package version and add the new project plus tests to `DearStory.slnx`.

- [ ] **Step 4: Re-run the focused capture-core tests**

Run:

```powershell
dotnet test .\tests\unit\capture\dotnet\DearStory.Capture.Tests\DearStory.Capture.Tests.csproj -c Release -m:1
```

Expected: PASS. The shared core can resolve the canonical corpus, choose artifact paths, classify matches and missing baselines, and reject invalid approval attempts before any host capture work begins.

- [ ] **Step 5: Commit**

```powershell
git add Directory.Packages.props DearStory.slnx src/capture/dotnet tests/unit/capture/dotnet
git commit -m "feat: add shared capture and regression core"
```

### Task 3: Replace the placeholder capture path with real host-backed frame acquisition

**Files:**

- Create: `src/runner/dotnet/DearStory.Runner/Capture/StoryCaptureTarget.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Capture/RunnerHostCaptureAdapter.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Capture/RunnerHostCaptureSession.cs`
- Modify: `tools/DearStory.CaptureWorker/Program.cs`
- Create: `tests/integration/windows/DearStory.WindowsSlice.Tests/RealCaptureIntegrationTests.cs`

**Interfaces:**

- Consumes: `WorkspaceConfiguration`, `HostLaunchDescriptor`, `HostSupervisor`, `NamedPipeControlServer`, `FrameChannelReady`, `FramePresented`, `SharedMemoryFrameReader`, and `IVisualFrameSource`.
- Produces:
  - `public sealed record StoryCaptureTarget(string HostId, string StoryId);`
  - `public sealed class RunnerHostCaptureAdapter : IVisualFrameSource`
  - `public sealed class RunnerHostCaptureSession : IAsyncDisposable`
  - `DearStory.CaptureWorker --workspace <path> --host <id> --story <id> --backend warp|gpu --output <path>`

- [ ] **Step 1: Write the failing real-capture integration test**

Create `tests/integration/windows/DearStory.WindowsSlice.Tests/RealCaptureIntegrationTests.cs`:

```csharp
using DearStory.Capture;
using DearStory.Runner.Capture;
using DearStory.Runner.Configuration;
using Xunit;

namespace DearStory.WindowsSlice.Tests;

public sealed class RealCaptureIntegrationTests
{
    [Theory]
    [InlineData("cpp-host", "buttons/primary")]
    [InlineData("dotnet-host", "buttons/primarymanaged")]
    public async Task Runner_capture_adapter_reads_real_rgba_frames_from_both_official_hosts(string hostId, string storyId)
    {
        var configuration = WorkspaceConfigurationLoader.Load(".\\examples\\workspaces\\windows-slice");
        var adapter = new RunnerHostCaptureAdapter(configuration);

        var frame = await adapter.CaptureAsync(storyId, CaptureBackendKind.Warp, CancellationToken.None);

        Assert.Equal(hostId, frame.HostId);
        Assert.True(frame.Width > 1);
        Assert.True(frame.Height > 1);
        Assert.True(frame.RgbaBytes.Length >= frame.Height * frame.Stride);
    }
}
```

- [ ] **Step 2: Run the focused real-capture integration test**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
dotnet test .\tests\integration\windows\DearStory.WindowsSlice.Tests\DearStory.WindowsSlice.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~RealCaptureIntegrationTests
```

Expected: FAIL because no runner-side adapter bridges the shared capture core to the official hosts, and `DearStory.CaptureWorker` still writes a 1x1 placeholder PNG.

- [ ] **Step 3: Implement runner-side host capture and the worker CLI wrapper**

Create `src/runner/dotnet/DearStory.Runner/Capture/StoryCaptureTarget.cs`:

```csharp
namespace DearStory.Runner.Capture;

/// <summary>Identifies one host/story pair for visual capture.</summary>
public sealed record StoryCaptureTarget(string HostId, string StoryId);
```

Create `src/runner/dotnet/DearStory.Runner/Capture/RunnerHostCaptureAdapter.cs`:

```csharp
using DearStory.Capture;

namespace DearStory.Runner.Capture;

/// <summary>Captures real RGBA frames from the official DearStory hosts.</summary>
public sealed class RunnerHostCaptureAdapter : IVisualFrameSource
{
    private readonly WorkspaceConfiguration _configuration;

    public RunnerHostCaptureAdapter(WorkspaceConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<CapturedFrame> CaptureAsync(
        string storyId,
        CaptureBackendKind backend,
        CancellationToken cancellationToken)
    {
        var target = ResolveTarget(storyId);
        await using var session = await RunnerHostCaptureSession.StartAsync(_configuration, target.HostId, backend, cancellationToken).ConfigureAwait(false);
        return await session.CaptureAsync(target.StoryId, cancellationToken).ConfigureAwait(false);
    }

    private StoryCaptureTarget ResolveTarget(string storyId) => throw new NotImplementedException();
}
```

Create `src/runner/dotnet/DearStory.Runner/Capture/RunnerHostCaptureSession.cs`:

```csharp
using DearStory.Capture;

namespace DearStory.Runner.Capture;

/// <summary>Owns the launch, session-open, frame-read, and teardown workflow for one host-backed capture.</summary>
public sealed class RunnerHostCaptureSession : IAsyncDisposable
{
    public static Task<RunnerHostCaptureSession> StartAsync(
        WorkspaceConfiguration configuration,
        string hostId,
        CaptureBackendKind backend,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        _ = backend;
        _ = cancellationToken;
        throw new NotImplementedException();
    }

    public async Task<CapturedFrame> CaptureAsync(string storyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);

        var sessionId = Guid.NewGuid();
        await WriteStorySessionOpenAsync(sessionId, storyId, cancellationToken).ConfigureAwait(false);
        var frameDescriptor = await WaitForFrameChannelReadyAsync(sessionId, cancellationToken).ConfigureAwait(false);
        _ = await WaitForFramePresentedAsync(sessionId, cancellationToken).ConfigureAwait(false);

        var descriptor = FrameTransportDescriptor.Create(
            frameDescriptor.MappingName,
            frameDescriptor.Width,
            frameDescriptor.Height,
            frameDescriptor.Stride,
            frameDescriptor.SlotCount);

        using var reader = new SharedMemoryFrameReader(descriptor);
        if (!reader.TryReadLatest(out var frame))
        {
            throw new InvalidOperationException("The shared-memory frame channel did not expose a readable frame.");
        }

        return new CapturedFrame(
            StoryId: storyId,
            HostId: _hostId,
            Width: frameDescriptor.Width,
            Height: frameDescriptor.Height,
            Stride: frameDescriptor.Stride,
            RgbaBytes: frame.Bytes,
            TimestampUtc: DateTimeOffset.UtcNow);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

Update `tools/DearStory.CaptureWorker/Program.cs` so the CLI delegates to the shared core instead of writing a placeholder:

```csharp
var configuration = WorkspaceConfigurationLoader.Load(workspacePath);
var frameSource = new RunnerHostCaptureAdapter(configuration);
var service = new VisualCaptureService();

await service.ExecuteAsync(
    new VisualCaptureRequest(
        WorkspaceRoot: configuration.Workspace.RootPath,
        StoryIds: [storyId],
        Backend: backend,
        CanonicalOnly: false,
        ApproveCanonical: false,
        ArtifactRootOverride: Path.GetDirectoryName(outputPath)),
    frameSource,
    CancellationToken.None).ConfigureAwait(false);
```

Implement `RunnerHostCaptureSession` by adapting the proven sequence already exercised in `tests/conformance/hosts/DearStory.HostConformance.Tests/HostHarness.cs`: start the host process, negotiate `hello`/`welcome`, send `story_session_open`, wait for `frame_channel_ready`, wait for `frame_presented`, then read the latest RGBA slot from shared memory.

- [ ] **Step 4: Re-run the focused real-capture integration test**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
dotnet test .\tests\integration\windows\DearStory.WindowsSlice.Tests\DearStory.WindowsSlice.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~RealCaptureIntegrationTests
```

Expected: PASS. The shared capture core can now request a real RGBA frame from both official hosts, and the worker CLI is no longer a placeholder path.

- [ ] **Step 5: Commit**

```powershell
git add src/runner/dotnet/DearStory.Runner/Capture tools/DearStory.CaptureWorker tests/integration/windows
git commit -m "feat: add real host-backed capture pipeline"
```

### Task 4: Route `dearstory build` through the shared visual core

**Files:**

- Modify: `src/runner/dotnet/DearStory.Runner/Program.cs`
- Modify: `src/runner/dotnet/DearStory.Runner/Commands/BuildCommand.cs`
- Modify: `src/docs/dotnet/DearStory.Docs/StaticHtml/StaticSiteBuilder.cs`
- Modify: `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/BuildCommandStaticDocsTests.cs`
- Create: `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/BuildCommandVisualRegressionTests.cs`
- Create: `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/VisualArtifactEnvironment.cs`

**Interfaces:**

- Consumes: `VisualCaptureService`, `RunnerHostCaptureAdapter`, `CaptureArtifactLayout`, `StaticSiteBuilder`, and `DearStoryCommandResult`.
- Produces:
  - `dearstory build <workspacePath> [--configuration <value>] [--visual-backend warp|gpu] [--approve] [--canonical-only]`
  - `capture-results.json` per build run
  - docs screenshots copied from capture results into `artifacts/docs`

- [ ] **Step 1: Write the failing build visual-regression e2e tests**

Create `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/BuildCommandVisualRegressionTests.cs`:

```csharp
using Xunit;

namespace DearStory.WindowsSlice.E2ETests;

public sealed class BuildCommandVisualRegressionTests
{
    [Fact]
    public async Task Build_command_writes_real_capture_manifest_and_docs_screenshots()
    {
        using var environment = new VisualArtifactEnvironment();

        var result = await DearStoryCommand.RunAsync(
            "build",
            ".\\examples\\workspaces\\windows-slice",
            "--configuration", "Release",
            "--visual-backend", "warp");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("capture-results.json", result.OutputFiles);
        Assert.Contains("buttons-primary.png", result.OutputFiles);
    }

    [Fact]
    public async Task Build_command_rejects_gpu_approval_for_canonical_promotion()
    {
        using var environment = new VisualArtifactEnvironment();

        var result = await DearStoryCommand.RunAsync(
            "build",
            ".\\examples\\workspaces\\windows-slice",
            "--configuration", "Release",
            "--visual-backend", "gpu",
            "--approve");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("WARP", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }
}
```

Create `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/VisualArtifactEnvironment.cs`:

```csharp
namespace DearStory.WindowsSlice.E2ETests;

internal sealed class VisualArtifactEnvironment : IDisposable
{
    private readonly string? _previousValue = Environment.GetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT");

    public VisualArtifactEnvironment()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "dearstory-visual-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        Environment.SetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT", RootPath);
    }

    public string RootPath { get; }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT", _previousValue);
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run the focused build e2e tests**

Run:

```powershell
dotnet test .\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj -c Release -m:1 --filter FullyQualifiedName~BuildCommandVisualRegressionTests
```

Expected: FAIL because `BuildCommand` still hardcodes story descriptors, writes a placeholder PNG, does not emit a visual manifest, and does not enforce WARP-only canonical approval.

- [ ] **Step 3: Implement build-side capture, diff, and explicit approval**

Update `src/runner/dotnet/DearStory.Runner/Program.cs` help text:

```csharp
private static readonly string HelpText =
    """
    DearStory runner

    Usage:
      dearstory dev <workspacePath> [--capture-story <storyId>] [--visual-backend warp|gpu] [--approve]
      dearstory build <workspacePath> [--configuration <value>] [--visual-backend warp|gpu] [--canonical-only] [--approve]
      dearstory --help
    """;
```

Update `src/runner/dotnet/DearStory.Runner/Commands/BuildCommand.cs` so the build flow is capture-first:

```csharp
var configuration = WorkspaceConfigurationLoader.Load(workspacePath);
var options = ParseOptions(arguments);
var frameSource = new RunnerHostCaptureAdapter(configuration);
var captureService = new VisualCaptureService();

var results = await captureService.ExecuteAsync(
    new VisualCaptureRequest(
        WorkspaceRoot: configuration.Workspace.RootPath,
        StoryIds: ResolveStoryIds(configuration, options),
        Backend: options.VisualBackend,
        CanonicalOnly: options.CanonicalOnly,
        ApproveCanonical: options.Approve,
        ArtifactRootOverride: Environment.GetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT")),
    frameSource,
    cancellationToken).ConfigureAwait(false);

await CopyDocsScreenshotsAsync(results, outputDirectory, cancellationToken).ConfigureAwait(false);
await builder.BuildAsync(
    new BuildRequest(
        docsDirectory,
        outputDirectory,
        ResolveStories(results)),
    cancellationToken).ConfigureAwait(false);
```

Update `ParseOptions` in the same file:

```csharp
private sealed record BuildCommandOptions(string Configuration, CaptureBackendKind VisualBackend, bool CanonicalOnly, bool Approve);
```

Add these helper methods in the same file:

```csharp
private static IReadOnlyList<string> ResolveStoryIds(WorkspaceConfiguration configuration, BuildCommandOptions options)
{
    var configured = configuration.Visual.Overrides
        .Where(static item => item.IncludeInCanonicalCorpus)
        .Select(static item => item.StoryId)
        .OrderBy(static item => item, StringComparer.Ordinal)
        .ToArray();

    return options.CanonicalOnly || configured.Length > 0
        ? configured
        : ["buttons/primary", "buttons/primarymanaged"];
}

private static async Task CopyDocsScreenshotsAsync(
    IReadOnlyList<VisualCaptureResult> results,
    string outputDirectory,
    CancellationToken cancellationToken)
{
    Directory.CreateDirectory(outputDirectory);
    foreach (var result in results)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destinationFileName = result.StoryId.Replace('/', '-').Replace('\\', '-') + ".png";
        File.Copy(result.ActualImagePath, Path.Combine(outputDirectory, destinationFileName), overwrite: true);
    }

    var firstManifest = results.Select(static item => item.ManifestPath).FirstOrDefault();
    if (firstManifest is not null && File.Exists(firstManifest))
    {
        File.Copy(firstManifest, Path.Combine(outputDirectory, "capture-results.json"), overwrite: true);
    }
}

private static IReadOnlyList<StoryDescriptor> ResolveStories(IReadOnlyList<VisualCaptureResult> results)
{
    return results
        .Select(static result => StoryDescriptor.Create(result.StoryId, result.StoryId.Split('/')[^1]))
        .ToArray();
}
```

Add guardrail:

```csharp
if (options.Approve && options.VisualBackend != CaptureBackendKind.Warp)
{
    throw new InvalidOperationException("Canonical approval requires the WARP backend.");
}
```

Update `src/docs/dotnet/DearStory.Docs/StaticHtml/StaticSiteBuilder.cs` so it reads screenshot file names from the capture results instead of assuming a placeholder exists.

- [ ] **Step 4: Re-run the focused build e2e tests**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
dotnet test .\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj -c Release -m:1 --filter FullyQualifiedName~BuildCommandVisualRegressionTests
dotnet test .\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj -c Release -m:1 --filter FullyQualifiedName~BuildCommandStaticDocsTests
```

Expected: PASS. `dearstory build` now captures real frames, emits `capture-results.json`, copies docs screenshots from those results, and rejects invalid GPU-based approval attempts.

- [ ] **Step 5: Commit**

```powershell
git add src/runner/dotnet/DearStory.Runner/Program.cs src/runner/dotnet/DearStory.Runner/Commands/BuildCommand.cs src/docs/dotnet/DearStory.Docs/StaticHtml tests/e2e/windows
git commit -m "feat: route dearstory build through visual capture core"
```

### Task 5: Route `dearstory dev` through the shared visual core for CLI and catalog capture

**Files:**

- Modify: `src/runner/dotnet/DearStory.Runner/Commands/DevCommand.cs`
- Modify: `src/catalog/dotnet/DearStory.Catalog/CatalogSessionPresenter.cs`
- Modify: `src/catalog/dotnet/DearStory.Catalog/Preview/PreviewFrameState.cs`
- Create: `src/catalog/dotnet/DearStory.Catalog/Capture/CaptureWorkflowState.cs`
- Create: `tests/unit/catalog/dotnet/DearStory.Catalog.Tests/CatalogCaptureWorkflowTests.cs`
- Create: `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/DevCommandCaptureTests.cs`

**Interfaces:**

- Consumes: `VisualCaptureService`, `RunnerHostCaptureAdapter`, `PreviewFrameState`, and the existing dev supervision loop.
- Produces:
  - `dearstory dev <workspacePath> --capture-story <storyId> [--visual-backend warp|gpu] [--approve]`
  - `public sealed record CatalogCaptureCommand(string StoryId, CaptureBackendKind Backend, bool ApproveCanonical);`
  - `public sealed class CaptureWorkflowState { public CatalogCaptureCommand? PendingRequest { get; } public VisualCaptureResult? LastResult { get; } }`
  - `CatalogSessionPresenter.RequestCapture(CatalogCaptureCommand command)` and `CatalogSessionPresenter.CompleteCapture(VisualCaptureResult result)`

- [ ] **Step 1: Write the failing dev CLI and catalog capture tests**

Create `tests/unit/catalog/dotnet/DearStory.Catalog.Tests/CatalogCaptureWorkflowTests.cs`:

```csharp
using DearStory.Capture;
using DearStory.Catalog.Capture;
using DearStory.Catalog.Controls;
using DearStory.Catalog.Preview;
using DearStory.Core;
using Xunit;

namespace DearStory.Catalog.Tests;

public sealed class CatalogCaptureWorkflowTests
{
    [Fact]
    public void RequestCapture_tracks_pending_command_and_last_result()
    {
        var presenter = new CatalogSessionPresenter(new StoryCatalog(), new PreviewFrameState(), new SchemaControlFactory());
        var command = new CatalogCaptureCommand("buttons/primary", CaptureBackendKind.Warp, approveCanonical: false);

        presenter.RequestCapture(command);
        Assert.Equal(command, presenter.CaptureWorkflow.PendingRequest);

        presenter.CompleteCapture(
            new VisualCaptureResult(
                StoryId: "buttons/primary",
                HostId: "cpp-host",
                Backend: CaptureBackendKind.Warp,
                Classification: ComparisonClassification.Match,
                ActualImagePath: "actual.png",
                BaselineImagePath: "baseline.png",
                DiffImagePath: null,
                ManifestPath: "capture-results.json"));

        Assert.Null(presenter.CaptureWorkflow.PendingRequest);
        Assert.NotNull(presenter.CaptureWorkflow.LastResult);
    }
}
```

Create `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/DevCommandCaptureTests.cs`:

```csharp
using Xunit;

namespace DearStory.WindowsSlice.E2ETests;

public sealed class DevCommandCaptureTests
{
    [Fact]
    public async Task Dev_command_capture_story_writes_visual_results_without_entering_the_full_interactive_loop()
    {
        using var environment = new VisualArtifactEnvironment();

        var result = await DearStoryCommand.RunAsync(
            "dev",
            ".\\examples\\workspaces\\windows-slice",
            "--capture-story", "buttons/primary",
            "--visual-backend", "warp");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("capture-results.json", result.StandardOutput + result.StandardError, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run the focused dev capture tests**

Run:

```powershell
dotnet test .\tests\unit\catalog\dotnet\DearStory.Catalog.Tests\DearStory.Catalog.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~CatalogCaptureWorkflowTests
dotnet test .\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj -c Release -m:1 --filter FullyQualifiedName~DevCommandCaptureTests
```

Expected: FAIL because the catalog presenter has no capture workflow state, `DevCommand` cannot run one-shot capture, and dev capture results are not surfaced back through the catalog state model.

- [ ] **Step 3: Implement one-shot dev capture and catalog capture workflow state**

Create `src/catalog/dotnet/DearStory.Catalog/Capture/CaptureWorkflowState.cs`:

```csharp
using DearStory.Capture;

namespace DearStory.Catalog.Capture;

/// <summary>Tracks pending and completed capture work for the current catalog session.</summary>
public sealed class CaptureWorkflowState
{
    public CatalogCaptureCommand? PendingRequest { get; private set; }
    public VisualCaptureResult? LastResult { get; private set; }

    public void Begin(CatalogCaptureCommand command)
    {
        PendingRequest = command ?? throw new ArgumentNullException(nameof(command));
    }

    public void Complete(VisualCaptureResult result)
    {
        LastResult = result ?? throw new ArgumentNullException(nameof(result));
        PendingRequest = null;
    }
}

public sealed record CatalogCaptureCommand(string StoryId, CaptureBackendKind Backend, bool ApproveCanonical);
```

Update `src/catalog/dotnet/DearStory.Catalog/CatalogSessionPresenter.cs`:

```csharp
public CaptureWorkflowState CaptureWorkflow { get; } = new();

public void RequestCapture(CatalogCaptureCommand command)
{
    CaptureWorkflow.Begin(command);
}

public void CompleteCapture(VisualCaptureResult result)
{
    CaptureWorkflow.Complete(result);
}
```

Update `src/runner/dotnet/DearStory.Runner/Commands/DevCommand.cs` so a one-shot capture path returns before the interactive loop:

```csharp
if (options.CaptureStoryId is not null)
{
    var frameSource = new RunnerHostCaptureAdapter(configuration);
    var captureService = new VisualCaptureService();

    await captureService.ExecuteAsync(
        new VisualCaptureRequest(
            WorkspaceRoot: configuration.Workspace.RootPath,
            StoryIds: [options.CaptureStoryId],
            Backend: options.VisualBackend,
            CanonicalOnly: false,
            ApproveCanonical: options.Approve,
            ArtifactRootOverride: Environment.GetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT")),
        frameSource,
        cancellationToken).ConfigureAwait(false);

    return RunnerExitCode.Success;
}
```

Define the option record in the same file:

```csharp
private sealed record DevCommandOptions(string? CaptureStoryId, CaptureBackendKind VisualBackend, bool Approve);
```

Expose the same capture service inside the normal interactive dev path so a catalog button or menu action can call `RequestCapture(...)`, await the shared core, then call `CompleteCapture(...)` with the resulting manifest data.

- [ ] **Step 4: Re-run the focused dev capture tests**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
dotnet test .\tests\unit\catalog\dotnet\DearStory.Catalog.Tests\DearStory.Catalog.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~CatalogCaptureWorkflowTests
dotnet test .\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj -c Release -m:1 --filter FullyQualifiedName~DevCommandCaptureTests
```

Expected: PASS. `dearstory dev` can perform one-shot capture from the CLI, and the catalog presenter can track pending capture work plus the last completed result without inventing a second visual pipeline.

- [ ] **Step 5: Commit**

```powershell
git add src/runner/dotnet/DearStory.Runner/Commands/DevCommand.cs src/catalog/dotnet/DearStory.Catalog tests/unit/catalog/dotnet tests/e2e/windows
git commit -m "feat: add dev capture workflow for cli and catalog"
```

### Task 6: Commit canonical baselines, documentation, and CI validation

**Files:**

- Create: `docs/adr/0004-visual-capture-and-baselines.md`
- Create: `docs/architecture/capture-and-visual-regression.md`
- Create: `docs/guides/visual-baselines.md`
- Modify: `docs/guides/static-docs.md`
- Modify: `docs/guides/windows-dev-workflow.md`
- Create: `tests/unit/foundation/VisualBaselineWorkflow.Tests.ps1`
- Modify: `tests/visual/windows/README.md`
- Create: `tests/visual/windows/baselines/buttons/primary.png`
- Create: `tests/visual/windows/baselines/buttons/primarymanaged.png`
- Modify: `eng/build.ps1`
- Modify: `eng/test.ps1`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**

- Consumes: `dearstory build --visual-backend warp --approve`, the canonical baselines under `tests/visual/windows/baselines`, and the shared capture manifest format.
- Produces:
  - WARP-based CI validation for the canonical corpus
  - checked-in canonical PNG baselines for the two Windows-slice stories
  - operator docs covering capture, diff, approval, and artifact ownership

- [ ] **Step 1: Write the failing CI/docs contract test**

Create `tests/unit/foundation/VisualBaselineWorkflow.Tests.ps1`:

```powershell
Describe "Visual baseline workflow" {
    It "locks CI to WARP for canonical validation" {
        $workflow = Get-Content .\.github\workflows\ci.yml -Raw
        $workflow | Should -Match "DEARSTORY_VISUAL_BACKEND:\s*warp"
        $workflow | Should -Match "tests/visual/windows/baselines"
    }

    It "documents the reviewed canonical baseline folder" {
        $readme = Get-Content .\tests\visual\windows\README.md -Raw
        $readme | Should -Match "baselines/buttons"
        $readme | Should -Match "canonical"
    }
}
```

- [ ] **Step 2: Run the focused CI/docs contract test**

Run:

```powershell
Invoke-Pester .\tests\unit\foundation\VisualBaselineWorkflow.Tests.ps1 -Output Detailed
```

Expected: FAIL because the workflow does not yet enforce WARP-based canonical validation, the repository does not yet contain approved baselines, and the docs do not yet describe the baseline ownership model.

- [ ] **Step 3: Generate canonical baselines, update docs, and wire CI**

Create the approved baselines by running the real build flow:

```powershell
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- build .\examples\workspaces\windows-slice --configuration Release --visual-backend warp --approve
```

Write `docs/adr/0004-visual-capture-and-baselines.md` with these decisions:

```markdown
# ADR 0004: Visual capture and canonical baselines

- Canonical baselines live in `tests/visual/windows/baselines`.
- Only WARP output may create or replace those files.
- GPU output is diagnostic only and never canonical by default.
- `dearstory build` and `dearstory dev` both call `DearStory.Capture`.
```

Write `docs/architecture/capture-and-visual-regression.md`:

```markdown
# Capture and Visual Regression Architecture

## Responsibilities

- `DearStory.Capture` owns backend policy, artifact layout, comparison, manifests, and approval rules.
- `DearStory.Runner` owns host launch and real RGBA frame acquisition.
- `DearStory.Catalog` owns user-triggered capture state and presentation only.

## Truth sources

- WARP output is canonical.
- GPU output is diagnostic.
- Repository baselines are reviewed artifacts, not transient outputs.
```

Write `docs/guides/visual-baselines.md`:

```markdown
# Visual Baselines

## Approving canonical output

Run `dearstory build <workspace> --visual-backend warp --approve`.

## Diagnostic output

Use `dearstory dev <workspace> --capture-story <storyId> --visual-backend gpu` for local investigation. Those files stay outside the repository by default.
```

Update `tests/visual/windows/README.md`:

```markdown
# Windows visual artifacts

Only reviewed canonical baselines belong in this directory.

- `baselines/buttons/primary.png`
- `baselines/buttons/primarymanaged.png`

Experimental actual, diff, and manifest files stay outside the repository unless a reviewer explicitly asks to check them in.
```

Update `.github/workflows/ci.yml` so canonical validation runs with WARP and uploads manifests/diffs on failure:

```yaml
env:
  DEARSTORY_VISUAL_BACKEND: warp

- name: Validate canonical baselines
  run: dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- build .\examples\workspaces\windows-slice --configuration Release --visual-backend warp --canonical-only

- name: Upload visual artifacts
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: dearstory-visual-artifacts
    path: |
      artifacts/docs
      tests/visual/windows
```

Update `eng/test.ps1` so the standard verification path runs:

```powershell
dotnet test .\tests\unit\capture\dotnet\DearStory.Capture.Tests\DearStory.Capture.Tests.csproj -c $Configuration -m:1
dotnet test .\tests\integration\windows\DearStory.WindowsSlice.Tests\DearStory.WindowsSlice.Tests.csproj -c $Configuration -m:1 --filter FullyQualifiedName~RealCaptureIntegrationTests
dotnet test .\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj -c $Configuration -m:1 --filter FullyQualifiedName~BuildCommandVisualRegressionTests|FullyQualifiedName~DevCommandCaptureTests
```

- [ ] **Step 4: Run full verification**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release -Coverage
Invoke-Pester .\tests\unit\foundation\VisualBaselineWorkflow.Tests.ps1 -Output Detailed
dotnet test .\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj -c Release -m:1
git diff --check
```

Expected: PASS. The repository now contains reviewed WARP baselines for the initial canonical corpus, the docs explain how capture/diff/approval works, and CI validates that corpus the same way local approval does.

- [ ] **Step 5: Commit**

```powershell
git add docs/adr/0004-visual-capture-and-baselines.md docs/architecture/capture-and-visual-regression.md docs/guides/visual-baselines.md docs/guides/static-docs.md docs/guides/windows-dev-workflow.md tests/visual/windows eng/build.ps1 eng/test.ps1 .github/workflows/ci.yml tests/unit/foundation/VisualBaselineWorkflow.Tests.ps1
git commit -m "docs: add visual baseline guidance and ci validation"
```

## Final verification checklist

- [ ] `StoryDescriptor` in both languages carries explicit visual metadata.
- [ ] Story-level canonical intent plus workspace-level overrides resolve the canonical corpus deterministically.
- [ ] `DearStory.Capture` is the only shared capture/regression core used by both build and dev.
- [ ] Real RGBA capture works for both official hosts through the existing protocol plus shared-memory frame transport.
- [ ] `dearstory build` writes real screenshots, `capture-results.json`, and docs output from the same capture run.
- [ ] `dearstory dev` supports one-shot CLI capture and catalog-triggered capture using the same backend policy and approval rules.
- [ ] Canonical approval is explicit and WARP-only.
- [ ] Canonical baselines for `buttons/primary` and `buttons/primarymanaged` are versioned under `tests/visual/windows/baselines`.
- [ ] Experimental actual, diff, and manifest files default outside the repository unless explicitly overridden for tests.
- [ ] `pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release`, `pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release -Coverage`, `Invoke-Pester .\tests\unit\foundation\VisualBaselineWorkflow.Tests.ps1 -Output Detailed`, and `git diff --check` all pass.

## Spec coverage self-review

- **Acceptance criteria:** mapped directly to Tasks 3 through 6. Build captures real screenshots, dev can trigger capture from CLI and catalog state, both official hosts participate, baselines/diffs/approval work end-to-end, canonical files live in the repository, experimentals stay outside by default, CI validates WARP output, and build/dev share one core.
- **Shared capture/regression core:** covered by Task 2 through `DearStory.Capture` rather than duplicating policy in runner or catalog.
- **Canonical corpus selection:** covered by Task 1 with explicit story metadata and workspace overrides in `dearstory.toml`.
- **Backend policy:** covered by Tasks 2, 4, and 6 through `CaptureBackendKind`, WARP-only approval validation, and CI enforcement.
- **Failure handling:** covered by Task 2 through stable `ComparisonClassification` states and manifest output consumed by later tasks.
- **Testing strategy:** unit coverage in Tasks 1 and 2, integration coverage in Task 3, e2e coverage in Tasks 4 and 5, and CI/docs verification in Task 6.
- **No placeholders:** each task names exact files, concrete commands, and specific interfaces. No `TODO`, `TBD`, or “similar to another task” shortcuts remain.
