# DearStory Windows Hosts, RGBA Transport, Catalog, and Static Docs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the first Windows-first vertical slice that combines isolated C++ and .NET hosts, RGBA shared-memory frame transport, runner-driven build/watch/restart, a unified interactive catalog, and static HTML documentation generated from the same story and Markdown model.

**Architecture:** Build one managed Windows executable for the runner plus catalog shell, keep official C++ and .NET stories in separate host processes, reuse the language-neutral protocol/core contracts already landing in this branch, and add a second transport channel for RGBA frames over shared memory. The runner owns configuration, builders, supervision, watch/restart, and deterministic captures; the catalog consumes merged story metadata plus live frame descriptors; the documentation builder consumes the same document model and emits safe static HTML.

**Tech Stack:** C++20, MSVC 19.40 or newer, CMake 3.30 or newer, vcpkg manifest mode, Dear ImGui `v1.92.8` (`8936b58fe26e8c3da834b8f60b06511d537b4c63`), .NET 10 LTS, ImGui.NET `1.91.6.1`, System.Text.Json, MemoryMappedFile/Win32 file mappings, Tomlyn for `dearstory.toml`, Markdig for CommonMark parsing, JsonSchema.Net 9.2.2, xUnit.net v3 3.2.2, Catch2 3.15.2, PowerShell 7, Doxygen 1.17.0, and GitHub Actions on `windows-2022`.

## Global Constraints

- Windows is the only implementation platform in this plan. Do not create or require WSL, WSL2, Docker, or a Unix shell.
- The repository remains public under the MIT License.
- This plan builds directly on the protocol/bootstrap and core-story-model work already present in the open branch `feature/core-story-model-and-schemas`.
- DearStory stays Dear ImGui-first and language-neutral. Story code calls `ImGui::` or its binding API directly.
- The standalone Windows product is one runner-plus-catalog executable with separate language host processes.
- Control traffic stays on named pipes. Frame traffic uses shared memory carrying RGBA8 pixels with multiple slots. Do not transport `ImDrawData` or renderer-owned textures across process boundaries.
- Native Dear ImGui `v1.92.8` (`8936b58fe26e8c3da834b8f60b06511d537b4c63`) is the pinned native host baseline. ImGui.NET `1.91.6.1` is the pinned managed host and catalog baseline. Their identities are recorded in the handshake instead of treated as ABI-compatible.
- JSON Schema Draft 2020-12 remains the canonical argument contract, limited to the documented DearStory subset plus `x-dearstory-*` annotations.
- Documentation uses CommonMark/GFM and typed Doc Blocks only. Do not introduce executable MDX, raw HTML execution, or arbitrary JavaScript.
- Public C++ APIs require Doxygen comments. Public C# APIs require XML documentation. Missing public documentation is a build failure.
- C++ and C# compile with warnings as errors. C# nullable analysis stays enabled.
- CI visual capture uses D3D11 WARP. Pixel tolerance defaults to zero and may be relaxed only by a reviewed test-specific rule with a documented reason.
- Core, protocol, runner, transport, and host-control logic must keep at least 80 percent line coverage and 70 percent branch coverage where the repository already enforces gates; new modules must join the no-regression gate as part of this plan.
- Documentation, code comments, ADRs, diagrams, and task-oriented guides are release requirements for this vertical slice.
- Every implementation task follows red-green-refactor TDD and ends in a focused commit.

---

## File structure locked by this plan

```text
docs/adr/0003-windows-host-baseline.md                         baseline runner/catalog/host decision record
docs/architecture/windows-host-baseline.md                     subsystem map for runner, hosts, transports, catalog, and docs
docs/guides/building-windows.md                                updated build and verification guide
docs/guides/authoring-stories.md                               updated host/story authoring guidance
docs/guides/windows-dev-workflow.md                            `dearstory dev` workflow guide
docs/guides/static-docs.md                                     `dearstory build` output guide
docs/protocol/control-v1.md                                    control message additions for frames, input, heartbeats, and faults
docs/protocol/frame-transport-v1.md                            normative shared-memory RGBA frame transport contract
docs/standards/documentation-and-quality.md                    expanded coverage/documentation scope for new modules
examples/workspaces/windows-slice/dearstory.toml               canonical mixed-language workspace
examples/workspaces/windows-slice/cpp/*                        C++ example story source
examples/workspaces/windows-slice/dotnet/*                     .NET example story source
examples/workspaces/windows-slice/docs/*                       Markdown docs for the example workspace
src/runner/dotnet/DearStory.Runner/*                           CLI, config loading, supervision, watch/restart, capture orchestration
src/catalog/dotnet/DearStory.Catalog/*                         ImGui.NET catalog UI, preview, controls, diagnostics
src/docs/dotnet/DearStory.Docs/*                               safe Markdown/Doc Block model and static HTML builder
src/transports/dotnet/DearStory.Transport.Windows/*            managed named-pipe and shared-memory Windows transport helpers
src/transports/cpp/*                                           native shared-memory frame transport helpers
src/hosts/cpp/*                                                native host executable and runtime
src/hosts/dotnet/DearStory.Host/*                              managed host executable and runtime
tools/DearStory.CaptureWorker/*                                headless deterministic screenshot entrypoint used by `dearstory build`
tests/unit/runner/dotnet/DearStory.Runner.Tests/*              config, supervision, and restart policy tests
tests/unit/catalog/dotnet/DearStory.Catalog.Tests/*            catalog tree, controls, preview, and diagnostics tests
tests/unit/docs/dotnet/DearStory.Docs.Tests/*                  Markdown, Doc Block, autodocs, and static-output tests
tests/unit/transports/dotnet/DearStory.Transport.Windows.Tests/* managed shared-memory transport tests
tests/unit/transports/cpp/*                                    native shared-memory transport tests
tests/conformance/hosts/DearStory.HostConformance.Tests/*      unchanged protocol/host contract tests for both official hosts
tests/integration/windows/DearStory.WindowsSlice.Tests/*       runner + builders + hosts + catalog integration tests
tests/e2e/windows/DearStory.WindowsSlice.E2ETests/*            `dearstory dev` and `dearstory build` end-to-end verification
tests/visual/windows/*                                         deterministic baseline, actual, and diff artifacts for the first slice
DearStory.slnx                                                 add runner/catalog/docs/transports/hosts managed projects and tests
CMakeLists.txt                                                 add native transport/host targets and native tests
Directory.Packages.props                                       add ImGui.NET, Tomlyn, and Markdig package versions
eng/build.ps1                                                  build runner/catalog/docs/hosts projects
eng/test.ps1                                                   run new unit/conformance/integration/e2e suites and coverage
.github/workflows/ci.yml                                       run Windows vertical-slice verification and publish artifacts
```

