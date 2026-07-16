# DearStory Cross-Language Protocol Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:subagent-driven-development` (recommended) or
> `superpowers:executing-plans` to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a Windows-native, versioned control-protocol bootstrap in
which a C++ probe and a .NET probe exchange a real `hello`/`welcome` handshake
over a named pipe and validate the same contract vectors in CI.

**Architecture:** A language-neutral JSON manifest defines message names,
fields, and wire names. A deterministic .NET build tool emits checked-in C++
and C# models from that manifest. Each language owns its JSON codec and
length-prefixed stream implementation, while a shared set of JSON vectors and
one black-box named-pipe test prevent behavioral drift.

**Tech Stack:** C++20, MSVC 19.40 or newer, CMake 3.30 or newer, vcpkg manifest
mode, nlohmann-json 3.12.0, Catch2 3.15.2, .NET 10 LTS, System.Text.Json,
JsonSchema.Net 9.2.2, xUnit.net v3 3.2.2, xunit.runner.visualstudio 3.1.5,
Microsoft.NET.Test.Sdk 18.8.1, coverlet.collector 10.0.1, PowerShell 7, Windows
named pipes, OpenCppCoverage 0.9.9.0 (Chocolatey package `0.9.9`), Doxygen
1.17.0, and GitHub Actions on
`windows-2022`.

## Global Constraints

- Windows is the only implementation platform in this plan. Do not create or
  require WSL, WSL2, Docker, or a Unix shell.
- The repository remains public under the MIT License.
- DearStory is Dear ImGui-first and language-neutral; neither the protocol nor
  the runner contract may depend on CLR or C++ ABI details.
- Standalone peers are process-isolated. Process isolation is not represented
  as a security sandbox.
- Control messages are length-prefixed UTF-8 JSON. Binary frames and large
  attachments never enter this channel.
- Protocol version `1.0` is the first wire version. Major mismatches are
  rejected; the negotiated minor is the lower supported minor.
- A control frame is rejected before allocation when its declared payload is
  greater than `1,048,576` bytes.
- Message IDs and correlation IDs are lowercase RFC 4122 UUID strings.
- Timestamps are UTC RFC 3339 strings with millisecond precision.
- Unknown optional JSON fields are ignored. Unknown message types and missing
  required fields are errors with stable error codes.
- Public C++ APIs require Doxygen comments. Public C# APIs require XML
  documentation. Documentation warnings are build failures.
- C++ and C# compile with warnings as errors. C# nullable analysis is enabled.
- Protocol code begins with minimum gates of 80 percent line coverage and
  70 percent branch coverage.
- Every implementation task follows red-green-refactor TDD and ends in a
  focused commit.
- Dependencies and GitHub Actions are pinned to immutable versions or commit
  SHAs. Generated files are checked in and CI proves regeneration is clean.
- Native Dear ImGui `v1.92.8` (`8936b58fe26e8c3da834b8f60b06511d537b4c63`)
  is reserved for the native host plan. ImGui.NET `1.91.6.1` is reserved for
  the official .NET adapter plan. Their independent versions are recorded in
  the handshake instead of treated as ABI-compatible.

---

## Delivery map

This is the first of six execution plans. It intentionally ends at a complete,
testable cross-language protocol handshake. Each subsequent plan is written
after its predecessor has committed interface snapshots, so paths and
signatures are based on verified code rather than guesses.

1. **Protocol bootstrap — this document:** build contracts, generated models,
   codecs, framing, named pipes, handshake, contract tests, and CI.
2. **Core story model and schemas:** stable story IDs, catalog merge, sessions,
   JSON Schema 2020-12 argument subset, validated patches, actions, logs,
   targets plus optional semantic metadata, deterministic clock/random
   services, C++ descriptors, the C# attribute/source-generator surface, and
   its explicitly limited runtime-reflection fallback.
3. **Hosts and RGBA frame transport:** D3D11 WARP renderer, multi-slot shared
   memory, one direct-`ImGui::` C++ story, one direct-ImGui.NET C# story, input,
   viewport, DPI, theme, pinned visual environment, crash containment,
   heartbeat recovery, and unchanged black-box conformance tests for both
   hosts.
4. **Runner, builders, and native catalog:** `dearstory.toml`, `dev`, CMake and
   MSBuild builders, safe process launch, watch/rebuild/restart, merged catalog,
   navigation, search, controls, preview, actions, logs, recovery UI, stable
   categorized exit codes, and diagnostic artifacts.
5. **Documentation and testing products:** safe CommonMark/GFM document model
   with raw HTML disabled and no executable MDX, typed Doc Blocks, Autodocs,
   source snippets, deterministic captures, searchable non-executable static HTML
   `build`, semantic completeness reporting, named-target interaction
   tests, visual diffs, property/fuzz suites, selective mutation tests, and
   console/machine-readable/HTML `test` reports.
6. **Embedding, packaging, and Windows optimization:** local transport,
   embedding APIs, CLI/package/templates, compatibility and migration docs,
   contributor/security/release policies, semantic versioning, dependency
   notices, D3D11 shared textures, profiling, backpressure metrics, and release provenance
   with checksums.

Linux, macOS, browser execution, additional languages, remote execution, and
ecosystem adapters remain in the public backlog and do not enter these six
Windows-first plans.

## File structure locked by this plan

```text
.editorconfig                              repository formatting policy
.gitattributes                             line-ending and generated-file policy
.gitignore                                 build, test, coverage, and IDE outputs
CMakeLists.txt                             native build root
CMakePresets.json                          supported Windows native presets
Directory.Build.props                      shared .NET quality policy
Directory.Packages.props                   centrally pinned NuGet versions
DearStory.slnx                             .NET solution
global.json                                .NET 10 SDK selection
xunit.runner.json                          serial xUnit execution policy
vcpkg-configuration.json                   pinned vcpkg registry
vcpkg.json                                 native dependency manifest
.github/workflows/ci.yml                   Windows build/test/doc/coverage gates
cmake/dearstory_warnings.cmake             native warning policy
eng/Doctor.psm1                            testable prerequisite detection
eng/doctor.ps1                             user-facing prerequisite report
eng/build.ps1                              transparent CMake + dotnet orchestration
eng/test.ps1                               unit, contract, and E2E orchestration
eng/generate-protocol.ps1                  deterministic generation/check entrypoint
eng/assert-coverage.ps1                    80/70 coverage gate
docs/adr/0001-control-protocol.md          framing and compatibility decision
docs/guides/building-windows.md            clean-checkout setup
docs/protocol/control-v1.md                 normative wire documentation
protocol/control/messages.json             language-neutral message manifest
protocol/control/control-envelope.schema.json JSON Schema 2020-12 envelope
protocol/test-vectors/handshake/*.json      shared positive and negative vectors
src/protocol/cpp/CMakeLists.txt             C++ protocol target
src/protocol/cpp/include/dearstory/protocol/*.hpp public native API
src/protocol/cpp/include/dearstory/protocol/generated/messages.hpp generated models
src/protocol/cpp/src/*.cpp                  native codec, framing, pipe, negotiation
src/protocol/dotnet/DearStory.Protocol/*    managed codec, framing, pipe, negotiation
tools/DearStory.ProtocolGenerator/*         deterministic model generator
tools/DearStory.ProtocolProbe.Cpp/*         one-shot native handshake server
tools/DearStory.ProtocolProbe.DotNet/*      one-shot managed handshake client
tests/unit/foundation/*                     build-policy tests
tests/unit/protocol/cpp/*                   Catch2 unit tests
tests/unit/protocol/dotnet/*                xUnit unit tests
tests/contract/protocol/*                   schema/vector conformance tests
tests/e2e/protocol/*                        cross-process named-pipe tests
```

