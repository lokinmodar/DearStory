# DearStory Core Story Model and Schemas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the language-neutral story model, schema and patch contract, catalog/session state, and thin C++/.NET SDK authoring surfaces that future hosts and the native catalog can consume without wrapping the Dear ImGui widget API.

**Architecture:** First repair the freshly merged bootstrap baseline so a new worktree can run Release verification cleanly. Then extend the control contract with story, session, patch, action, log, and target messages; implement aligned C++ and .NET core libraries against shared vectors; and layer thin SDKs over those libraries with C++ descriptors plus a .NET source-generated registry and a narrowly scoped reflection fallback.

**Tech Stack:** C++20, MSVC 19.40 or newer, CMake 3.30 or newer, vcpkg manifest mode, nlohmann-json 3.12.0, Catch2 3.15.2, .NET 10 LTS, System.Text.Json, JsonSchema.Net 9.2.2, xUnit.net v3 3.2.2, Roslyn incremental generators, PowerShell 7, Doxygen 1.17.0, and GitHub Actions on `windows-2022`.

## Global Constraints

- Windows is the only implementation platform in this plan. Do not create or require WSL, WSL2, Docker, or a Unix shell.
- The repository remains public under the MIT License.
- This plan does not add host rendering, frame transport, builder orchestration, hot reload, or native catalog UI; those remain in later delivery plans.
- DearStory stays Dear ImGui-first and language-neutral. Story code calls `ImGui::` or its binding API directly.
- SDKs remain thin. They may expose lifecycle, metadata, arguments, actions, targets, deterministic services, and registration, but they may not mirror or wrap the Dear ImGui widget API.
- JSON Schema Draft 2020-12 remains the canonical argument contract, limited to the documented DearStory subset plus `x-dearstory-*` annotations.
- Documentation uses CommonMark/GFM and typed Doc Blocks only. Do not introduce executable MDX or arbitrary JavaScript.
- Public C++ APIs require Doxygen comments. Public C# APIs require XML documentation. Missing public documentation is a build failure.
- C++ and C# compile with warnings as errors. C# nullable analysis stays enabled.
- Core and schema code must meet at least 80 percent line coverage and 70 percent branch coverage.
- Shared behavior is proven through checked-in vectors and unchanged tests in both languages.
- Runtime reflection in .NET is opt-in and limited to explicitly documented scenarios. The default path is compile-time generation.
- The merged `main` branch currently has a verified Release baseline issue: protocol E2E probe resolution is hardcoded to `Debug` output paths. Task 1 repairs that before any story-model work begins.
- Every implementation task follows red-green-refactor TDD and ends in a focused commit.

---

## File structure locked by this plan

```text
docs/adr/0002-story-model-and-schema-contracts.md        architectural decision for story/session/schema contracts
docs/architecture/core-story-model.md                    architecture record for IDs, catalogs, sessions, schemas, and SDK layering
docs/guides/authoring-stories.md                         task-oriented guide for C++ and .NET story authors
docs/protocol/argument-schema-subset.md                  normative JSON Schema subset and patch rules
docs/protocol/control-v1.md                              extended control contract documentation
protocol/control/messages.json                           generated-model manifest extended with story/session messages
protocol/test-vectors/stories/*.json                     shared positive and negative story/session/schema vectors
schemas/arguments/dearstory-args.schema.json             machine-readable DearStory JSON Schema subset
src/core/cpp/CMakeLists.txt                              native core target
src/core/cpp/include/dearstory/core/*.hpp                public native story/core contracts
src/core/cpp/src/*.cpp                                   native catalog/session/schema implementation
src/core/dotnet/DearStory.Core/*                         managed story/core contracts and implementation
sdk/cpp/CMakeLists.txt                                   native SDK target
sdk/cpp/include/dearstory/sdk/*.hpp                      public thin native SDK
sdk/cpp/src/*.cpp                                        native SDK implementation
sdk/dotnet/DearStory.Sdk/*                               managed thin SDK surface
sdk/dotnet/DearStory.Sdk.Generator/*                     Roslyn source generator for registry/schema emission
tests/contract/core/DearStory.Core.ContractTests/*       shared vector contract tests for core and schemas
tests/contract/core/vectors/*.json                       canonical vectors for IDs, merge, patches, targets, and logs
tests/unit/core/cpp/*                                    native core unit tests
tests/unit/core/dotnet/DearStory.Core.Tests/*            managed core unit tests
tests/unit/sdk/cpp/*                                     native SDK unit tests
tests/unit/sdk/dotnet/DearStory.Sdk.Tests/*              managed SDK surface and reflection-fallback tests
tests/unit/sdk/dotnet/DearStory.Sdk.Generator.Tests/*    incremental generator tests
tests/e2e/protocol/DearStory.Protocol.E2ETests/*         Release-aware protocol probe resolution repair
```

## Delivery map inside plan 2

1. Repair the merged Release baseline so fresh worktrees can verify cleanly.
2. Extend the control manifest and documentation with story/session/schema messages.
3. Implement managed core story identity, catalog merge, sessions, deterministic services, and event/target models.
4. Implement matching native core story identity, catalog merge, sessions, deterministic services, and event/target models.
5. Define the DearStory JSON Schema subset and validated patch behavior in both languages against shared vectors.
6. Build the thin C++ SDK surface over native core contracts.
7. Build the thin .NET SDK surface with source generation and limited reflection fallback.
8. Lock the slice with docs, shared vectors, CI, and coverage gates so the hosts/frame-transport plan can build on stable interfaces.