## Delivery map inside this baseline plan

1. Extend the protocol and repository scaffolding so frame transport, host faults, and Windows-slice projects have stable contracts.
2. Implement the shared-memory RGBA transport on both managed and native sides with deterministic tests.
3. Build the Windows runner core: configuration, CLI, supervision, and structured diagnostics.
4. Deliver the native C++ host against pinned Dear ImGui with one example story and frame publication.
5. Deliver the managed .NET host against pinned ImGui.NET with one example story and frame publication.
6. Build the unified catalog UI: merged story tree, preview, schema controls, logs, actions, and host-health diagnostics.
7. Add builders, file watching, rebuild, restart, and preserved catalog/session state for the active workspace.
8. Add the safe Markdown/Doc Block pipeline, autodocs, static HTML output, deterministic screenshots, and final CI/e2e hardening.

## Scope guard

This plan intentionally stops before:

- Linux or macOS runners, transports, rendering, or packaging;
- D3D11 shared-texture transport;
- browser or WebAssembly story execution;
- public extension/marketplace APIs;
- Rust, Python, or other additional hosts;
- full interaction authoring UX or large visual-baseline management beyond the deterministic smoke coverage needed for this slice;
- embedded/in-process runner mode.

### Task 1: Extend the control contract and scaffold the Windows vertical-slice projects

**Files:**

- Modify: `protocol/control/messages.json`
- Modify: `docs/protocol/control-v1.md`
- Create: `docs/protocol/frame-transport-v1.md`
- Create: `docs/adr/0003-windows-host-baseline.md`
- Create: `docs/architecture/windows-host-baseline.md`
- Modify: `Directory.Packages.props`
- Modify: `DearStory.slnx`
- Modify: `CMakeLists.txt`
- Modify: `tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests/TestManifest.cs`
- Modify: `tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests/ModelEmitterTests.cs`

**Interfaces:**

- Consumes: the generated control manifest already used by protocol C++ and .NET code generation.
- Produces: generated message records for `frame_channel_ready`, `frame_presented`, `capture_requested`, `capture_completed`, `input_batch`, `viewport_changed`, `heartbeat`, and `host_faulted`.

- [ ] **Step 1: Write the failing manifest test for frame and supervision messages**

Add this test to `tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests/TestManifest.cs`:

```csharp
[Fact]
public void Manifest_contains_frame_and_supervision_messages()
{
    var manifest = TestManifest.Load();

    Assert.Contains(manifest.Messages, message => message.Name == "frame_channel_ready");
    Assert.Contains(manifest.Messages, message => message.Name == "frame_presented");
    Assert.Contains(manifest.Messages, message => message.Name == "capture_requested");
    Assert.Contains(manifest.Messages, message => message.Name == "host_faulted");
    Assert.Contains(manifest.Messages, message => message.Name == "heartbeat");
}
```

- [ ] **Step 2: Run the focused generator tests**

Run:

```powershell
dotnet test .\tests\unit\protocol\dotnet\DearStory.ProtocolGenerator.Tests\DearStory.ProtocolGenerator.Tests.csproj -c Release -m:1
```

Expected: FAIL because the current manifest does not publish the frame-channel and host-supervision messages.

- [ ] **Step 3: Extend the manifest, pin the new packages, and register the new projects**

Add these records to `protocol/control/messages.json`:

```json
{
  "name": "frame_channel_ready",
  "fields": [
    { "name": "sessionId", "type": "uuid", "required": true },
    { "name": "mappingName", "type": "string", "required": true },
    { "name": "slotCount", "type": "int32", "required": true },
    { "name": "pixelFormat", "type": "string", "required": true },
    { "name": "width", "type": "int32", "required": true },
    { "name": "height", "type": "int32", "required": true },
    { "name": "stride", "type": "int32", "required": true }
  ]
},
{
  "name": "frame_presented",
  "fields": [
    { "name": "sessionId", "type": "uuid", "required": true },
    { "name": "slotIndex", "type": "int32", "required": true },
    { "name": "sequence", "type": "int64", "required": true },
    { "name": "timestampUtc", "type": "timestamp", "required": true }
  ]
}
```

In `Directory.Packages.props`, add `PackageVersion` entries for `ImGui.NET`, `Tomlyn`, and `Markdig`. In `DearStory.slnx`, add projects under `/src/runner/dotnet/`, `/src/catalog/dotnet/`, `/src/docs/dotnet/`, `/src/transports/dotnet/`, `/src/hosts/dotnet/`, and the matching new test folders. In `CMakeLists.txt`, add `add_subdirectory(src/transports/cpp)` and `add_subdirectory(src/hosts/cpp)`.

- [ ] **Step 4: Regenerate the protocol and re-run the generator tests**

Run:

```powershell
pwsh -NoProfile -File .\eng\generate-protocol.ps1
dotnet test .\tests\unit\protocol\dotnet\DearStory.ProtocolGenerator.Tests\DearStory.ProtocolGenerator.Tests.csproj -c Release -m:1
```

Expected: PASS. The generated protocol model now contains the frame and supervision messages and the new projects are represented in the solution/build graph.

- [ ] **Step 5: Commit**

```powershell
git add protocol/control/messages.json docs/protocol/control-v1.md docs/protocol/frame-transport-v1.md docs/adr/0003-windows-host-baseline.md docs/architecture/windows-host-baseline.md Directory.Packages.props DearStory.slnx CMakeLists.txt tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests
git commit -m "feat: add windows host and frame transport contracts"
```