### Task 1: Windows prerequisite contract and repository policy

**Files:**

- Create: `.editorconfig`
- Create: `.gitattributes`
- Create: `.gitignore`
- Create: `eng/Doctor.psm1`
- Create: `eng/doctor.ps1`
- Create: `tests/unit/foundation/Doctor.Tests.ps1`
- Create: `docs/guides/building-windows.md`

**Interfaces:**

- Consumes: native Windows PowerShell and the approved design specification.
- Produces: `Get-DearStoryPrerequisiteReport -> [PrerequisiteResult[]]` and
  `Test-DearStoryVersion -Actual string -Minimum System.Version -> bool`.

- [ ] **Step 1: Write the failing version-policy test**

```powershell
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
pwsh -NoProfile -File .\tests\unit\foundation\Doctor.Tests.ps1
```

Expected: FAIL because `eng\Doctor.psm1` does not exist.

- [ ] **Step 3: Implement the prerequisite module and CLI**

Create `eng/Doctor.psm1` with these public functions and no process mutation:

```powershell
Set-StrictMode -Version Latest

function Test-DearStoryVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Actual,
        [Parameter(Mandatory)][version]$Minimum
    )

    $match = [regex]::Match($Actual, '(?<version>\d+\.\d+(?:\.\d+)?)')
    if (-not $match.Success) { return $false }
    return ([version]$match.Groups['version'].Value) -ge $Minimum
}

function Get-DearStoryPrerequisiteReport {
    [CmdletBinding()]
    param()

    $checks = @(
        @{ Name = 'PowerShell'; Command = 'pwsh'; Arguments = @('--version'); Minimum = [version]'7.4' },
        @{ Name = '.NET SDK'; Command = 'dotnet'; Arguments = @('--version'); Minimum = [version]'10.0' },
        @{ Name = 'CMake'; Command = 'cmake'; Arguments = @('--version'); Minimum = [version]'3.30' },
        @{ Name = 'Git'; Command = 'git'; Arguments = @('--version'); Minimum = [version]'2.45' }
    )

    foreach ($check in $checks) {
        $resolved = Get-Command $check.Command -ErrorAction SilentlyContinue
        if ($null -eq $resolved) {
            [pscustomobject]@{
                Name = $check.Name; Command = $check.Command; Found = $false
                MeetsMinimum = $false; Output = ''; Required = $check.Minimum.ToString()
            }
            continue
        }

        $output = (& $resolved.Source @($check.Arguments) 2>&1 | Out-String).Trim()
        [pscustomobject]@{
            Name = $check.Name; Command = $resolved.Source; Found = $true
            MeetsMinimum = Test-DearStoryVersion -Actual $output -Minimum $check.Minimum
            Output = $output; Required = $check.Minimum.ToString()
        }
    }
}

Export-ModuleMember -Function Test-DearStoryVersion, Get-DearStoryPrerequisiteReport
```

Create `eng/doctor.ps1`:

```powershell
[CmdletBinding()]
param([switch]$Json)

$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot\Doctor.psm1" -Force
$report = @(Get-DearStoryPrerequisiteReport)

if ($Json) {
    $report | ConvertTo-Json -Depth 4
} else {
    $report | Format-Table Name, Found, MeetsMinimum, Required, Command -AutoSize
}

if ($report.Where({ -not $_.MeetsMinimum }).Count -gt 0) { exit 1 }
```

Document the exact native prerequisites in `docs/guides/building-windows.md`:
Visual Studio 2022 Build Tools 17.10+ with Desktop development with C++, .NET
10 SDK, CMake 3.30+, Git 2.45+, PowerShell 7.4+, and a local vcpkg checkout
identified by `VCPKG_ROOT`. State that the repository never creates WSL2 and
that system-wide installation requires an explicit user decision.

- [ ] **Step 4: Add formatting and ignore policy**

Set UTF-8, final newline, four spaces for C++/C#/PowerShell, two spaces for
JSON/YAML, CRLF for `.ps1`, LF for repository text elsewhere, and ignore only
`.vs/`, `.vscode/`, `artifacts/`, `build/`, `TestResults/`, `coverage/`,
`*.user`, and generated local caches. Mark
`src/protocol/*/generated/* linguist-generated=true` in `.gitattributes`.

- [ ] **Step 5: Run the test and doctor**

Run:

```powershell
pwsh -NoProfile -File .\tests\unit\foundation\Doctor.Tests.ps1
pwsh -NoProfile -File .\eng\doctor.ps1 -Json
```

Expected: the unit test passes. `doctor.ps1` exits `0` only when every required
native tool is present and otherwise returns a JSON report naming each missing
or old tool. Do not install missing system tools inside this task.

- [ ] **Step 6: Commit**

```powershell
git add .editorconfig .gitattributes .gitignore eng docs/guides tests/unit/foundation
git commit -m "build: define native Windows prerequisites"
```

### Task 2: Native C++20 build and protocol version primitive

**Files:**

- Create: `CMakeLists.txt`
- Create: `CMakePresets.json`
- Create: `vcpkg.json`
- Create: `vcpkg-configuration.json`
- Create: `cmake/dearstory_warnings.cmake`
- Create: `src/protocol/cpp/CMakeLists.txt`
- Create: `src/protocol/cpp/include/dearstory/protocol/version.hpp`
- Create: `tests/unit/protocol/cpp/CMakeLists.txt`
- Create: `tests/unit/protocol/cpp/version_tests.cpp`

**Interfaces:**

- Consumes: `VCPKG_ROOT`, CMake preset `windows-msvc-debug`.
- Produces: `dearstory::protocol::version`, constants `current_major = 1` and
  `current_minor = 0`, CMake target `DearStory::ProtocolCpp`.

- [ ] **Step 1: Write the failing C++ version test**

```cpp
#include <dearstory/protocol/version.hpp>
#include <catch2/catch_test_macros.hpp>

TEST_CASE("protocol 1.0 negotiates the lower compatible minor")
{
    using dearstory::protocol::version;
    REQUIRE(version{1, 0}.negotiate(version{1, 3}) == version{1, 0});
    REQUIRE_FALSE(version{2, 0}.is_major_compatible(version{1, 0}));
}
```

- [ ] **Step 2: Add the native build root and verify red**

Use project `DearStory VERSION 0.1.0 LANGUAGES CXX`, set C++20 with extensions
off, include `CTest`, and add `src/protocol/cpp` plus tests when
`BUILD_TESTING=ON`. The `windows-msvc-debug` configure preset uses generator
`Visual Studio 17 2022`, architecture `x64`, binary directory
`build/windows-msvc-debug`, and
`$env{VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake`.