## Scope guard

This plan intentionally stops before:

- D3D11 or RGBA frame transport;
- a running host process that renders stories;
- a visible native catalog user interface;
- builder/watch/restart orchestration;
- static documentation build output;
- interaction or visual testing against real rendered stories.

Those concerns belong to later delivery plans and may depend on the contracts emitted here, but they are not implemented here.

### Task 1: Repair Release-aware probe artifact resolution

**Files:**

- Modify: `eng/test.ps1`
- Create: `tests/e2e/protocol/DearStory.Protocol.E2ETests/ProbeArtifacts.cs`
- Create: `tests/e2e/protocol/DearStory.Protocol.E2ETests/ProbeArtifactsTests.cs`
- Modify: `tests/e2e/protocol/DearStory.Protocol.E2ETests/ProcessProbe.cs`

**Interfaces:**

- Consumes: `DEARSTORY_TEST_CONFIGURATION` process environment variable, existing protocol probe binaries.
- Produces: `ProbeArtifacts.CurrentConfiguration() -> string`, `ProbeArtifacts.ResolveNativeProbe() -> string`, and `ProbeArtifacts.ResolveManagedProbe() -> string`.

- [ ] **Step 1: Write the failing artifact-resolution test**

```csharp
namespace DearStory.Protocol.E2ETests;

public sealed class ProbeArtifactsTests
{
    [Fact]
    public void ResolveNativeProbe_UsesRequestedReleaseConfiguration()
    {
        Environment.SetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION", "Release");
        var path = ProbeArtifacts.ResolveNativeProbe();

        Assert.Contains(Path.Combine("artifacts", "bin", "native", "Release"), path, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveManagedProbe_UsesRequestedReleaseConfiguration()
    {
        Environment.SetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION", "Release");
        var path = ProbeArtifacts.ResolveManagedProbe();

        Assert.Contains(Path.Combine("tools", "DearStory.ProtocolProbe.DotNet", "bin", "Release", "net10.0"), path, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run the focused test and reproduce the root cause**

Run:

```powershell
dotnet test .\tests\e2e\protocol\DearStory.Protocol.E2ETests -c Release -m:1 --filter FullyQualifiedName~ProbeArtifacts
```

Expected: FAIL because `ProcessProbe` currently resolves `Debug` probe paths regardless of the active test configuration.

- [ ] **Step 3: Implement configuration-aware artifact lookup**

Create `tests/e2e/protocol/DearStory.Protocol.E2ETests/ProbeArtifacts.cs`:

```csharp
namespace DearStory.Protocol.E2ETests;