### Task 2: Implement the Windows shared-memory RGBA frame transport

**Files:**

- Create: `src/transports/dotnet/DearStory.Transport.Windows/DearStory.Transport.Windows.csproj`
- Create: `src/transports/dotnet/DearStory.Transport.Windows/FrameTransportDescriptor.cs`
- Create: `src/transports/dotnet/DearStory.Transport.Windows/SharedMemoryFrameWriter.cs`
- Create: `src/transports/dotnet/DearStory.Transport.Windows/SharedMemoryFrameReader.cs`
- Create: `src/transports/dotnet/DearStory.Transport.Windows/FrameSlotLease.cs`
- Create: `src/transports/cpp/CMakeLists.txt`
- Create: `src/transports/cpp/include/dearstory/transports/windows/shared_memory_frame_channel.hpp`
- Create: `src/transports/cpp/src/shared_memory_frame_channel.cpp`
- Create: `tests/unit/transports/dotnet/DearStory.Transport.Windows.Tests/DearStory.Transport.Windows.Tests.csproj`
- Create: `tests/unit/transports/dotnet/DearStory.Transport.Windows.Tests/SharedMemoryFrameChannelTests.cs`
- Create: `tests/unit/transports/cpp/CMakeLists.txt`
- Create: `tests/unit/transports/cpp/shared_memory_frame_channel_tests.cpp`

**Interfaces:**

- Consumes: `frame_channel_ready` and `frame_presented` protocol messages.
- Produces: `FrameTransportDescriptor`, `SharedMemoryFrameWriter.Publish(ReadOnlySpan<byte> rgbaBytes)`, `SharedMemoryFrameReader.TryReadLatest(out FrameSlotLease frame)`, and `dearstory::transports::windows::shared_memory_frame_channel`.

- [ ] **Step 1: Write the failing reader/writer tests**

Create `tests/unit/transports/dotnet/DearStory.Transport.Windows.Tests/SharedMemoryFrameChannelTests.cs`:

```csharp
public sealed class SharedMemoryFrameChannelTests
{
    [Fact]
    public void Publish_then_read_latest_returns_written_rgba_frame()
    {
        var descriptor = FrameTransportDescriptor.Create("Local\\dearstory-frame-test", width: 2, height: 2, stride: 8, slotCount: 3);
        using var writer = new SharedMemoryFrameWriter(descriptor);
        using var reader = new SharedMemoryFrameReader(descriptor);

        writer.Publish(new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255 });

        Assert.True(reader.TryReadLatest(out var frame));
        Assert.Equal(1L, frame.Sequence);
        Assert.Equal(16, frame.Bytes.Length);
    }
}
```

Create `tests/unit/transports/cpp/shared_memory_frame_channel_tests.cpp`:

```cpp
TEST_CASE("shared memory frame channel publishes monotonic sequence")
{
    auto descriptor = dearstory::transports::windows::frame_transport_descriptor::create(L"Local\\dearstory-frame-test-cpp", 2, 2, 8, 3);
    dearstory::transports::windows::shared_memory_frame_channel channel(descriptor);

    std::array<std::byte, 16> pixels{};
    auto first = channel.publish(pixels);
    auto second = channel.publish(pixels);

    REQUIRE(first.sequence == 1);
    REQUIRE(second.sequence == 2);
}
```

- [ ] **Step 2: Run the focused transport tests**

Run:

```powershell
dotnet test .\tests\unit\transports\dotnet\DearStory.Transport.Windows.Tests\DearStory.Transport.Windows.Tests.csproj -c Release -m:1
ctest --test-dir .\build\windows-msvc-debug -C Release -R "shared_memory_frame_channel" --output-on-failure
```

Expected: FAIL because the transport projects and implementations do not exist yet.

- [ ] **Step 3: Implement the shared-memory transport on managed and native sides**

Create `src/transports/dotnet/DearStory.Transport.Windows/SharedMemoryFrameWriter.cs`:

```csharp
public sealed class SharedMemoryFrameWriter : IDisposable
{
    private readonly FrameTransportDescriptor _descriptor;
    private readonly MemoryMappedFile _mapping;
    private long _sequence;

    public SharedMemoryFrameWriter(FrameTransportDescriptor descriptor)
    {
        _descriptor = descriptor;
        _mapping = MemoryMappedFile.CreateOrOpen(descriptor.MappingName, descriptor.TotalByteLength);
    }

    public FrameSlotLease Publish(ReadOnlySpan<byte> rgbaBytes)
    {
        var slotIndex = (int)(Interlocked.Increment(ref _sequence) - 1) % _descriptor.SlotCount;
        using var accessor = _mapping.CreateViewAccessor(slotIndex * _descriptor.FrameByteLength, _descriptor.FrameByteLength);
        accessor.WriteArray(0, rgbaBytes.ToArray(), 0, rgbaBytes.Length);
        return new FrameSlotLease(slotIndex, _sequence, rgbaBytes.Length);
    }
}
```

Create `src/transports/cpp/include/dearstory/transports/windows/shared_memory_frame_channel.hpp`:

```cpp
namespace dearstory::transports::windows
{
    struct frame_transport_descriptor
    {
        std::wstring mapping_name;
        std::int32_t width;
        std::int32_t height;
        std::int32_t stride;
        std::int32_t slot_count;
    };

    struct published_frame
    {
        std::int32_t slot_index;
        std::int64_t sequence;
        std::chrono::sys_time<std::chrono::milliseconds> timestamp_utc;
    };

    class shared_memory_frame_channel final
    {
    public:
        explicit shared_memory_frame_channel(frame_transport_descriptor descriptor);
        [[nodiscard]] published_frame publish(std::span<std::byte const> rgba_bytes);
    };
}
```