Pin `builtin-baseline` to
`1db84273378ff8e2d30e7bc7fdd5d1cb4f4260fc`; declare `nlohmann-json` and
`catch2` dependencies. Run:

```powershell
cmake --preset windows-msvc-debug
cmake --build --preset windows-msvc-debug
```

Expected: FAIL because `version.hpp` is absent.

- [ ] **Step 3: Implement the minimal documented native version type**

```cpp
#pragma once

#include <cstdint>
#include <optional>

namespace dearstory::protocol {

/// Identifies a DearStory wire-protocol version.
struct version final {
    std::uint16_t major{};
    std::uint16_t minor{};

    /// Returns true when both peers use the same protocol major.
    [[nodiscard]] constexpr bool is_major_compatible(version other) const noexcept
    {
        return major == other.major;
    }

    /// Chooses the shared major and the lower supported minor.
    [[nodiscard]] constexpr std::optional<version> negotiate(version other) const noexcept
    {
        if (!is_major_compatible(other)) { return std::nullopt; }
        return version{major, minor < other.minor ? minor : other.minor};
    }

    friend constexpr bool operator==(version, version) noexcept = default;
};

inline constexpr std::uint16_t current_major = 1;
inline constexpr std::uint16_t current_minor = 0;

} // namespace dearstory::protocol
```

`dearstory_warnings.cmake` enables `/W4 /WX /permissive- /Zc:__cplusplus` for
MSVC and `-Wall -Wextra -Wpedantic -Werror` otherwise. Export the alias target
`DearStory::ProtocolCpp` without exposing Catch2. Name the Catch2 executable
target `dearstory-protocol-cpp-tests` and set native runtime output to
`artifacts/bin/native/$<CONFIG>` so scripts never search configuration-specific
Visual Studio directories.

- [ ] **Step 4: Build and run the native unit test**

```powershell
cmake --build --preset windows-msvc-debug
ctest --preset windows-msvc-debug --output-on-failure
```

Expected: PASS, including `protocol 1.0 negotiates the lower compatible minor`.

- [ ] **Step 5: Commit**

```powershell
git add CMakeLists.txt CMakePresets.json vcpkg.json vcpkg-configuration.json cmake src/protocol/cpp tests/unit/protocol/cpp
git commit -m "build: establish C++20 protocol target"
```

### Task 3: .NET 10 build and matching protocol version primitive

**Files:**

- Create: `global.json`
- Create: `xunit.runner.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `DearStory.slnx`
- Create: `src/protocol/dotnet/DearStory.Protocol/DearStory.Protocol.csproj`
- Create: `src/protocol/dotnet/DearStory.Protocol/ProtocolVersion.cs`
- Create: `tests/unit/protocol/dotnet/DearStory.Protocol.Tests/DearStory.Protocol.Tests.csproj`
- Create: `tests/unit/protocol/dotnet/DearStory.Protocol.Tests/ProtocolVersionTests.cs`

**Interfaces:**

- Consumes: .NET SDK `10.0.100` or a newer .NET 10 feature band.
- Produces: `DearStory.Protocol.ProtocolVersion`, constants `CurrentMajor = 1`
  and `CurrentMinor = 0`.

- [ ] **Step 1: Create the projects and write the failing managed test**

```csharp
using DearStory.Protocol;

namespace DearStory.Protocol.Tests;

public sealed class ProtocolVersionTests
{
    [Fact]
    public void Negotiate_UsesLowerMinor_WhenMajorMatches()
    {
        var negotiated = new ProtocolVersion(1, 0).Negotiate(new(1, 3));

        Assert.Equal(new ProtocolVersion(1, 0), negotiated);
        Assert.Null(new ProtocolVersion(2, 0).Negotiate(new(1, 0)));
    }
}
```

Generate `DearStory.slnx` with:

```powershell
dotnet new sln --name DearStory --format slnx
dotnet sln .\DearStory.slnx add .\src\protocol\dotnet\DearStory.Protocol\DearStory.Protocol.csproj
dotnet sln .\DearStory.slnx add .\tests\unit\protocol\dotnet\DearStory.Protocol.Tests\DearStory.Protocol.Tests.csproj
dotnet restore .\DearStory.slnx --use-lock-file
dotnet test .\DearStory.slnx -m:1
```

Expected: FAIL because `ProtocolVersion` is absent.

- [ ] **Step 2: Pin the managed build policy and packages**

`global.json` selects `10.0.100` with `rollForward` set to `latestFeature` and
`allowPrerelease` false. `Directory.Build.props` sets `net10.0`,
`LangVersion=latest`, nullable and implicit usings enabled, deterministic and
continuous-integration builds, warnings as errors, documentation generation,
`RestorePackagesWithLockFile=true`, and `NoWarn` empty for production projects.
Test projects set `IsTestProject=true`, suppress only `CS1591`, and copy the
root `xunit.runner.json` to their output directory.

Central package versions are exactly:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="JsonSchema.Net" Version="9.2.2" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

Every test project references `Microsoft.NET.Test.Sdk`, `xunit.v3`,
`xunit.runner.visualstudio`, and `coverlet.collector`; the runner and collector
use `PrivateAssets=all`. The root runner configuration is:

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1
}
```

- [ ] **Step 3: Implement the documented managed version type**

```csharp
namespace DearStory.Protocol;

/// <summary>Identifies a DearStory wire-protocol version.</summary>
/// <param name="Major">Breaking protocol generation.</param>
/// <param name="Minor">Additive protocol generation.</param>
public readonly record struct ProtocolVersion(ushort Major, ushort Minor)
{
    /// <summary>Gets the current protocol major.</summary>
    public const ushort CurrentMajor = 1;

    /// <summary>Gets the current protocol minor.</summary>
    public const ushort CurrentMinor = 0;

    /// <summary>Returns the shared version, or <see langword="null"/> for a major mismatch.</summary>
    public ProtocolVersion? Negotiate(ProtocolVersion other) =>
        Major == other.Major ? new(Major, Math.Min(Minor, other.Minor)) : null;
}
```

- [ ] **Step 4: Restore, build, and test in-band**

```powershell
dotnet restore .\DearStory.slnx --use-lock-file
dotnet build .\DearStory.slnx --no-restore -warnaserror
dotnet test .\DearStory.slnx --no-build -m:1
dotnet restore .\DearStory.slnx --locked-mode
```

Expected: PASS. Commit `packages.lock.json` for every project.

- [ ] **Step 5: Commit**

```powershell
git add global.json xunit.runner.json Directory.Build.props Directory.Packages.props DearStory.slnx src/protocol/dotnet tests/unit/protocol/dotnet
git commit -m "build: establish .NET 10 protocol target"
```

### Task 4: Unified transparent build commands and baseline CI

**Files:**

- Create: `eng/build.ps1`
- Create: `eng/test.ps1`
- Create: `.github/workflows/ci.yml`
- Modify: `docs/guides/building-windows.md`

**Interfaces:**

- Consumes: CMake and dotnet projects from Tasks 2 and 3.
- Produces: `pwsh ./eng/build.ps1 -Configuration Debug|Release` and
  `pwsh ./eng/test.ps1 -Configuration Debug|Release`.