internal static class ProbeArtifacts
{
    internal static string CurrentConfiguration() =>
        string.Equals(Environment.GetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION"), "Release", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

    internal static string ResolveNativeProbe() =>
        Path.Combine(RepositoryRoot.Find(), "artifacts", "bin", "native", CurrentConfiguration(), "dearstory-protocol-probe-cpp.exe");

    internal static string ResolveManagedProbe() =>
        Path.Combine(RepositoryRoot.Find(), "tools", "DearStory.ProtocolProbe.DotNet", "bin", CurrentConfiguration(), "net10.0", "DearStory.ProtocolProbe.DotNet.exe");
}
```

Update `ProcessProbe.cs` to call `ProbeArtifacts.ResolveNativeProbe()` and `ProbeArtifacts.ResolveManagedProbe()` instead of embedding `Debug` paths. Update `eng/test.ps1` so the `dotnet test` invocation sets `DEARSTORY_TEST_CONFIGURATION` to the selected `$Configuration` before launching tests and removes it afterward.

- [ ] **Step 4: Re-run the clean Release baseline**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release
```

Expected: PASS. The previously failing protocol E2E tests now find the `Release` probe binaries and the worktree baseline is clean.

- [ ] **Step 5: Commit**

```powershell
git add eng/test.ps1 tests/e2e/protocol/DearStory.Protocol.E2ETests
git commit -m "test: make protocol e2e artifact lookup configuration-aware"
```

### Task 2: Extend the control contract for stories, sessions, patches, actions, logs, and targets

**Files:**

- Modify: `protocol/control/messages.json`
- Modify: `docs/protocol/control-v1.md`
- Create: `docs/adr/0002-story-model-and-schema-contracts.md`
- Create: `protocol/test-vectors/stories/story-index-published.valid.json`
- Create: `protocol/test-vectors/stories/story-session-open.valid.json`
- Create: `protocol/test-vectors/stories/argument-patch-rejected.invalid.json`
- Modify: `tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests/TestManifest.cs`
- Modify: `tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests/ModelEmitterTests.cs`

**Interfaces:**

- Consumes: the existing protocol generator and `hello`/`welcome`/`reject` bootstrap contract.
- Produces: generated types and wire messages `story_index_published`, `story_session_open`, `story_session_opened`, `story_session_reset`, `story_session_closed`, `argument_patch`, `argument_patch_result`, `action_emitted`, `log_emitted`, and `target_snapshot`.

- [ ] **Step 1: Write the failing generator-manifest test**

Add this case to `tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests/TestManifest.cs`:

```csharp
[Fact]
public void Manifest_contains_story_and_session_messages()
{
    var manifest = TestManifest.Load();

    Assert.Contains(manifest.Messages, message => message.Name == "story_index_published");
    Assert.Contains(manifest.Messages, message => message.Name == "story_session_open");
    Assert.Contains(manifest.Messages, message => message.Name == "argument_patch_result");
    Assert.Contains(manifest.Messages, message => message.Name == "target_snapshot");
}
```

- [ ] **Step 2: Run the focused generator tests**

Run:

```powershell
dotnet test .\tests\unit\protocol\dotnet\DearStory.ProtocolGenerator.Tests -c Release -m:1
```

Expected: FAIL because the manifest still exposes only the bootstrap handshake messages.

- [ ] **Step 3: Extend the manifest and ADR with exact wire shapes**

Add these message fragments to `protocol/control/messages.json`:

```json
{
  "name": "story_index_published",
  "fields": [
    { "name": "hostId", "type": "string", "required": true },
    { "name": "stories", "type": "story_descriptor[]", "required": true }
  ]
},
{
  "name": "argument_patch_result",
  "fields": [
    { "name": "sessionId", "type": "uuid", "required": true },
    { "name": "accepted", "type": "boolean", "required": true },
    { "name": "updatedArguments", "type": "json", "required": true },
    { "name": "diagnostics", "type": "field_diagnostic[]", "required": true }
  ]
}
```

Define supporting records for `story_descriptor`, `story_argument_schema`, `story_target`, `action_event`, `log_event`, `semantic_metadata`, and `field_diagnostic`. In `docs/adr/0002-story-model-and-schema-contracts.md`, document that story IDs are canonical language-neutral keys, that duplicate IDs are merge errors, and that patch validation is performed both by the catalog and by the host.

- [ ] **Step 4: Regenerate models and validate vectors**

Run:

```powershell
pwsh -NoProfile -File .\eng\generate-protocol.ps1
dotnet test .\tests\unit\protocol\dotnet\DearStory.ProtocolGenerator.Tests -c Release -m:1
```

Expected: PASS. Generated C++ and C# models include the new story/session/schema messages and the manifest tests see them.

- [ ] **Step 5: Commit**

```powershell
git add protocol/control/messages.json protocol/test-vectors/stories docs/protocol/control-v1.md docs/adr/0002-story-model-and-schema-contracts.md src/protocol/cpp/include/dearstory/protocol/generated/messages.hpp src/protocol/dotnet/DearStory.Protocol/Generated/Messages.g.cs tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests
git commit -m "feat: extend protocol with story and session contracts"
```

### Task 3: Implement managed core story identity, catalog merge, sessions, and deterministic services

**Files:**

- Create: `src/core/dotnet/DearStory.Core/DearStory.Core.csproj`
- Create: `src/core/dotnet/DearStory.Core/StoryId.cs`
- Create: `src/core/dotnet/DearStory.Core/StoryDescriptor.cs`
- Create: `src/core/dotnet/DearStory.Core/Catalog/StoryCatalog.cs`
- Create: `src/core/dotnet/DearStory.Core/Catalog/CatalogMergeResult.cs`
- Create: `src/core/dotnet/DearStory.Core/Sessions/StorySession.cs`
- Create: `src/core/dotnet/DearStory.Core/Services/DeterministicClock.cs`
- Create: `src/core/dotnet/DearStory.Core/Services/DeterministicRandom.cs`
- Create: `src/core/dotnet/DearStory.Core/Events/ActionEvent.cs`
- Create: `src/core/dotnet/DearStory.Core/Events/LogEvent.cs`
- Create: `src/core/dotnet/DearStory.Core/Targets/InteractionTarget.cs`
- Create: `tests/unit/core/dotnet/DearStory.Core.Tests/DearStory.Core.Tests.csproj`
- Create: `tests/unit/core/dotnet/DearStory.Core.Tests/StoryIdTests.cs`
- Create: `tests/unit/core/dotnet/DearStory.Core.Tests/CatalogMergeTests.cs`
- Create: `tests/unit/core/dotnet/DearStory.Core.Tests/StorySessionTests.cs`
- Create: `tests/unit/core/dotnet/DearStory.Core.Tests/DeterministicServiceTests.cs`
- Modify: `DearStory.slnx`

**Interfaces:**

- Consumes: generated protocol models from Task 2.
- Produces: `StoryId.Parse(string)`, `StoryCatalog.Merge(string hostId, IReadOnlyList<StoryDescriptor>)`, `StorySession.Open(...)`, `StorySession.Reset(...)`, `StorySession.Close()`, `DeterministicClock.Advance(TimeSpan)`, and `DeterministicRandom.NextUInt32()`.

- [ ] **Step 1: Write failing managed core tests**

```csharp
public sealed class StoryIdTests
{
    [Theory]
    [InlineData("Buttons/Primary", "buttons/primary")]
    [InlineData(" buttons/Primary ", "buttons/primary")]
    public void Parse_canonicalizes_story_ids(string raw, string expected)
        => Assert.Equal(expected, StoryId.Parse(raw).Value);
}

public sealed class CatalogMergeTests
{
    [Fact]
    public void Merge_rejects_duplicate_canonical_ids_from_different_hosts()
    {
        var catalog = new StoryCatalog();
        catalog.Merge("cpp-host", [StoryDescriptor.Create("buttons/primary", "Buttons/Primary")]);

        var result = catalog.Merge("dotnet-host", [StoryDescriptor.Create("Buttons/Primary", "Buttons/Primary")]);

        Assert.False(result.Succeeded);
        Assert.Single(result.Diagnostics);
        Assert.Equal("story.duplicate_id", result.Diagnostics[0].Code);
    }
}
```

- [ ] **Step 2: Run the focused managed core tests and verify red**

Run:

```powershell
dotnet test .\tests\unit\core\dotnet\DearStory.Core.Tests -c Release -m:1
```

Expected: FAIL because `DearStory.Core` and its types do not exist yet.

- [ ] **Step 3: Implement the managed core surface**

Create `src/core/dotnet/DearStory.Core/StoryId.cs`:

```csharp
namespace DearStory.Core;

public readonly record struct StoryId(string Value)
{
    public static StoryId Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var normalized = raw.Trim().Replace('\\', '/').ToLowerInvariant();
        return new StoryId(string.Join('/', normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)));
    }
}
```

Create `src/core/dotnet/DearStory.Core/Sessions/StorySession.cs`:

```csharp
namespace DearStory.Core.Sessions;

public sealed class StorySession
{
    public required Guid SessionId { get; init; }
    public required StoryId StoryId { get; init; }
    public required JsonNode Arguments { get; init; }
    public required DeterministicClock Clock { get; init; }
    public required DeterministicRandom Random { get; init; }

    public void Reset(JsonNode defaultArguments, long seed, DateTimeOffset startTimeUtc)
    {
        Arguments = defaultArguments.DeepClone();
        Clock.Reset(startTimeUtc);
        Random.Reset(seed);
    }
}
```

Implement `StoryCatalog` with an internal `Dictionary<StoryId, StoryDescriptor>` and a merge result that records diagnostics when the same canonical ID arrives from more than one host. `DeterministicClock` stores a current `DateTimeOffset` and advances only when explicitly told to. `DeterministicRandom` wraps a fixed-seed `Random` replacement and exposes stable integer and float helpers.

- [ ] **Step 4: Run the managed core tests**

Run:

```powershell
dotnet test .\tests\unit\core\dotnet\DearStory.Core.Tests -c Release -m:1
```

Expected: PASS, including duplicate-ID rejection, reset semantics, and deterministic clock/random behavior.

- [ ] **Step 5: Commit**

```powershell
git add src/core/dotnet tests/unit/core/dotnet DearStory.slnx
git commit -m "feat: add managed core story catalog and sessions"
```

### Task 4: Implement native core story identity, catalog merge, sessions, and deterministic services

**Files:**

- Create: `src/core/cpp/CMakeLists.txt`
- Create: `src/core/cpp/include/dearstory/core/story_id.hpp`
- Create: `src/core/cpp/include/dearstory/core/story_descriptor.hpp`
- Create: `src/core/cpp/include/dearstory/core/story_catalog.hpp`
- Create: `src/core/cpp/include/dearstory/core/story_session.hpp`
- Create: `src/core/cpp/include/dearstory/core/action_event.hpp`
- Create: `src/core/cpp/include/dearstory/core/log_event.hpp`
- Create: `src/core/cpp/include/dearstory/core/interaction_target.hpp`
- Create: `src/core/cpp/include/dearstory/core/deterministic_clock.hpp`
- Create: `src/core/cpp/include/dearstory/core/deterministic_random.hpp`
- Create: `src/core/cpp/src/story_id.cpp`
- Create: `src/core/cpp/src/story_catalog.cpp`
- Create: `src/core/cpp/src/story_session.cpp`
- Create: `tests/unit/core/cpp/CMakeLists.txt`
- Create: `tests/unit/core/cpp/story_id_tests.cpp`
- Create: `tests/unit/core/cpp/story_catalog_tests.cpp`
- Create: `tests/unit/core/cpp/story_session_tests.cpp`
- Modify: `CMakeLists.txt`

**Interfaces:**

- Consumes: generated protocol records and the same story/session rules as managed core.
- Produces: `dearstory::core::story_id::parse(std::string_view)`, `story_catalog::merge(std::string_view host_id, std::vector<story_descriptor>)`, `story_session::reset(nlohmann::json const&, std::uint64_t, std::chrono::sys_time<std::chrono::milliseconds>)`, and `deterministic_random::next_uint32()`.

- [ ] **Step 1: Write failing native core tests**

```cpp
#include <dearstory/core/story_id.hpp>
#include <dearstory/core/story_catalog.hpp>
#include <catch2/catch_test_macros.hpp>

TEST_CASE("story_id canonicalizes case and slash separators")
{
    REQUIRE(dearstory::core::story_id::parse("Buttons\\Primary").value() == "buttons/primary");
}

TEST_CASE("story catalog rejects duplicate ids from different hosts")
{
    dearstory::core::story_catalog catalog;
    auto first = catalog.merge("cpp-host", { dearstory::core::story_descriptor::create("buttons/primary", "Buttons/Primary") });
    auto second = catalog.merge("dotnet-host", { dearstory::core::story_descriptor::create("Buttons/Primary", "Buttons/Primary") });

    REQUIRE(first.succeeded);
    REQUIRE_FALSE(second.succeeded);
    REQUIRE(second.diagnostics.front().code == "story.duplicate_id");
}
```

- [ ] **Step 2: Run the focused native core tests and verify red**

Run:

```powershell
cmake --build --preset windows-msvc-debug --config Release
ctest --test-dir .\build\windows-msvc-debug -C Release -R "story_(id|catalog|session)" --output-on-failure
```

Expected: FAIL because the native core target and tests do not exist yet.

- [ ] **Step 3: Implement the native core target**

Create `src/core/cpp/include/dearstory/core/story_id.hpp`:

```cpp
#pragma once

#include <expected>
#include <string>
#include <string_view>

namespace dearstory::core {

struct story_id final {
    std::string value;

    [[nodiscard]] static std::expected<story_id, std::string> parse(std::string_view raw);
    friend bool operator==(story_id const&, story_id const&) noexcept = default;
};

} // namespace dearstory::core
```

Create `src/core/cpp/include/dearstory/core/story_session.hpp`:

```cpp
#pragma once

#include <nlohmann/json.hpp>
#include <chrono>
#include <cstdint>

namespace dearstory::core {

class story_session final {
public:
    void reset(nlohmann::json const& default_arguments,
               std::uint64_t seed,
               std::chrono::sys_time<std::chrono::milliseconds> start_time);
};

} // namespace dearstory::core
```

Implement `story_id::parse` with trim, slash normalization, lowercase canonicalization, and empty-segment rejection. Implement `story_catalog` with duplicate detection across host IDs and a deterministic merged order sorted by canonical `story_id`.

- [ ] **Step 4: Run the native core tests**

Run:

```powershell
cmake --build --preset windows-msvc-debug --config Release
ctest --test-dir .\build\windows-msvc-debug -C Release -R "story_(id|catalog|session)" --output-on-failure
```

Expected: PASS, matching the managed behavior for canonicalization, merge diagnostics, and reset semantics.

- [ ] **Step 5: Commit**

```powershell
git add CMakeLists.txt src/core/cpp tests/unit/core/cpp
git commit -m "feat: add native core story catalog and sessions"
```

### Task 5: Define the DearStory JSON Schema subset and validated patch behavior

**Files:**

- Create: `schemas/arguments/dearstory-args.schema.json`
- Create: `docs/protocol/argument-schema-subset.md`
- Create: `src/core/dotnet/DearStory.Core/Schemas/ArgumentSchema.cs`
- Create: `src/core/dotnet/DearStory.Core/Schemas/ArgumentPatchValidator.cs`
- Create: `src/core/cpp/include/dearstory/core/argument_schema.hpp`
- Create: `src/core/cpp/include/dearstory/core/argument_patch_validator.hpp`
- Create: `src/core/cpp/src/argument_patch_validator.cpp`
- Create: `tests/contract/core/DearStory.Core.ContractTests/DearStory.Core.ContractTests.csproj`
- Create: `tests/contract/core/DearStory.Core.ContractTests/SchemaVectorTests.cs`
- Create: `tests/contract/core/vectors/patch-valid.string.json`
- Create: `tests/contract/core/vectors/patch-invalid.enum.json`
- Create: `tests/unit/core/cpp/argument_patch_validator_tests.cpp`
- Create: `tests/unit/core/dotnet/DearStory.Core.Tests/ArgumentPatchValidatorTests.cs`
- Modify: `DearStory.slnx`

**Interfaces:**

- Consumes: `story_argument_schema` records from Task 2 and core session state from Tasks 3 and 4.
- Produces: `.NET ArgumentPatchValidator.Apply(ArgumentSchema schema, JsonNode currentArguments, JsonNode patchDocument) -> PatchResult`, `C++ apply_patch(argument_schema const&, nlohmann::json const&, nlohmann::json const&) -> patch_result`, and machine-readable schema file `schemas/arguments/dearstory-args.schema.json`.

- [ ] **Step 1: Write failing schema and patch tests**

```csharp
[Fact]
public void Apply_rejects_unknown_enum_value()
{
    var schema = ArgumentSchema.Parse("""
    {
      "type": "object",
      "properties": {
        "size": {
          "type": "string",
          "enum": ["small", "medium", "large"],
          "x-dearstory-control": "radio"
        }
      }
    }
    """);

    var result = ArgumentPatchValidator.Apply(schema, JsonNode.Parse("""{"size":"medium"}""")!, JsonNode.Parse("""{"size":"giant"}""")!);

    Assert.False(result.Accepted);
    Assert.Equal("args.enum", result.Diagnostics.Single().Code);
}
```

```cpp
TEST_CASE("argument patch validator rejects an invalid enum value")
{
    auto schema = dearstory::core::argument_schema::parse(R"({
        "type":"object",
        "properties":{"size":{"type":"string","enum":["small","medium","large"]}}
    })").value();

    auto result = dearstory::core::apply_patch(schema, nlohmann::json::parse(R"({"size":"medium"})"), nlohmann::json::parse(R"({"size":"giant"})"));

    REQUIRE_FALSE(result.accepted);
    REQUIRE(result.diagnostics.front().code == "args.enum");
}
```

- [ ] **Step 2: Run focused managed, native, and contract tests**

Run:

```powershell
dotnet test .\tests\unit\core\dotnet\DearStory.Core.Tests -c Release -m:1 --filter FullyQualifiedName~ArgumentPatchValidator
dotnet test .\tests\contract\core\DearStory.Core.ContractTests -c Release -m:1
ctest --test-dir .\build\windows-msvc-debug -C Release -R "argument_patch_validator" --output-on-failure
```

Expected: FAIL because schema parsing and patch validation are not implemented yet.

- [ ] **Step 3: Implement the canonical subset and validators**

Define `schemas/arguments/dearstory-args.schema.json` with exact supported keywords:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://dearstory.dev/schemas/arguments/dearstory-args.schema.json",
  "type": ["object", "boolean", "integer", "number", "string", "array"],
  "properties": {
    "x-dearstory-control": { "type": "string" },
    "x-dearstory-order": { "type": "integer" },
    "x-dearstory-category": { "type": "string" },
    "x-dearstory-visible": { "type": "boolean" }
  }
}
```

Implement both validators to support `type`, `properties`, `required`, `enum`, `minimum`, `maximum`, `minLength`, `maxLength`, `items`, `default`, and the `x-dearstory-*` annotations above. Reject unsupported keywords with a stable `args.unsupported_keyword` diagnostic instead of silently ignoring them.

- [ ] **Step 4: Re-run all schema and patch tests**

Run:

```powershell
dotnet test .\tests\unit\core\dotnet\DearStory.Core.Tests -c Release -m:1 --filter FullyQualifiedName~ArgumentPatchValidator
dotnet test .\tests\contract\core\DearStory.Core.ContractTests -c Release -m:1
ctest --test-dir .\build\windows-msvc-debug -C Release -R "argument_patch_validator" --output-on-failure
```

Expected: PASS. Both languages accept the same valid patches, reject the same invalid patches, and emit the same diagnostic codes.

- [ ] **Step 5: Commit**

```powershell
git add schemas/arguments docs/protocol/argument-schema-subset.md src/core tests/contract/core DearStory.slnx
git commit -m "feat: add core schema subset and patch validation"
```

### Task 6: Build the thin C++ SDK surface

**Files:**

- Create: `sdk/cpp/CMakeLists.txt`
- Create: `sdk/cpp/include/dearstory/sdk/story_context.hpp`
- Create: `sdk/cpp/include/dearstory/sdk/story_registration.hpp`
- Create: `sdk/cpp/include/dearstory/sdk/argument_descriptor.hpp`
- Create: `sdk/cpp/include/dearstory/sdk/story_registry.hpp`
- Create: `sdk/cpp/include/dearstory/sdk/macros.hpp`
- Create: `sdk/cpp/src/story_registry.cpp`
- Create: `tests/unit/sdk/cpp/CMakeLists.txt`
- Create: `tests/unit/sdk/cpp/story_registry_tests.cpp`
- Create: `tests/unit/sdk/cpp/argument_descriptor_tests.cpp`
- Modify: `CMakeLists.txt`

**Interfaces:**

- Consumes: native core types from Tasks 4 and 5.
- Produces: `DEARSTORY_STORY(id, fn, args_type)`, `story_registry::add(story_registration)`, `story_context::args()`, `story_context::actions()`, `story_context::logs()`, `story_context::targets()`, `story_context::clock()`, and `story_context::random()`.

- [ ] **Step 1: Write the failing native SDK registration test**

```cpp
#include <dearstory/sdk/macros.hpp>
#include <dearstory/sdk/story_registry.hpp>
#include <catch2/catch_test_macros.hpp>