- [ ] **Step 4: Re-run the focused transport tests**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
dotnet test .\tests\unit\transports\dotnet\DearStory.Transport.Windows.Tests\DearStory.Transport.Windows.Tests.csproj -c Release -m:1
ctest --test-dir .\build\windows-msvc-debug -C Release -R "shared_memory_frame_channel" --output-on-failure
```

Expected: PASS. Managed and native transports agree on slot layout, frame byte size, and monotonic sequence behavior.

- [ ] **Step 5: Commit**

```powershell
git add src/transports tests/unit/transports DearStory.slnx CMakeLists.txt
git commit -m "feat: add windows shared memory frame transport"
```

### Task 3: Build the Windows runner core with configuration, CLI, and supervision

**Files:**

- Create: `src/runner/dotnet/DearStory.Runner/DearStory.Runner.csproj`
- Create: `src/runner/dotnet/DearStory.Runner/Program.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Commands/DevCommand.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Commands/BuildCommand.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Configuration/WorkspaceConfiguration.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Configuration/WorkspaceConfigurationLoader.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Supervision/HostLaunchDescriptor.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Supervision/HostSupervisor.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Diagnostics/StructuredDiagnostic.cs`
- Create: `tests/unit/runner/dotnet/DearStory.Runner.Tests/DearStory.Runner.Tests.csproj`
- Create: `tests/unit/runner/dotnet/DearStory.Runner.Tests/WorkspaceConfigurationLoaderTests.cs`
- Create: `tests/unit/runner/dotnet/DearStory.Runner.Tests/HostSupervisorTests.cs`
- Create: `examples/workspaces/windows-slice/dearstory.toml`

**Interfaces:**

- Consumes: the existing protocol/core libraries and the new transport descriptors.
- Produces: `WorkspaceConfigurationLoader.Load(string startDirectory)`, `HostSupervisor.StartAsync(HostLaunchDescriptor descriptor, CancellationToken cancellationToken)`, `RunnerExitCode`, and public `dearstory dev`/`dearstory build` command entrypoints.

- [ ] **Step 1: Write the failing configuration and supervision tests**

Create `tests/unit/runner/dotnet/DearStory.Runner.Tests/WorkspaceConfigurationLoaderTests.cs`:

```csharp
public sealed class WorkspaceConfigurationLoaderTests
{
    [Fact]
    public void Load_finds_dearstory_toml_and_binds_hosts()
    {
        var config = WorkspaceConfigurationLoader.LoadFromText("""
            [workspace]
            name = "windows-slice"

            [[hosts]]
            id = "cpp-host"
            builder = "cmake"

            [[hosts]]
            id = "dotnet-host"
            builder = "dotnet"
            """);

        Assert.Equal("windows-slice", config.Workspace.Name);
        Assert.Collection(config.Hosts, host => Assert.Equal("cpp-host", host.Id), host => Assert.Equal("dotnet-host", host.Id));
    }
}
```

Create `tests/unit/runner/dotnet/DearStory.Runner.Tests/HostSupervisorTests.cs`:

```csharp
public sealed class HostSupervisorTests
{
    [Fact]
    public async Task Restart_policy_stops_after_bounded_retries()
    {
        var supervisor = new HostSupervisor(maxRestartAttempts: 3);
        var launch = HostLaunchDescriptor.Failing("cpp-host");

        var result = await supervisor.RunUntilTerminalAsync(launch, CancellationToken.None);

        Assert.Equal(3, result.RestartAttempts);
        Assert.Equal(HostTerminalState.Faulted, result.State);
    }
}
```

- [ ] **Step 2: Run the focused runner tests**

Run:

```powershell
dotnet test .\tests\unit\runner\dotnet\DearStory.Runner.Tests\DearStory.Runner.Tests.csproj -c Release -m:1
```

Expected: FAIL because the runner project, TOML loading, and supervision types do not exist yet.

- [ ] **Step 3: Implement the managed runner core and example workspace file**

Create `src/runner/dotnet/DearStory.Runner/Configuration/WorkspaceConfigurationLoader.cs`:

```csharp
public static class WorkspaceConfigurationLoader
{
    public static WorkspaceConfiguration LoadFromText(string tomlText)
    {
        var model = Tomlyn.Toml.ToModel(tomlText);
        return WorkspaceConfigurationBinding.Bind(model);
    }
}
```

Create `examples/workspaces/windows-slice/dearstory.toml`:

```toml
[workspace]
name = "windows-slice"

[catalog]
theme = "dark"

[[hosts]]
id = "cpp-host"
builder = "cmake"
project = "cpp"

[[hosts]]
id = "dotnet-host"
builder = "dotnet"
project = "dotnet"