- [ ] **Step 1: Write a failing repository-command smoke test**

Add `tests/unit/foundation/BuildScripts.Tests.ps1` that invokes both scripts
with `-WhatIf`, asserts exit code zero, and asserts the emitted command list
contains `cmake --build`, `ctest`, `dotnet build`, and `dotnet test` exactly
once. Run it before the scripts exist and expect failure.

- [ ] **Step 2: Implement scripts that preserve native diagnostics**

Both scripts use `[CmdletBinding(SupportsShouldProcess)]`, `$ErrorActionPreference
= 'Stop'`, and argument arrays. They invoke executables directly with `&` and
immediately throw a message containing executable, arguments, and
`$LASTEXITCODE` when nonzero. They do not use `Invoke-Expression`, `cmd /c`, or
concatenated shell commands.

`build.ps1` runs, in order:

```powershell
cmake --preset windows-msvc-debug
cmake --build --preset windows-msvc-debug
dotnet restore .\DearStory.slnx --locked-mode
dotnet build .\DearStory.slnx --no-restore -warnaserror
```

`test.ps1` runs, in order:

```powershell
ctest --preset windows-msvc-debug --output-on-failure
dotnet test .\DearStory.slnx --no-build -m:1
pwsh -NoProfile -File .\tests\unit\foundation\Doctor.Tests.ps1
pwsh -NoProfile -File .\tests\unit\foundation\BuildScripts.Tests.ps1
```

- [ ] **Step 3: Run the smoke test and full build locally**

```powershell
pwsh -NoProfile -File .\tests\unit\foundation\BuildScripts.Tests.ps1
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Debug
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Debug
```

Expected: all commands pass. If the prerequisite report is not green, stop at
that evidence and request authorization before installing system-wide tools.

- [ ] **Step 4: Add the pinned Windows CI workflow**

Use `windows-2022`, concurrency cancellation per branch, least-privilege
`contents: read`, and these immutable action commits:

- `actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0`
  (`v7.0.0`)
- `actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1`
  (`v5.4.0`)
- `actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a`
  (`v7.0.1`)

Cache only vcpkg binary artifacts and NuGet packages using lock-file hashes.
Run doctor, build, and test as separate named steps. Upload `artifacts/logs`
with `if: always()`.

- [ ] **Step 5: Commit**

```powershell
git add eng tests/unit/foundation .github/workflows/ci.yml docs/guides/building-windows.md
git commit -m "ci: add transparent Windows build gates"
```

### Task 5: Canonical handshake manifest, schema, vectors, and ADR

**Files:**

- Create: `protocol/control/messages.json`
- Create: `protocol/control/control-envelope.schema.json`
- Create: `protocol/test-vectors/handshake/hello.valid.json`
- Create: `protocol/test-vectors/handshake/welcome.valid.json`
- Create: `protocol/test-vectors/handshake/reject.major-mismatch.json`
- Create: `protocol/test-vectors/handshake/hello.missing-message-id.json`
- Create: `docs/adr/0001-control-protocol.md`
- Create: `docs/protocol/control-v1.md`
- Create: `tests/contract/protocol/DearStory.Protocol.ContractTests/DearStory.Protocol.ContractTests.csproj`
- Create: `tests/contract/protocol/DearStory.Protocol.ContractTests/EnvelopeSchemaTests.cs`

**Interfaces:**

- Consumes: JSON Schema Draft 2020-12 and protocol version `1.0`.
- Produces: wire types `hello`, `welcome`, and `reject`; error codes
  `protocol.major_mismatch`, `protocol.required_capability_missing`,
  `protocol.unknown_message_type`, `protocol.invalid_envelope`, and
  `protocol.frame_too_large`.

- [ ] **Step 1: Write failing schema-vector tests**

```csharp
using System.Text.Json.Nodes;
using Json.Schema;

namespace DearStory.Protocol.ContractTests;

public sealed class EnvelopeSchemaTests
{
    public static TheoryData<string, bool> Vectors => new()
    {
        { "hello.valid.json", true },
        { "welcome.valid.json", true },
        { "reject.major-mismatch.json", true },
        { "hello.missing-message-id.json", false },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Vector_matches_envelope_schema(string fileName, bool expected)
    {
        var root = FindRepositoryRoot();
        var schema = JsonSchema.FromText(File.ReadAllText(
            Path.Combine(root, "protocol", "control", "control-envelope.schema.json")));
        var instance = JsonNode.Parse(File.ReadAllText(
            Path.Combine(root, "protocol", "test-vectors", "handshake", fileName)))!;

        var result = schema.Evaluate(instance, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true,
        });

        Assert.Equal(expected, result.IsValid);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DearStory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root containing DearStory.slnx was not found.");
    }
}
```

Run the focused test. Expected: FAIL because the schema and vectors are absent.

- [ ] **Step 2: Define the exact manifest**

`messages.json` contains enums `peer_role = runner|catalog|host`, shared records
`protocol_version`, `implementation_identity`, `protocol_error`, and messages:

```json
{
  "protocol": { "major": 1, "minor": 0 },
  "messages": [
    {
      "name": "hello",
      "fields": [
        { "name": "role", "type": "peer_role", "required": true },
        { "name": "implementation", "type": "implementation_identity", "required": true },
        { "name": "supportedCapabilities", "type": "string[]", "required": true },
        { "name": "requiredCapabilities", "type": "string[]", "required": true }
      ]
    },
    {
      "name": "welcome",
      "fields": [
        { "name": "peerId", "type": "uuid", "required": true },
        { "name": "negotiatedVersion", "type": "protocol_version", "required": true },
        { "name": "acceptedCapabilities", "type": "string[]", "required": true }
      ]
    },
    {
      "name": "reject",
      "fields": [
        { "name": "error", "type": "protocol_error", "required": true }
      ]
    }
  ]
}
```

`implementation_identity` has required `name`, `version`, `language`,
`toolchain`, and optional `binding`, `dearImGuiVersion`, and
`dearImGuiIdentity`. `protocol_error` has required `code`, `message`, and
`recovery`, plus optional `details` object.

- [ ] **Step 3: Define the exact envelope schema and vectors**

The schema requires `protocol`, `type`, `messageId`, `timestamp`, and `payload`;
allows optional `correlationId` and `sessionId`; uses `oneOf` keyed by `type` to
validate each payload; sets `additionalProperties: true` only on the envelope
and payload records; and rejects additional fields inside `protocol_version`.
UUIDs use `format: uuid`; timestamps use `format: date-time`; the wire strings
are UTF-8.

Use fixed IDs and timestamps in vectors:

```json
{
  "protocol": { "major": 1, "minor": 0 },
  "type": "hello",
  "messageId": "11111111-1111-4111-8111-111111111111",
  "timestamp": "2026-07-15T12:00:00.000Z",
  "payload": {
    "role": "host",
    "implementation": {
      "name": "DearStory.ProtocolProbe.DotNet",
      "version": "0.1.0",
      "language": "csharp",
      "toolchain": ".NET 10.0",
      "binding": "ImGui.NET 1.91.6.1",
      "dearImGuiVersion": "1.91.6",
      "dearImGuiIdentity": "ImGui.NET/ImGui.NET@8e26803be78b344fd68834817905405b3cdffb94"
    },
    "supportedCapabilities": ["control.handshake.v1"],
    "requiredCapabilities": ["control.handshake.v1"]
  }
}
```