struct primary_button_args final {
    std::string label{"Save"};
};

static void primary_button(dearstory::sdk::story_context&) {}

TEST_CASE("sdk story registration produces a canonical descriptor without wrapping ImGui")
{
    dearstory::sdk::story_registry registry;
    registry.add(DEARSTORY_STORY("Buttons/Primary", primary_button, primary_button_args));

    auto descriptors = registry.descriptors();
    REQUIRE(descriptors.size() == 1);
    REQUIRE(descriptors.front().id.value == "buttons/primary");
}
```

- [ ] **Step 2: Run the focused native SDK tests**

Run:

```powershell
cmake --build --preset windows-msvc-debug --config Release
ctest --test-dir .\build\windows-msvc-debug -C Release -R "sdk_(story_registry|argument_descriptor)" --output-on-failure
```

Expected: FAIL because the native SDK target and tests do not exist yet.

- [ ] **Step 3: Implement the thin native SDK**

Create `sdk/cpp/include/dearstory/sdk/story_context.hpp`:

```cpp
#pragma once

#include <dearstory/core/story_session.hpp>

namespace dearstory::sdk {

class story_context final {
public:
    explicit story_context(core::story_session& session) noexcept : session_(session) {}

    [[nodiscard]] auto& session() noexcept { return session_; }

private:
    core::story_session& session_;
};

} // namespace dearstory::sdk
```

Create `sdk/cpp/include/dearstory/sdk/macros.hpp`:

```cpp
#pragma once