[[docs]]
glob = "docs/**/*.md"
```

Implement `Program.cs` so `dearstory dev <workspacePath>` loads config, builds launch descriptors, starts supervision, and returns stable nonzero exit codes for configuration, build, protocol, and host-launch failures.

- [ ] **Step 4: Re-run the focused runner tests**

Run:

```powershell
dotnet test .\tests\unit\runner\dotnet\DearStory.Runner.Tests\DearStory.Runner.Tests.csproj -c Release -m:1
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- --help
```

Expected: PASS. The runner resolves the workspace file, exposes `dev` and `build`, and enforces bounded host-restart policy.

- [ ] **Step 5: Commit**

```powershell
git add src/runner/dotnet tests/unit/runner/dotnet examples/workspaces/windows-slice DearStory.slnx eng/build.ps1
git commit -m "feat: add windows runner configuration and supervision core"
```

### Task 4: Deliver the native C++ host with one Dear ImGui story and frame publication

**Files:**

- Create: `src/hosts/cpp/CMakeLists.txt`
- Create: `src/hosts/cpp/include/dearstory/hosts/cpp/native_host.hpp`
- Create: `src/hosts/cpp/src/native_host.cpp`
- Create: `src/hosts/cpp/src/main.cpp`
- Create: `examples/workspaces/windows-slice/cpp/src/buttons_primary.cpp`
- Create: `tests/conformance/hosts/DearStory.HostConformance.Tests/CppHostConformanceTests.cs`

**Interfaces:**

- Consumes: protocol/core C++ libraries plus `dearstory::transports::windows::shared_memory_frame_channel`.
- Produces: `dearstory-host-cpp.exe`, `dearstory::hosts::cpp::native_host::run()`, and the first native story ID `buttons/primary`.

- [ ] **Step 1: Write the failing native host conformance test**

Create `tests/conformance/hosts/DearStory.HostConformance.Tests/CppHostConformanceTests.cs`:

```csharp
public sealed class CppHostConformanceTests
{
    [Fact]
    public async Task Cpp_host_publishes_story_index_and_first_frame()
    {
        await using var harness = await HostHarness.StartAsync("cpp-host");

        var stories = await harness.WaitForStoryIndexAsync();
        var frame = await harness.OpenSessionAndReadFrameAsync("buttons/primary");

        Assert.Contains(stories, story => story.CanonicalId == "buttons/primary");
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
    }
}
```

- [ ] **Step 2: Run the focused conformance test**

Run:

```powershell
dotnet test .\tests\conformance\hosts\DearStory.HostConformance.Tests\DearStory.HostConformance.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~CppHostConformanceTests
```

Expected: FAIL because the native host executable and harness target are missing.

- [ ] **Step 3: Implement the native host and example story**

Create `examples/workspaces/windows-slice/cpp/src/buttons_primary.cpp`:

```cpp
DB_STORY("Buttons/Primary", buttons_primary)
{
    auto label = context.args.string("label", "Save");

    if (ImGui::Button(label.c_str()))
    {
        context.actions.emit("clicked");
    }

    context.targets.capture_last_item("save-button");
}
```

Create `src/hosts/cpp/include/dearstory/hosts/cpp/native_host.hpp`:

```cpp
namespace dearstory::hosts::cpp
{
    class native_host final
    {
    public:
        native_host(protocol::windows::named_pipe_client control_client,
                    transports::windows::shared_memory_frame_channel frame_channel);

        int run();
    };
}
```

Implement `native_host.cpp` so startup performs handshake, publishes the story index, opens sessions, renders to an RGBA buffer, writes through the shared-memory frame channel, and emits `frame_presented`.

- [ ] **Step 4: Re-run the focused native host conformance test**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
dotnet test .\tests\conformance\hosts\DearStory.HostConformance.Tests\DearStory.HostConformance.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~CppHostConformanceTests
```

Expected: PASS. The native host starts under harness control, publishes `buttons/primary`, and produces the first RGBA frame.

- [ ] **Step 5: Commit**

```powershell
git add src/hosts/cpp examples/workspaces/windows-slice/cpp tests/conformance/hosts DearStory.slnx CMakeLists.txt
git commit -m "feat: add native cplusplus story host baseline"
```

### Task 5: Deliver the managed .NET host with one ImGui.NET story and frame publication

**Files:**

- Create: `src/hosts/dotnet/DearStory.Host/DearStory.Host.csproj`
- Create: `src/hosts/dotnet/DearStory.Host/Program.cs`
- Create: `src/hosts/dotnet/DearStory.Host/ManagedHost.cs`
- Create: `src/hosts/dotnet/DearStory.Host/StoryAssemblyLoadContext.cs`
- Create: `examples/workspaces/windows-slice/dotnet/Stories/ButtonsPrimary.cs`
- Create: `tests/conformance/hosts/DearStory.HostConformance.Tests/DotNetHostConformanceTests.cs`

**Interfaces:**

- Consumes: `DearStory.Protocol`, `DearStory.Core`, `DearStory.Sdk`, `DearStory.Transport.Windows`, and ImGui.NET `1.91.6.1`.
- Produces: `DearStory.Host.exe`, `ManagedHost.RunAsync()`, and the first managed story ID `buttons/primarymanaged`.

- [ ] **Step 1: Write the failing managed host conformance test**

Create `tests/conformance/hosts/DearStory.HostConformance.Tests/DotNetHostConformanceTests.cs`:

```csharp
public sealed class DotNetHostConformanceTests
{
    [Fact]
    public async Task Dotnet_host_publishes_story_index_and_first_frame()
    {
        await using var harness = await HostHarness.StartAsync("dotnet-host");

        var stories = await harness.WaitForStoryIndexAsync();
        var frame = await harness.OpenSessionAndReadFrameAsync("buttons/primarymanaged");

        Assert.Contains(stories, story => story.CanonicalId == "buttons/primarymanaged");
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
    }
}
```

- [ ] **Step 2: Run the focused managed host conformance test**

Run:

```powershell
dotnet test .\tests\conformance\hosts\DearStory.HostConformance.Tests\DearStory.HostConformance.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~DotNetHostConformanceTests
```

Expected: FAIL because the managed host executable and story assembly are missing.

- [ ] **Step 3: Implement the managed host and example story**

Create `examples/workspaces/windows-slice/dotnet/Stories/ButtonsPrimary.cs`:

```csharp
[Story("Buttons/PrimaryManaged")]
public static class ButtonsPrimary
{
    public static void Render(StoryContext context)
    {
        var label = context.Args.String("label", "Save");

        if (ImGui.Button(label))
        {
            context.Actions.Emit("clicked");
        }

        context.Targets.CaptureLastItem("save-button");
    }
}
```

Create `src/hosts/dotnet/DearStory.Host/ManagedHost.cs`:

```csharp
public sealed class ManagedHost
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _control.NegotiateAsync(cancellationToken);
        await _catalogPublisher.PublishAsync(_registry.DescribeStories(), cancellationToken);
        await _sessionLoop.RunAsync(cancellationToken);
    }
}
```

Use a collectible `AssemblyLoadContext` when possible; when unload safety cannot be proven, require process restart and surface that reason through diagnostics.