The invalid vector is identical except it omits `messageId`. The reject vector
uses `protocol.major_mismatch` and correlates to the hello ID.

- [ ] **Step 4: Write normative docs and ADR**

The ADR records: separate control/frame channels, uint32 little-endian length,
1 MiB maximum, UTF-8 without BOM, JSON Schema 2020-12, additive minor
evolution, generated checked-in models, and why `ImDrawData` and binary blobs
are excluded. `control-v1.md` defines every field, direction, validation rule,
error code, state transition, and one complete request/response transcript.

- [ ] **Step 5: Run contract tests**

```powershell
dotnet test .\tests\contract\protocol\DearStory.Protocol.ContractTests\DearStory.Protocol.ContractTests.csproj -m:1
```

Expected: three valid vectors pass and the missing-ID vector is rejected.

- [ ] **Step 6: Commit**

```powershell
git add protocol docs/adr docs/protocol tests/contract DearStory.slnx Directory.Packages.props
git commit -m "docs: specify control protocol handshake"
```

### Task 6: Deterministic C++ and C# model generation

**Files:**

- Create: `tools/DearStory.ProtocolGenerator/DearStory.ProtocolGenerator.csproj`
- Create: `tools/DearStory.ProtocolGenerator/Program.cs`
- Create: `tools/DearStory.ProtocolGenerator/Manifest.cs`
- Create: `tools/DearStory.ProtocolGenerator/ManifestException.cs`
- Create: `tools/DearStory.ProtocolGenerator/ModelEmitter.cs`
- Create: `tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests/*`
- Create: `tests/unit/protocol/dotnet/DearStory.ProtocolGenerator.Tests/TestManifest.cs`
- Create: `eng/generate-protocol.ps1`
- Generate: `src/protocol/cpp/include/dearstory/protocol/generated/messages.hpp`
- Generate: `src/protocol/dotnet/DearStory.Protocol/Generated/Messages.g.cs`

**Interfaces:**

- Consumes: `ModelEmitter.Emit(ProtocolManifest) -> GeneratedModels`.
- Produces: `GeneratedModels(string Cpp, string CSharp)`, CLI options
  `--manifest`, `--cpp-output`, `--csharp-output`, and `--check`.

- [ ] **Step 1: Write failing deterministic-generation tests**

```csharp
namespace DearStory.ProtocolGenerator.Tests;

public sealed class ModelEmitterTests
{
    [Fact]
    public void Emit_is_deterministic_and_contains_all_wire_types()
    {
        var manifest = ProtocolManifest.Parse(TestManifest.Valid);

        var first = ModelEmitter.Emit(manifest);
        var second = ModelEmitter.Emit(manifest);

        Assert.Equal(first, second);
        Assert.Contains("struct hello final", first.Cpp, StringComparison.Ordinal);
        Assert.Contains("public sealed record Hello", first.CSharp, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", first.CSharp, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_duplicate_message_names()
    {
        var error = Assert.Throws<ManifestException>(() =>
            ProtocolManifest.Parse(TestManifest.WithDuplicateHello));

        Assert.Equal("manifest.duplicate_message", error.Code);
    }
}
```

Expected: FAIL because the generator types do not exist.

- [ ] **Step 2: Implement strict manifest parsing**

Use `System.Text.Json` with case-sensitive property names, reject missing or
unknown manifest properties, sort messages and fields by wire name before
emitting, validate identifiers against `^[A-Za-z][A-Za-z0-9]*$`, and map only
these types:

```text
string -> std::string / string
string[] -> std::vector<std::string> / IReadOnlyList<string>
uint16 -> std::uint16_t / ushort
uuid -> std::string / Guid
object -> nlohmann::json / JsonObject
named record -> generated value type / generated record
```

Throw `ManifestException` with stable codes
`manifest.invalid_json`, `manifest.unknown_property`,
`manifest.duplicate_message`, `manifest.duplicate_field`,
`manifest.invalid_identifier`, and `manifest.unknown_type`.

`ManifestException` is a sealed exception with a documented `string Code`
property. `TestManifest` exposes complete `Valid` and `WithDuplicateHello`
JSON strings; the duplicate fixture is derived by parsing `Valid`, appending a
second complete `hello` message node, and serializing it, so the failure is
isolated to the duplicate name.

- [ ] **Step 3: Implement exact documented emitters**

The C++ output has `#pragma once`, required standard includes, namespace
`dearstory::protocol::generated`, `enum class peer_role`, documented final
structs, `std::optional<T>` for optional fields, equality operators, and the
banner `// Generated by DearStory.ProtocolGenerator. Do not edit.`.

The C# output has nullable enabled, namespace `DearStory.Protocol.Generated`,
`JsonPropertyName` on every positional property, XML comments on every public
type/property, immutable sealed records, `IReadOnlyList<string>`, `Guid`, and
the same generated banner. Generated content contains no timestamp or machine
path.

- [ ] **Step 4: Implement check mode and generate both files**

`--check` compares normalized UTF-8/LF content and exits `2` with the differing
output paths without writing. Normal mode writes atomically through a sibling
temporary file and then `File.Move(..., overwrite: true)`.

`eng/generate-protocol.ps1` passes arguments as an array to:

```powershell
dotnet run --project .\tools\DearStory.ProtocolGenerator -- `
  --manifest .\protocol\control\messages.json `
  --cpp-output .\src\protocol\cpp\include\dearstory\protocol\generated\messages.hpp `
  --csharp-output .\src\protocol\dotnet\DearStory.Protocol\Generated\Messages.g.cs
```

- [ ] **Step 5: Verify generation and check mode**

```powershell
pwsh -NoProfile -File .\eng\generate-protocol.ps1
pwsh -NoProfile -File .\eng\generate-protocol.ps1 -Check
dotnet test .\tests\unit\protocol\dotnet\DearStory.ProtocolGenerator.Tests -m:1
git diff --exit-code -- src/protocol/cpp/include/dearstory/protocol/generated src/protocol/dotnet/DearStory.Protocol/Generated
```

Expected: all pass and the final diff is empty.

- [ ] **Step 6: Commit**

```powershell
git add tools eng/generate-protocol.ps1 src/protocol/cpp/include/dearstory/protocol/generated src/protocol/dotnet/DearStory.Protocol/Generated tests/unit/protocol/dotnet DearStory.slnx
git commit -m "feat: generate protocol models for C++ and C#"
```

### Task 7: Native JSON codec and length-prefixed framing

**Files:**

- Create: `src/protocol/cpp/include/dearstory/protocol/control_envelope.hpp`
- Create: `src/protocol/cpp/include/dearstory/protocol/codec.hpp`
- Create: `src/protocol/cpp/include/dearstory/protocol/framing.hpp`
- Create: `src/protocol/cpp/src/codec.cpp`
- Create: `src/protocol/cpp/src/framing.cpp`
- Create: `tests/unit/protocol/cpp/codec_tests.cpp`
- Create: `tests/unit/protocol/cpp/framing_tests.cpp`
- Create: `tests/unit/protocol/cpp/test_vectors.hpp`
- Create: `tests/unit/protocol/cpp/test_vectors.cpp`

**Interfaces:**

- Consumes: generated `hello`, `welcome`, `reject` payloads and shared vectors.
- Produces: `decode(std::string_view) -> decode_result`,
  `encode(control_envelope const&) -> std::string`,
  `frame(std::string_view) -> std::vector<std::byte>`, and
  `frame_decoder::push(std::span<std::byte const>) -> vector<string>`.

- [ ] **Step 1: Write failing codec tests against shared vectors**

```cpp
TEST_CASE("native codec round-trips the canonical hello vector")
{
    const auto json = read_vector("hello.valid.json");
    const auto decoded = dearstory::protocol::decode(json);
    REQUIRE(decoded.has_value());
    REQUIRE(decoded->type == "hello");
    REQUIRE(std::holds_alternative<generated::hello>(decoded->payload));
    REQUIRE(json_semantically_equal(encode(*decoded), json));
}