#include <dearstory/sdk/story_registration.hpp>

#define DEARSTORY_STORY(ID, FUNCTION, ARGS_TYPE) \
    ::dearstory::sdk::story_registration::create(ID, FUNCTION, ::dearstory::sdk::describe_arguments<ARGS_TYPE>())
```

Keep the SDK thin: no wrapper widgets, no retained-mode abstractions, no rendering helpers. `describe_arguments<T>()` yields serializable schema/default metadata only.

- [ ] **Step 4: Re-run the native SDK tests**

Run:

```powershell
cmake --build --preset windows-msvc-debug --config Release
ctest --test-dir .\build\windows-msvc-debug -C Release -R "sdk_(story_registry|argument_descriptor)" --output-on-failure
```

Expected: PASS. Story registration yields canonical descriptors and argument metadata without owning or abstracting any Dear ImGui calls.

- [ ] **Step 5: Commit**

```powershell
git add CMakeLists.txt sdk/cpp tests/unit/sdk/cpp
git commit -m "feat: add thin native story sdk"
```

### Task 7: Build the thin .NET SDK with source generation and limited reflection fallback

**Files:**

- Create: `sdk/dotnet/DearStory.Sdk/DearStory.Sdk.csproj`
- Create: `sdk/dotnet/DearStory.Sdk/StoryAttribute.cs`
- Create: `sdk/dotnet/DearStory.Sdk/StoryArgAttribute.cs`
- Create: `sdk/dotnet/DearStory.Sdk/StoryContext.cs`
- Create: `sdk/dotnet/DearStory.Sdk/GeneratedStoryRegistry.cs`
- Create: `sdk/dotnet/DearStory.Sdk/ReflectionStoryRegistry.cs`
- Create: `sdk/dotnet/DearStory.Sdk/ReflectionStoryRegistryOptions.cs`
- Create: `sdk/dotnet/DearStory.Sdk.Generator/DearStory.Sdk.Generator.csproj`
- Create: `sdk/dotnet/DearStory.Sdk.Generator/StoryRegistryGenerator.cs`
- Create: `sdk/dotnet/DearStory.Sdk.Generator/XmlDocumentationReader.cs`
- Create: `tests/unit/sdk/dotnet/DearStory.Sdk.Tests/DearStory.Sdk.Tests.csproj`
- Create: `tests/unit/sdk/dotnet/DearStory.Sdk.Tests/ReflectionStoryRegistryTests.cs`
- Create: `tests/unit/sdk/dotnet/DearStory.Sdk.Generator.Tests/DearStory.Sdk.Generator.Tests.csproj`
- Create: `tests/unit/sdk/dotnet/DearStory.Sdk.Generator.Tests/StoryRegistryGeneratorTests.cs`
- Modify: `DearStory.slnx`

**Interfaces:**

- Consumes: managed core types from Tasks 3 and 5.
- Produces: `[Story(string id, Type ArgsType)]`, `[StoryArg(string name)]`, `GeneratedStoryRegistry.Create()`, `ReflectionStoryRegistry.Create(Assembly assembly, ReflectionStoryRegistryOptions options)`, and generator output `GeneratedStoryRegistry.g.cs`.

- [ ] **Step 1: Write failing generator and reflection tests**

```csharp
using DearStory.Sdk;