- [ ] **Step 4: Re-run the focused managed host conformance test**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
dotnet test .\tests\conformance\hosts\DearStory.HostConformance.Tests\DearStory.HostConformance.Tests.csproj -c Release -m:1 --filter FullyQualifiedName~DotNetHostConformanceTests
```

Expected: PASS. The managed host loads the example story assembly, publishes its story index, and emits its first frame descriptor and RGBA buffer.

- [ ] **Step 5: Commit**

```powershell
git add src/hosts/dotnet examples/workspaces/windows-slice/dotnet tests/conformance/hosts DearStory.slnx
git commit -m "feat: add managed story host baseline"
```

### Task 6: Build the unified catalog UI with live preview, controls, and diagnostics

**Files:**

- Create: `src/catalog/dotnet/DearStory.Catalog/DearStory.Catalog.csproj`
- Create: `src/catalog/dotnet/DearStory.Catalog/CatalogSessionPresenter.cs`
- Create: `src/catalog/dotnet/DearStory.Catalog/CatalogTreeBuilder.cs`
- Create: `src/catalog/dotnet/DearStory.Catalog/Controls/SchemaControlFactory.cs`
- Create: `src/catalog/dotnet/DearStory.Catalog/Preview/PreviewFrameState.cs`
- Create: `src/catalog/dotnet/DearStory.Catalog/Diagnostics/HostDiagnosticsPanel.cs`
- Create: `tests/unit/catalog/dotnet/DearStory.Catalog.Tests/DearStory.Catalog.Tests.csproj`
- Create: `tests/unit/catalog/dotnet/DearStory.Catalog.Tests/CatalogTreeBuilderTests.cs`
- Create: `tests/unit/catalog/dotnet/DearStory.Catalog.Tests/SchemaControlFactoryTests.cs`
- Create: `tests/unit/catalog/dotnet/DearStory.Catalog.Tests/CatalogSessionPresenterTests.cs`

**Interfaces:**

- Consumes: merged `StoryCatalog`, transport frame descriptors, action/log streams, and validated argument schemas.
- Produces: `CatalogSessionPresenter`, `CatalogTreeBuilder.Build(IEnumerable<StoryDescriptor>)`, `SchemaControlFactory.Create(ArgumentSchema schema)`, and a reusable catalog render loop hosted by `DearStory.Runner`.

- [ ] **Step 1: Write the failing catalog tree and control-generation tests**

Create `tests/unit/catalog/dotnet/DearStory.Catalog.Tests/CatalogTreeBuilderTests.cs`:

```csharp
public sealed class CatalogTreeBuilderTests
{
    [Fact]
    public void Build_groups_cpp_and_dotnet_stories_under_one_searchable_tree()
    {
        var stories = new[]
        {
            StoryDescriptor.Create("buttons/primary", "Buttons/Primary"),
            StoryDescriptor.Create("buttons/primarymanaged", "Buttons/PrimaryManaged")
        };

        var tree = CatalogTreeBuilder.Build(stories);

        Assert.Equal("Buttons", tree.Children.Single().Title);
        Assert.Equal(2, tree.Children.Single().Children.Count);
    }
}
```

Create `tests/unit/catalog/dotnet/DearStory.Catalog.Tests/SchemaControlFactoryTests.cs`:

```csharp
public sealed class SchemaControlFactoryTests
{
    [Fact]
    public void Create_returns_color_editor_for_rgba_annotation()
    {
        var schema = ArgumentSchema.Parse("""
            { "type": "string", "format": "color", "x-dearstory-control": "color-rgba" }
            """);

        var control = SchemaControlFactory.Create(schema);

        Assert.Equal("color-rgba", control.Kind);
    }
}
```

- [ ] **Step 2: Run the focused catalog tests**

Run:

```powershell
dotnet test .\tests\unit\catalog\dotnet\DearStory.Catalog.Tests\DearStory.Catalog.Tests.csproj -c Release -m:1
```

Expected: FAIL because the catalog project, tree builder, and control-generation layer do not exist yet.

- [ ] **Step 3: Implement the catalog presenter and UI wiring**

Create `src/catalog/dotnet/DearStory.Catalog/CatalogSessionPresenter.cs`:

```csharp
public sealed class CatalogSessionPresenter
{
    public CatalogSessionPresenter(StoryCatalog catalog, PreviewFrameState preview, SchemaControlFactory controls)
    {
        Catalog = catalog;
        Preview = preview;
        Controls = controls;
    }

    public StoryCatalog Catalog { get; }
    public PreviewFrameState Preview { get; }
    public SchemaControlFactory Controls { get; }

    public void ApplyPatch(string path, JsonNode? value) => _patchDispatcher.Apply(path, value);
}
```

Create `src/catalog/dotnet/DearStory.Catalog/Preview/PreviewFrameState.cs`:

```csharp
public sealed class PreviewFrameState
{
    public void Update(FramePresented message, SharedMemoryFrameReader reader)
    {
        if (reader.TryReadLatest(out var frame))
        {
            CurrentFrame = frame;
        }
    }