TEST_CASE("native codec rejects an envelope without messageId")
{
    const auto decoded = dearstory::protocol::decode(
        read_vector("hello.missing-message-id.json"));
    REQUIRE_FALSE(decoded.has_value());
    REQUIRE(decoded.error().code == "protocol.invalid_envelope");
}
```

- [ ] **Step 2: Write failing framing tests**

Cover a frame split one byte at a time, two frames in one input span, zero
length, invalid UTF-8, and a declared size of `1,048,577`. The oversize case
must fail immediately after receiving the four-byte prefix and must not resize
the payload buffer.

- [ ] **Step 3: Implement the minimal native codec**

`control_envelope` contains `version protocol`, `std::string type`, UUID
strings for IDs, `std::string timestamp`, and
`std::variant<generated::hello, generated::welcome, generated::reject> payload`.
Parse with nlohmann-json only after the UTF-8 frame passes validation. Catch
library exceptions at the boundary and return `protocol_error`; never expose a
nlohmann exception in the public API. Validate UUIDs, timestamp shape, required
fields, exact payload/type pairing, and major version before constructing the
envelope.

The test helper declares
`std::string read_vector(std::string_view file_name)` and
`bool json_semantically_equal(std::string_view left, std::string_view right)`.
It locates the repository from the compile definition
`DEARSTORY_REPOSITORY_ROOT`, reads in binary mode, and compares parsed
nlohmann-json values. The CMake test target supplies the absolute source root
through that definition; production targets do not receive it.

- [ ] **Step 4: Implement bounded incremental framing**

Use a four-byte little-endian unsigned prefix and a fixed
`max_control_frame_bytes = 1'048'576`. `frame_decoder` retains at most four
prefix bytes plus the accepted payload size, emits complete UTF-8 JSON strings,
and returns `protocol.frame_too_large` or `protocol.invalid_envelope` for size
and UTF-8 failures. The decoder can recover only by constructing a new
instance after an error.

- [ ] **Step 5: Run focused native tests**

```powershell
cmake --build --preset windows-msvc-debug
ctest --test-dir .\build\windows-msvc-debug -C Debug -R "protocol_(codec|framing)" --output-on-failure
```

Expected: all shared vector, fragmentation, multi-frame, invalid UTF-8, and
oversize tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/protocol/cpp tests/unit/protocol/cpp
git commit -m "feat: add bounded native control codec"
```

### Task 8: Managed JSON codec and length-prefixed framing

**Files:**

- Create: `src/protocol/dotnet/DearStory.Protocol/ControlEnvelope.cs`
- Create: `src/protocol/dotnet/DearStory.Protocol/ProtocolError.cs`
- Create: `src/protocol/dotnet/DearStory.Protocol/ControlCodec.cs`
- Create: `src/protocol/dotnet/DearStory.Protocol/LengthPrefixedControlStream.cs`
- Create: `tests/unit/protocol/dotnet/DearStory.Protocol.Tests/ControlCodecTests.cs`
- Create: `tests/unit/protocol/dotnet/DearStory.Protocol.Tests/LengthPrefixedControlStreamTests.cs`
- Create: `tests/unit/protocol/dotnet/DearStory.Protocol.Tests/TestVectors.cs`
- Create: `tests/unit/protocol/dotnet/DearStory.Protocol.Tests/JsonSemanticComparer.cs`
- Create: `tests/unit/protocol/dotnet/DearStory.Protocol.Tests/RecordingArrayPool.cs`

**Interfaces:**

- Consumes: generated managed payloads and shared vectors.
- Produces: `ControlCodec.Decode(ReadOnlySpan<byte>) -> DecodeResult`,
  `ControlCodec.Encode(ControlEnvelope) -> byte[]`,
  `LengthPrefixedControlStream.ReadAsync(Stream, CancellationToken)`, and
  `WriteAsync(Stream, ReadOnlyMemory<byte>, CancellationToken)`.

- [ ] **Step 1: Write managed tests matching every native case**

```csharp
[Fact]
public void Decode_round_trips_canonical_hello()
{
    var json = TestVectors.ReadBytes("hello.valid.json");

    var decoded = ControlCodec.Decode(json);

    Assert.True(decoded.IsSuccess, decoded.Error?.Message);
    Assert.IsType<Hello>(decoded.Value!.Payload);
    Assert.True(JsonSemanticComparer.Equals(json, ControlCodec.Encode(decoded.Value)));
}

[Fact]
public async Task ReadAsync_rejects_size_before_renting_payload_buffer()
{
    var prefix = BitConverter.GetBytes(LengthPrefixedControlStream.MaxFrameBytes + 1);
    await using var input = new MemoryStream(prefix);
    var pool = new RecordingArrayPool<byte>();

    var error = await Assert.ThrowsAsync<ProtocolException>(
        () => LengthPrefixedControlStream.ReadAsync(input, pool, CancellationToken.None).AsTask());

    Assert.Equal("protocol.frame_too_large", error.Code);
    Assert.Empty(pool.RequestedLengths);
}
```

- [ ] **Step 2: Run focused tests and verify red**

```powershell
dotnet test .\tests\unit\protocol\dotnet\DearStory.Protocol.Tests --filter "FullyQualifiedName~ControlCodec|FullyQualifiedName~LengthPrefixed" -m:1
```

Expected: FAIL because the managed codec and stream are absent.

- [ ] **Step 3: Implement strict managed codec behavior**

Use `Utf8JsonReader` with `JsonDocumentOptions` that reject comments and
trailing commas. Resolve payload type only from the validated envelope `type`.
Use source-generated `JsonSerializerContext`, camelCase wire names, case-
sensitive matching, and `UnmappedMemberHandling.Skip` only for additive
optional fields. Convert `JsonException`, `FormatException`, and unsupported
type failures into `DecodeResult.Failure(ProtocolError)`.

Define `DecodeResult` as a sealed discriminated result with documented
`IsSuccess`, `ControlEnvelope? Value`, and `ProtocolError? Error` properties;
factory methods enforce that exactly one of value/error is present. Define
`ProtocolException` as a sealed exception with a documented `string Code`.