namespace DearStory.Sdk.Generator.Tests;

public sealed class StoryRegistryGeneratorTests
{
    [Fact]
    public void Generator_emits_descriptor_with_xml_documentation_and_args()
    {
        const string source = """
        using DearStory.Sdk;

        /// <summary>Primary button story.</summary>
        public static class Stories
        {
            [Story("buttons/primary", typeof(PrimaryButtonArgs))]
            public static void PrimaryButton(StoryContext context) {}
        }

        public sealed class PrimaryButtonArgs
        {
            /// <summary>Caption shown on the button.</summary>
            [StoryArg("label")]
            public string Label { get; init; } = "Save";
        }
        """;

        var output = StoryRegistryGeneratorHarness.Run(source);

        Assert.Contains("buttons/primary", output, StringComparison.Ordinal);
        Assert.Contains("Caption shown on the button.", output, StringComparison.Ordinal);
    }
}
```

```csharp
public sealed class ReflectionStoryRegistryTests
{
    [Fact]
    public void Reflection_registry_requires_explicit_opt_in()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReflectionStoryRegistry.Create(typeof(ReflectionStoryRegistryTests).Assembly, new ReflectionStoryRegistryOptions { AllowReflectionFallback = false }));
    }
}
```

- [ ] **Step 2: Run the focused SDK tests**

Run:

```powershell
dotnet test .\tests\unit\sdk\dotnet\DearStory.Sdk.Generator.Tests -c Release -m:1
dotnet test .\tests\unit\sdk\dotnet\DearStory.Sdk.Tests -c Release -m:1
```

Expected: FAIL because the SDK and generator projects do not exist yet.

- [ ] **Step 3: Implement the managed SDK and generator**

Create `sdk/dotnet/DearStory.Sdk/StoryAttribute.cs`:

```csharp
namespace DearStory.Sdk;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class StoryAttribute(string id, Type argsType) : Attribute
{
    public string Id { get; } = id;
    public Type ArgsType { get; } = argsType;
}
```

Create `sdk/dotnet/DearStory.Sdk/ReflectionStoryRegistry.cs`:

```csharp
namespace DearStory.Sdk;