    public FrameSlotLease? CurrentFrame { get; private set; }
}
```

Render a searchable story tree, a preview panel, schema-driven controls, action/log panels, and host-health diagnostics with manual retry.

- [ ] **Step 4: Re-run the focused catalog tests and a local dev smoke**

Run:

```powershell
dotnet test .\tests\unit\catalog\dotnet\DearStory.Catalog.Tests\DearStory.Catalog.Tests.csproj -c Release -m:1
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- dev .\examples\workspaces\windows-slice
```

Expected: PASS. Unit tests validate tree/controls state, and the local smoke opens one catalog containing both the native and managed stories.

- [ ] **Step 5: Commit**

```powershell
git add src/catalog/dotnet tests/unit/catalog/dotnet src/runner/dotnet DearStory.slnx docs/architecture/windows-host-baseline.md
git commit -m "feat: add unified windows catalog ui"
```

### Task 7: Add builders, file watching, rebuild, restart, and preserved state

**Files:**

- Modify: `src/runner/dotnet/DearStory.Runner/Commands/DevCommand.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Builders/CMakeHostBuilder.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Builders/DotNetHostBuilder.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Watching/WorkspaceWatcher.cs`
- Create: `src/runner/dotnet/DearStory.Runner/Watching/RestartPlanner.cs`
- Create: `src/runner/dotnet/DearStory.Runner/State/SerializableSessionState.cs`
- Create: `tests/integration/windows/DearStory.WindowsSlice.Tests/DearStory.WindowsSlice.Tests.csproj`
- Create: `tests/integration/windows/DearStory.WindowsSlice.Tests/HotReloadIntegrationTests.cs`
- Create: `tests/integration/windows/DearStory.WindowsSlice.Tests/HostCrashRecoveryTests.cs`
- Create: `docs/guides/windows-dev-workflow.md`

**Interfaces:**

- Consumes: `WorkspaceConfiguration`, `HostSupervisor`, `CatalogSessionPresenter`, and both official host builders.
- Produces: `IHostBuilder.BuildAsync()`, `WorkspaceWatcher.Start()`, `RestartPlanner.PlanChanges(...)`, and `SerializableSessionState`.

- [ ] **Step 1: Write the failing hot-reload and crash-recovery integration tests**

Create `tests/integration/windows/DearStory.WindowsSlice.Tests/HotReloadIntegrationTests.cs`:

```csharp
public sealed class HotReloadIntegrationTests
{
    [Fact]
    public async Task Dev_loop_restarts_only_affected_host_and_preserves_arguments()
    {
        await using var harness = await WindowsSliceHarness.StartAsync();

        await harness.SelectStoryAsync("buttons/primary");
        await harness.ApplyArgumentAsync("label", "Ship");
        await harness.TouchCppStoryAsync();

        var restart = await harness.WaitForHostRestartAsync("cpp-host");

        Assert.Equal("cpp-host", restart.HostId);
        Assert.Equal("Ship", await harness.ReadCurrentArgumentAsync("label"));
        Assert.False(await harness.WasHostRestartedAsync("dotnet-host"));
    }
}
```

- [ ] **Step 2: Run the focused integration tests**

Run:

```powershell
dotnet test .\tests\integration\windows\DearStory.WindowsSlice.Tests\DearStory.WindowsSlice.Tests.csproj -c Release -m:1
```

Expected: FAIL because no builder/watch/restart loop exists and the integration harness cannot observe selective restarts.

- [ ] **Step 3: Implement builders, watching, restart planning, and preserved state**

Create `src/runner/dotnet/DearStory.Runner/Builders/CMakeHostBuilder.cs`:

```csharp
public sealed class CMakeHostBuilder : IHostBuilder
{
    public Task<HostBuildResult> BuildAsync(HostBuildRequest request, CancellationToken cancellationToken) =>
        _processRunner.RunAsync("cmake", ["--build", request.BuildDirectory, "--config", request.Configuration], cancellationToken);
}
```

Create `src/runner/dotnet/DearStory.Runner/Watching/RestartPlanner.cs`:

```csharp
public sealed class RestartPlanner
{
    public IReadOnlyList<string> PlanChanges(IReadOnlyList<string> changedPaths) =>
        changedPaths.Any(path => path.Contains("\\cpp\\", StringComparison.OrdinalIgnoreCase))
            ? ["cpp-host"]
            : changedPaths.Any(path => path.Contains("\\dotnet\\", StringComparison.OrdinalIgnoreCase))
                ? ["dotnet-host"]
                : [];
}
```

Persist the selected story ID plus serializable arguments in `SerializableSessionState` and restore them after a compatible host restart.

- [ ] **Step 4: Re-run the focused integration tests**

Run:

```powershell
dotnet test .\tests\integration\windows\DearStory.WindowsSlice.Tests\DearStory.WindowsSlice.Tests.csproj -c Release -m:1
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- dev .\examples\workspaces\windows-slice
```

Expected: PASS. Editing a C++ story restarts only `cpp-host`, preserves serializable argument state, and crash recovery surfaces bounded retry plus diagnostics.

- [ ] **Step 5: Commit**

```powershell
git add src/runner/dotnet tests/integration/windows docs/guides/windows-dev-workflow.md
git commit -m "feat: add build watch and restart workflow"
```

### Task 8: Add the safe docs pipeline, static HTML build output, screenshots, and final hardening

**Files:**

- Create: `src/docs/dotnet/DearStory.Docs/DearStory.Docs.csproj`
- Create: `src/docs/dotnet/DearStory.Docs/Markdown/DocumentModel.cs`
- Create: `src/docs/dotnet/DearStory.Docs/Markdown/DocBlockParser.cs`
- Create: `src/docs/dotnet/DearStory.Docs/Autodocs/AutodocsGenerator.cs`
- Create: `src/docs/dotnet/DearStory.Docs/StaticHtml/StaticSiteBuilder.cs`
- Create: `tools/DearStory.CaptureWorker/DearStory.CaptureWorker.csproj`
- Create: `tools/DearStory.CaptureWorker/Program.cs`
- Create: `examples/workspaces/windows-slice/docs/buttons-primary.md`
- Create: `tests/unit/docs/dotnet/DearStory.Docs.Tests/DearStory.Docs.Tests.csproj`
- Create: `tests/unit/docs/dotnet/DearStory.Docs.Tests/DocBlockParserTests.cs`
- Create: `tests/unit/docs/dotnet/DearStory.Docs.Tests/StaticSiteBuilderTests.cs`
- Create: `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/DearStory.WindowsSlice.E2ETests.csproj`
- Create: `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/DevCommandSmokeTests.cs`
- Create: `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/BuildCommandStaticDocsTests.cs`
- Create: `tests/visual/windows/README.md`
- Modify: `eng/build.ps1`
- Modify: `eng/test.ps1`
- Modify: `.github/workflows/ci.yml`
- Modify: `docs/guides/building-windows.md`
- Modify: `docs/guides/authoring-stories.md`
- Create: `docs/guides/static-docs.md`
- Modify: `docs/standards/documentation-and-quality.md`
- Modify: `README.md`

**Interfaces:**

- Consumes: `StoryCatalog`, story schemas, example docs globs, and deterministic host captures.
- Produces: `DocBlockParser.Parse(string markdown)`, `AutodocsGenerator.Generate(StoryDescriptor story)`, `StaticSiteBuilder.BuildAsync(BuildRequest request)`, `dearstory build`, and CI artifact folders containing logs, screenshots, HTML, and diffs.

- [ ] **Step 1: Write the failing doc-block and build-output tests**

Create `tests/unit/docs/dotnet/DearStory.Docs.Tests/DocBlockParserTests.cs`:

```csharp
public sealed class DocBlockParserTests
{
    [Fact]
    public void Parse_recognizes_story_controls_and_source_blocks()
    {
        var document = DocBlockParser.Parse("""
            # Primary Button
            :::story id="buttons/primary"
            :::
            :::controls
            :::
            :::source language="cpp"
            :::
            """);

        Assert.Equal(3, document.Blocks.Count(block => block.Kind is "story" or "controls" or "source"));
    }
}
```

Create `tests/e2e/windows/DearStory.WindowsSlice.E2ETests/BuildCommandStaticDocsTests.cs`:

```csharp
public sealed class BuildCommandStaticDocsTests
{
    [Fact]
    public async Task Build_command_emits_searchable_html_and_screenshot()
    {
        var result = await DearStoryCommand.RunAsync("build", ".\\examples\\workspaces\\windows-slice", "--configuration", "Release");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("index.html", result.OutputFiles);
        Assert.Contains("buttons-primary.png", result.OutputFiles);
    }
}
```

- [ ] **Step 2: Run the focused docs and e2e tests**

Run:

```powershell
dotnet test .\tests\unit\docs\dotnet\DearStory.Docs.Tests\DearStory.Docs.Tests.csproj -c Release -m:1
dotnet test .\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj -c Release -m:1 --filter FullyQualifiedName~BuildCommandStaticDocsTests
```

Expected: FAIL because the docs project, capture worker, and static-site output do not exist yet.

- [ ] **Step 3: Implement the docs pipeline, capture worker, and `dearstory build`**

Create `examples/workspaces/windows-slice/docs/buttons-primary.md`:

```markdown
# Primary Button