The test helpers have these exact signatures:

```csharp
internal static class TestVectors
{
    internal static byte[] ReadBytes(string fileName);
}

internal static class JsonSemanticComparer
{
    internal static bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right);
}

internal sealed class RecordingArrayPool<T> : ArrayPool<T>
{
    internal List<int> RequestedLengths { get; } = [];
    public override T[] Rent(int minimumLength);
    public override void Return(T[] array, bool clearArray = false);
}
```

`TestVectors` uses the same upward `DearStory.slnx` root search as the contract
test. `JsonSemanticComparer` parses both spans into `JsonDocument` and compares
their root elements. `RecordingArrayPool<T>` records each request and delegates
storage to `ArrayPool<T>.Shared`.

- [ ] **Step 4: Implement exact-length async framing**

Read exactly four prefix bytes, interpret little endian with
`BinaryPrimitives.ReadUInt32LittleEndian`, reject over 1 MiB before renting,
rent accepted payloads from `ArrayPool<byte>`, validate strict UTF-8 with
`new UTF8Encoding(false, true)`, and return buffers in `finally`. A clean EOF
before the first prefix byte returns `null`; EOF after any partial prefix or
payload throws `protocol.invalid_envelope`.

- [ ] **Step 5: Run all managed protocol tests**

```powershell
dotnet test .\tests\unit\protocol\dotnet\DearStory.Protocol.Tests -m:1
```

Expected: every shared vector and framing edge case passes.

- [ ] **Step 6: Commit**

```powershell
git add src/protocol/dotnet tests/unit/protocol/dotnet
git commit -m "feat: add bounded managed control codec"
```

### Task 9: Windows named-pipe adapters and handshake state machine

**Files:**

- Create: `src/protocol/cpp/include/dearstory/protocol/handshake.hpp`
- Create: `src/protocol/cpp/include/dearstory/protocol/windows/named_pipe_server.hpp`
- Create: `src/protocol/cpp/include/dearstory/protocol/windows/named_pipe_client.hpp`
- Create: `src/protocol/cpp/src/handshake.cpp`
- Create: `src/protocol/cpp/src/windows/named_pipe_server.cpp`
- Create: `src/protocol/cpp/src/windows/named_pipe_client.cpp`
- Create: `src/protocol/dotnet/DearStory.Protocol/HandshakeNegotiator.cs`
- Create: `src/protocol/dotnet/DearStory.Protocol/Windows/NamedPipeControlClient.cs`
- Create: `src/protocol/dotnet/DearStory.Protocol/Windows/NamedPipeControlServer.cs`
- Create: `tests/unit/protocol/cpp/handshake_tests.cpp`
- Create: `tests/unit/protocol/dotnet/DearStory.Protocol.Tests/HandshakeNegotiatorTests.cs`
- Create: `tests/integration/protocol/DearStory.Protocol.IntegrationTests/*`

**Interfaces:**

- Consumes: codecs/framing from Tasks 7 and 8.
- Produces: C++ `negotiate(control_envelope const& hello_envelope,
  handshake_policy const&) -> control_envelope`, C#
  `HandshakeNegotiator.Negotiate(ControlEnvelope helloEnvelope,
  HandshakePolicy policy) -> ControlEnvelope`, C++
  `named_pipe_server::accept(stop_token) -> pipe_connection`, C++
  `named_pipe_client::connect(std::wstring_view pipe_name, stop_token) ->
  pipe_connection`, C# `NamedPipeControlClient.ConnectAsync`, and C#
  `NamedPipeControlServer.AcceptAsync`.

- [ ] **Step 1: Write negotiation tests before transport code**

Both languages use the same table:

```text
local 1.0, remote 1.0, required shared -> welcome 1.0
local 1.3, remote 1.1, required shared -> welcome 1.1
local 1.0, remote 2.0                 -> protocol.major_mismatch
required capability absent            -> protocol.required_capability_missing
duplicate capability                   -> protocol.invalid_envelope
```

Assert accepted capabilities are the sorted intersection and the welcome
correlation ID equals the hello message ID.

- [ ] **Step 2: Implement the pure handshake state machine**

The policy contains local version, supported capabilities, implementation
identity, and an injected UUID/time provider. `negotiate` is deterministic for
the same inputs and always returns either a `welcome` or `reject` control
envelope. Successful negotiation sorts the accepted capabilities, copies the
incoming hello `messageId` into the response `correlationId`, uses the injected
UUID provider for the response `messageId` and welcome `sessionId`, and uses
the injected clock for the response timestamp. It never opens a pipe, reads the
clock directly, logs, or starts a process. Reject messages contain a recovery
action naming supported major/minor or the missing capability.

- [ ] **Step 3: Write loopback pipe tests**

Create a unique pipe name `dearstory-test-{Guid:N}`, start one server accept,
connect one client, send two framed messages back-to-back, and assert order and
correlation. Add cancellation-before-connect, peer-disconnect-mid-frame, and
second-client-rejected cases. Set a 10-second test timeout; do not use sleeps.

- [ ] **Step 4: Implement native and managed pipe adapters**

The native server owns `HANDLE`s through a move-only RAII `unique_handle`, uses
`CreateNamedPipeW` with byte mode and one instance, `ConnectNamedPipe`,
`ReadFile`, `WriteFile`, and `CancelIoEx`. Preserve `GetLastError()` in
`protocol_error.details` and close every handle on all exits. The native client
uses `CreateFileW`, `SetNamedPipeHandleState`, `ReadFile`, `WriteFile`, and
`CancelIoEx`, and it exposes the same framed read/write surface as the server
connection.

The managed client uses `NamedPipeClientStream` in asynchronous byte mode,
passes cancellation to `ConnectAsync`, uses the framing API exclusively, and
implements `IAsyncDisposable`. The managed server uses
`NamedPipeServerStream` in asynchronous byte mode, accepts exactly one client
per server instance, and exposes the same framed read/write surface. Neither
adapter performs JSON parsing itself.

- [ ] **Step 5: Run unit and integration tests**

```powershell
ctest --test-dir .\build\windows-msvc-debug -C Debug -R "protocol_(handshake|pipe)" --output-on-failure
dotnet test .\tests\integration\protocol\DearStory.Protocol.IntegrationTests -m:1
```

Expected: negotiation matrices and all pipe lifecycle cases pass without
orphaned processes or handles.

- [ ] **Step 6: Commit**

```powershell
git add src/protocol tests/unit/protocol tests/integration/protocol DearStory.slnx
git commit -m "feat: negotiate protocol over Windows named pipes"
```

### Task 10: Cross-language black-box conformance, coverage, and API docs

**Files:**