public static class ReflectionStoryRegistry
{
    public static GeneratedStoryRegistry Create(Assembly assembly, ReflectionStoryRegistryOptions options)
    {
        if (!options.AllowReflectionFallback)
        {
            throw new InvalidOperationException("Reflection fallback is disabled. Use the source-generated registry by default.");
        }

        return ReflectionStoryRegistryBuilder.Build(assembly);
    }
}
```

Implement the incremental generator so it:

- finds `[Story]` methods;
- reads XML docs for story and arg descriptions;
- emits canonical `StoryDescriptor` plus argument schema/default metadata;
- rejects duplicate canonical IDs at generation time with a build diagnostic;
- emits `GeneratedStoryRegistry.Create()` as the default discovery path.

- [ ] **Step 4: Re-run the managed SDK tests**

Run:

```powershell
dotnet test .\tests\unit\sdk\dotnet\DearStory.Sdk.Generator.Tests -c Release -m:1
dotnet test .\tests\unit\sdk\dotnet\DearStory.Sdk.Tests -c Release -m:1
```

Expected: PASS. The generator emits deterministic descriptors and the reflection fallback remains opt-in and explicitly limited.

- [ ] **Step 5: Commit**

```powershell
git add sdk/dotnet tests/unit/sdk/dotnet DearStory.slnx
git commit -m "feat: add managed story sdk and generator"
```

### Task 8: Lock the slice with shared vectors, docs, and CI

**Files:**

- Create: `docs/architecture/core-story-model.md`
- Create: `docs/guides/authoring-stories.md`
- Modify: `README.md`
- Modify: `.github/workflows/ci.yml`
- Modify: `eng/build.ps1`
- Modify: `eng/test.ps1`
- Modify: `CMakeLists.txt`
- Modify: `DearStory.slnx`
- Modify: `Doxyfile`
- Modify: `docs/standards/documentation-and-quality.md`
- Modify: `tests/unit/foundation/BuildScripts.Tests.ps1`
- Modify: `tests/unit/foundation/CoverageGate.Tests.ps1`

**Interfaces:**

- Consumes: Tasks 1 through 7.
- Produces: complete Release verification through `eng/build.ps1` and `eng/test.ps1`, updated architecture and authoring docs, and CI coverage/docs gates that include the new core and SDK modules.

- [ ] **Step 1: Write the failing verification-policy test**

Add this assertion to `tests/unit/foundation/BuildScripts.Tests.ps1`:

```powershell
$testScript = Get-Content -Raw "$PSScriptRoot\..\..\..\eng\test.ps1"