Use the primary button for the main action in a view.

:::story id="buttons/primary"
:::

## Properties

:::controls
:::

:::arg-types
:::

:::source language="cpp"
:::
```

Create `src/docs/dotnet/DearStory.Docs/StaticHtml/StaticSiteBuilder.cs`:

```csharp
public sealed class StaticSiteBuilder
{
    public async Task BuildAsync(BuildRequest request, CancellationToken cancellationToken)
    {
        var document = await _loader.LoadAsync(request, cancellationToken);
        var autodocs = _autodocs.Generate(document.Stories);
        await _writer.WriteAsync(request.OutputDirectory, document, autodocs, cancellationToken);
    }
}
```

Create `tools/DearStory.CaptureWorker/Program.cs` so `dearstory build` can request deterministic screenshots from both hosts under the pinned WARP profile and write them to `artifacts/docs` before HTML emission.

- [ ] **Step 4: Run the full slice verification**

Run:

```powershell
pwsh -NoProfile -File .\eng\generate-protocol.ps1 -Check
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release -Coverage
dotnet test .\tests\conformance\hosts\DearStory.HostConformance.Tests\DearStory.HostConformance.Tests.csproj -c Release -m:1
dotnet test .\tests\integration\windows\DearStory.WindowsSlice.Tests\DearStory.WindowsSlice.Tests.csproj -c Release -m:1
dotnet test .\tests\e2e\windows\DearStory.WindowsSlice.E2ETests\DearStory.WindowsSlice.E2ETests.csproj -c Release -m:1
doxygen .\Doxyfile
git diff --check
```

Expected: PASS. The branch now supports `dearstory dev` for the mixed-language workspace, `dearstory build` for safe static HTML plus screenshots, deterministic Windows CI, and the repository’s documentation/coverage gates.

- [ ] **Step 5: Commit**

```powershell
git add src/docs/dotnet tools/DearStory.CaptureWorker examples/workspaces/windows-slice/docs tests/unit/docs/dotnet tests/e2e/windows tests/visual/windows eng/build.ps1 eng/test.ps1 .github/workflows/ci.yml docs/guides/building-windows.md docs/guides/authoring-stories.md docs/guides/static-docs.md docs/standards/documentation-and-quality.md README.md DearStory.slnx
git commit -m "feat: add static docs pipeline and windows slice hardening"
```

## Final verification checklist

- [ ] `frame_channel_ready`, `frame_presented`, `capture_requested`, `capture_completed`, `heartbeat`, and `host_faulted` are generated into both protocol languages and documented.
- [ ] The Windows runner starts one C++ host and one .NET host from `examples/workspaces/windows-slice/dearstory.toml`.
- [ ] Both hosts publish stories into one catalog and produce RGBA frames over shared memory.
- [ ] The catalog exposes search, preview, schema-generated controls, logs, actions, and host diagnostics.
- [ ] Editing example C++ or .NET story files rebuilds and restarts only the affected host.
- [ ] Serializable selection and arguments survive compatible host restarts.
- [ ] `dearstory build` emits static HTML, screenshots, source snippets, schemas, and search data.
- [ ] Deterministic captures run in Windows CI under WARP and publish useful artifacts on failure.
- [ ] Public APIs, ADRs, architecture docs, guides, and code comments are updated for the new modules.
- [ ] `pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release`, `pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release -Coverage`, `doxygen .\Doxyfile`, and `git diff --check` all pass.

## Spec coverage self-review

- **Milestones 17.1-17.3:** covered by Tasks 1 through 8 through explicit work on supervisor, RGBA frame transport, both official hosts, one unified catalog, build/watch/restart, Markdown/Doc Blocks, autodocs, screenshots, and static HTML.
- **Sections 6, 8, 9, 10, 11, 13, 14, and 15 of the design spec:** mapped to transport, runner, protocol, controls, docs, recovery, testing, and documentation-quality tasks above.
- **Explicit backlog exclusions:** Linux, macOS, D3D11 shared textures, browser execution, extra hosts, remote execution, and embedded mode are intentionally omitted and called out in the scope guard.
- **No placeholders:** each task names exact files, concrete tests, concrete commands, and explicit interfaces. No “TODO”, “TBD”, or “similar to Task N” shortcuts remain.