- Create: `tools/DearStory.ProtocolProbe.Cpp/CMakeLists.txt`
- Create: `tools/DearStory.ProtocolProbe.Cpp/main.cpp`
- Create: `tools/DearStory.ProtocolProbe.DotNet/DearStory.ProtocolProbe.DotNet.csproj`
- Create: `tools/DearStory.ProtocolProbe.DotNet/Program.cs`
- Create: `tests/e2e/protocol/DearStory.Protocol.E2ETests/DearStory.Protocol.E2ETests.csproj`
- Create: `tests/e2e/protocol/DearStory.Protocol.E2ETests/CrossLanguageHandshakeTests.cs`
- Create: `tests/e2e/protocol/DearStory.Protocol.E2ETests/ProcessProbe.cs`
- Create: `tests/coverage/valid-cobertura.xml`
- Create: `tests/coverage/invalid-cobertura.xml`
- Create: `tests/unit/foundation/CoverageGate.Tests.ps1`
- Create: `eng/assert-coverage.ps1`
- Create: `Doxyfile`
- Modify: `eng/test.ps1`
- Modify: `.github/workflows/ci.yml`
- Modify: `docs/protocol/control-v1.md`

**Interfaces:**

- Consumes: full bootstrap protocol implementation.
- Produces: native one-shot server CLI, managed one-shot client CLI, black-box
  conformance suite, Cobertura 80/70 gate, and generated C++/.NET API docs.

- [ ] **Step 1: Write the failing cross-language E2E test**

```csharp
[Fact(Timeout = 30_000)]
public async Task DotNet_client_negotiates_with_native_server()
{
    var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
    await using var server = ProcessProbe.StartNative("serve", "--pipe", pipeName, "--once");

    var result = await ProcessProbe.RunManagedAsync(
        "connect", "--pipe", pipeName, "--role", "host",
        "--require", "control.handshake.v1");

    Assert.Equal(0, result.ExitCode);
    Assert.Contains("WELCOME protocol=1.0", result.StandardOutput, StringComparison.Ordinal);
    Assert.Equal(0, await server.WaitForExitAsync());
}
```

`ProcessProbe` exposes
`StartNative(params string[] arguments) -> RunningProbe` and
`RunManagedAsync(params string[] arguments) -> Task<ProbeResult>`.
`RunningProbe` implements `IAsyncDisposable` and provides
`WaitForExitAsync() -> Task<int>`. `ProbeResult` is a sealed record containing
`ExitCode`, `StandardOutput`, and `StandardError`. The implementation uses
`ProcessStartInfo.ArgumentList`,
`UseShellExecute=false`, redirected UTF-8 output, and no shell command string.
On failure, assertions include executable, each argument, exit code, Win32
native error code/errno when available, syscall/operation, stdout, and stderr.

- [ ] **Step 2: Implement one-shot probes**

The native probe accepts both `serve --pipe <name> --once` and
`connect --pipe <name> --role <role> [--require <capability>]`. In server mode
it accepts exactly one connection, decodes exactly one hello, writes welcome or
reject, emits one JSON diagnostic line to stderr on failure, and returns stable
exit codes: `0 success`, `20 usage`, `21 pipe`, `22 protocol`, `23 timeout`.
In client mode it sends one hello, prints the single welcome/reject summary,
and returns the same exit-code categories.

The managed probe accepts both `connect --pipe <name> --role <role> [--require
<capability>]` and `serve --pipe <name> --once`. In client mode it sends its
actual .NET/toolchain identity, prints the single welcome/reject summary, and
returns the same exit-code categories. In server mode it accepts exactly one
connection, decodes exactly one hello, writes welcome or reject, emits one JSON
diagnostic line to stderr on failure, and returns the same exit-code
categories. Neither probe contains test-only branches.

- [ ] **Step 3: Add positive and negative black-box tests**

Test .NET client to native server for success, major mismatch, missing required
capability, malformed JSON, oversize prefix, server exit before welcome, and a
10-second timeout. Reverse the connection with the managed server/native
client mode and run the same vector table so conformance is symmetric.

- [ ] **Step 4: Write and test the coverage gate**

`assert-coverage.ps1` accepts one or more Cobertura files, sums covered/valid
lines and branches, prints exact totals, and exits `1` below 0.80 line or 0.70
branch. `CoverageGate.Tests.ps1` proves the valid fixture passes and the invalid
fixture fails with both measured percentages in stderr.

- [ ] **Step 5: Enforce docs and coverage in CI**

Configure Doxygen with `WARN_AS_ERROR=YES`, `EXTRACT_ALL=NO`, and only public
C++ headers as input. C# builds already generate XML documentation and fail on
missing public comments. CI installs the two pinned Windows tools with:

```powershell
choco install opencppcoverage --version=0.9.9 --yes --no-progress
choco install doxygen.install --version=1.17.0 --yes --no-progress
```

Run native coverage with:

```powershell
& "$env:ProgramFiles\OpenCppCoverage\OpenCppCoverage.exe" `
  --quiet `
  --sources "$pwd\src\protocol\cpp" `
  --export_type "cobertura:$pwd\artifacts\coverage\native.xml" `
  -- "$pwd\artifacts\bin\native\Release\dearstory-protocol-cpp-tests.exe"
```

Run managed coverage with:

```powershell
dotnet test .\DearStory.slnx -c Release --no-build -m:1 `
  --collect:"XPlat Code Coverage" `
  --results-directory .\artifacts\coverage\managed
```

Normalize the discovered managed `coverage.cobertura.xml` path, pass it with
the native file to `assert-coverage.ps1`, and upload logs, test results,
coverage, and generated API docs with `if: always()`.

Run generation in check mode before build. The final CI order is doctor,
generation check, build, unit, contract, integration, E2E, coverage gate, docs,
and artifact upload.

- [ ] **Step 6: Run the complete local verification**

```powershell
pwsh -NoProfile -File .\eng\doctor.ps1
pwsh -NoProfile -File .\eng\generate-protocol.ps1 -Check
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release -Coverage
doxygen .\Doxyfile
git diff --check
git status --short
```

Expected: every test layer passes, line coverage is at least 80 percent, branch
coverage is at least 70 percent, Doxygen emits no warnings, generation has no
diff, `git diff --check` is empty, and only the intended plan implementation
files remain uncommitted before the final commit.

- [ ] **Step 7: Commit**

```powershell
git add tools tests/e2e tests/coverage tests/unit/foundation eng Doxyfile .github/workflows/ci.yml docs/protocol DearStory.slnx CMakeLists.txt
git commit -m "test: prove cross-language protocol handshake"
```

## Plan acceptance checklist

- [ ] A documented clean Windows checkout can diagnose prerequisites without
  changing the machine.
- [ ] Native and managed builds use warnings as errors and locked dependencies.
- [ ] One manifest deterministically generates documented C++ and C# models.
- [ ] Both codecs pass the same positive and negative JSON vectors.
- [ ] Both framers reject oversize input before payload allocation.
- [ ] Named-pipe cancellation, disconnect, ordering, and handle cleanup pass.
- [ ] Major mismatch and missing required capability yield stable rejections.
- [ ] Real C++ and .NET processes negotiate in both client/server directions.
- [ ] Protocol line coverage is at least 80 percent and branch coverage at
  least 70 percent.
- [ ] Public API documentation and every test layer pass on `windows-2022`.
- [ ] CI artifacts retain enough command, process, stdout/stderr, OS error, and
  correlation information to diagnose a failed handshake.

Completing this checklist authorizes writing the Core Story Model and Schemas
implementation plan; it does not authorize pulling host rendering, catalog UI,
or documentation-builder code into this bootstrap.