if ($testScript -notmatch 'tests\\unit\\core\\dotnet\\DearStory\.Core\.Tests') {
    throw 'eng/test.ps1 must run the managed core tests.'
}

if ($testScript -notmatch 'tests\\unit\\sdk\\dotnet\\DearStory\.Sdk\.Tests') {
    throw 'eng/test.ps1 must run the managed SDK tests.'
}
```

- [ ] **Step 2: Run the policy tests and verify red**

Run:

```powershell
pwsh -NoProfile -File .\tests\unit\foundation\BuildScripts.Tests.ps1
```

Expected: FAIL because the scripts and CI do not yet include the new core/SDK verification surface.

- [ ] **Step 3: Update docs and verification commands**

Extend `eng/build.ps1` and `eng/test.ps1` so they build and test:

- `src/core/cpp` and `tests/unit/core/cpp`;
- `sdk/cpp` and `tests/unit/sdk/cpp`;
- `src/core/dotnet/DearStory.Core`;
- `sdk/dotnet/DearStory.Sdk`;
- `sdk/dotnet/DearStory.Sdk.Generator`;
- `tests/unit/core/dotnet`;
- `tests/unit/sdk/dotnet`;
- `tests/contract/core`.

Update `.github/workflows/ci.yml` to run:

```powershell
pwsh -NoProfile -File .\eng\generate-protocol.ps1 -Check
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release -Coverage
doxygen .\Doxyfile
git diff --check
```

Document in `docs/guides/authoring-stories.md` exactly how C++ and .NET story authors declare IDs, args, actions, targets, and semantic metadata without using any DearStory-owned widget API.

- [ ] **Step 4: Run the full local verification**

Run:

```powershell
pwsh -NoProfile -File .\eng\generate-protocol.ps1 -Check
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release -Coverage
doxygen .\Doxyfile
git diff --check
git status --short
```

Expected: PASS. Coverage stays above 80 percent line and 70 percent branch for core/schema code, documentation generation has no warnings, generation is clean, and only the intended implementation files remain before the final commit.

- [ ] **Step 5: Commit**

```powershell
git add docs README.md .github/workflows/ci.yml eng CMakeLists.txt DearStory.slnx Doxyfile tests/unit/foundation
git commit -m "docs: codify core story model and schema slice"
```

## Plan acceptance checklist

- [ ] A fresh Windows worktree can build and test in `Release` without probe-path drift.
- [ ] The control manifest defines story/session/patch/action/log/target messages and generated models in both languages.
- [ ] Managed and native core libraries canonicalize story IDs the same way.
- [ ] Catalog merge rejects duplicate canonical story IDs with stable diagnostics naming both hosts.
- [ ] Session reset restores default arguments plus deterministic clock and random state.
- [ ] The DearStory JSON Schema subset is documented, machine-readable, and enforced in both languages.
- [ ] Valid and invalid patch vectors produce the same acceptance and diagnostic codes in C++ and .NET.
- [ ] The C++ SDK remains thin and never wraps the Dear ImGui widget API.
- [ ] The .NET SDK uses source generation by default and requires explicit opt-in for reflection fallback.
- [ ] Public C++ and C# APIs are fully documented and documentation generation stays warning-free.
- [ ] Release CI builds and tests the new core and SDK surfaces with coverage gates intact.

Completing this checklist authorizes the Hosts and RGBA Frame Transport implementation plan. It does not authorize catalog UI, builder/watch/restart logic, or static documentation output work inside this branch.

## Self-review

- **Spec coverage:** story IDs, merged catalog state, sessions, deterministic services, JSON Schema subset, validated patches, actions, logs, targets with semantic metadata, C++ descriptors, .NET source generation, limited reflection fallback, tests, and docs are all mapped to Tasks 2 through 8. The verified merged-baseline issue is covered by Task 1 because implementation on a red baseline would be unsound.
- **Placeholder scan:** no `TODO`, `TBD`, or “appropriate handling” placeholders remain. Every task includes exact files, commands, interfaces, and commit messages.
- **Type consistency:** `StoryId`, `StoryCatalog`, `StorySession`, `ArgumentPatchValidator`, `GeneratedStoryRegistry`, and `ReflectionStoryRegistry` are named consistently across tasks and downstream references.

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-16-dearstory-core-story-model-and-schemas.md`.

Two execution options:

1. **Subagent-Driven (recommended)** - execute task-by-task with separate agents and review gates between tasks.
2. **Inline Execution** - execute tasks in this session using the same branch and worktree.
