# DearStory Library Surface and Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make DearStory consumable as a reusable library in both C++ and C# by productizing the shared protocol/core/SDK layers, proving package-based consumption, and keeping Windows-first runtime tooling outside the public package boundary.

**Architecture:** Productize the library surface first. Package only the shared protocol/core/SDK layers, keep runner/catalog/hosts/transports internal, and add external-style consumer smoke tests that install or reference produced artifacts instead of internal project references. Use one shared pre-1.0 version policy, one pack script, and one CI artifact path so later release automation can build on verified package boundaries.

**Tech Stack:** C++20, CMake 3.30+, vcpkg manifest mode, MSVC 19.40+, .NET 10, Roslyn incremental generators, PowerShell 7, xUnit.net v3, Catch2 3.15.2, GitHub Actions on `windows-2022`, NuGet package metadata, CMake package config/version generation.

## Global Constraints

- DearStory remains Dear ImGui-first and language-neutral.
- The C++ SDK and the C# SDK remain first-class surfaces.
- Public APIs require Doxygen or XML comments.
- Build/test/docs verification stays mandatory through `eng/build.ps1` and `eng/test.ps1`.
- Windows-first implementation does not justify Windows-specific assumptions in shared contracts or public library APIs.
- Public packages for phase 2 are `.NET`: `DearStory.Protocol`, `DearStory.Core`, `DearStory.Sdk`, `DearStory.Sdk.Generator`; `C++`: `DearStory::ProtocolCpp`, `DearStory::CoreCpp`, `DearStory::SdkCpp`.
- Internal Windows-first tooling (`Runner`, `Catalog`, `Host`, `Transport.Windows`, `Capture`, `Docs`) must not become dependencies of packaged libraries.

---

## File structure

### Library packaging and metadata

- Modify: `Directory.Build.props`
- Modify: `README.md`
- Modify: `sdk/dotnet/DearStory.Sdk/DearStory.Sdk.csproj`
- Modify: `sdk/dotnet/DearStory.Sdk.Generator/DearStory.Sdk.Generator.csproj`
- Modify: `src/core/dotnet/DearStory.Core/DearStory.Core.csproj`
- Modify: `src/protocol/dotnet/DearStory.Protocol/DearStory.Protocol.csproj`
- Create: `docs/guides/consuming-dotnet-packages.md`
- Create: `docs/guides/consuming-cpp-package.md`
- Create: `docs/architecture/library-product-boundary.md`

### Pack/build orchestration

- Create: `eng/pack.ps1`
- Modify: `eng/build.ps1`
- Modify: `eng/test.ps1`
- Modify: `.github/workflows/ci.yml`

### C++ install/export package

- Modify: `CMakeLists.txt`
- Modify: `src/protocol/cpp/CMakeLists.txt`
- Modify: `src/core/cpp/CMakeLists.txt`
- Modify: `sdk/cpp/CMakeLists.txt`
- Create: `cmake/DearStoryConfig.cmake.in`
- Create: `cmake/DearStoryConfigVersion.cmake.in`

### Consumer proof

- Create: `tests/consumers/dotnet/DearStory.Consumer.Smoke/DearStory.Consumer.Smoke.csproj`
- Create: `tests/consumers/dotnet/DearStory.Consumer.Smoke/ButtonStories.cs`
- Create: `tests/consumers/dotnet/DearStory.Consumer.Smoke/PackageConsumptionTests.cs`
- Create: `tests/consumers/cpp/CMakeLists.txt`
- Create: `tests/consumers/cpp/main.cpp`
- Create: `tests/consumers/cpp/package_consumption_tests.cpp`

### Plan-facing contract outputs

- Produce: local `.nupkg` outputs for public .NET packages
- Produce: local C++ install prefix containing exported `DearStory` CMake package
- Produce: CI artifacts for package inspection

## Task 1: Define the public package boundary and shared package metadata

**Files:**
- Modify: `Directory.Build.props`
- Modify: `README.md`
- Modify: `src/protocol/dotnet/DearStory.Protocol/DearStory.Protocol.csproj`
- Modify: `src/core/dotnet/DearStory.Core/DearStory.Core.csproj`
- Modify: `sdk/dotnet/DearStory.Sdk/DearStory.Sdk.csproj`
- Modify: `sdk/dotnet/DearStory.Sdk.Generator/DearStory.Sdk.Generator.csproj`
- Create: `docs/architecture/library-product-boundary.md`
- Test: `dotnet build .\DearStory.slnx --no-restore -c Release -warnaserror`

**Interfaces:**
- Consumes: current protocol/core/SDK project references and repository documentation rules.
- Produces: package IDs `DearStory.Protocol`, `DearStory.Core`, `DearStory.Sdk`, `DearStory.Sdk.Generator`, shared version metadata, and explicit internal-vs-public product boundary docs.

- [ ] **Step 1: Write the failing metadata check**

Create a note-driven regression in `docs/architecture/library-product-boundary.md` that lists the expected package set and the prohibition on internal Windows-first dependencies.

```markdown
# DearStory library product boundary

Public .NET packages in phase 2:

- DearStory.Protocol
- DearStory.Core
- DearStory.Sdk
- DearStory.Sdk.Generator

These packages must not depend on Runner, Catalog, Host, Capture, Docs, or Transport.Windows.
```

- [ ] **Step 2: Run the build to confirm package metadata is still absent or incomplete**

Run: `dotnet build .\DearStory.slnx --no-restore -c Release -warnaserror`

Expected: PASS build, but no `PackageId`, `PackageLicenseExpression`, `PackageReadmeFile`, or `IsPackable` metadata exists yet in the public package projects.

- [ ] **Step 3: Add shared package metadata and package IDs**

Update `Directory.Build.props` to centralize pre-1.0 package metadata and make public-package projects opt in cleanly.

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <VersionPrefix>0.1.0-alpha</VersionPrefix>
    <Authors>Dante</Authors>
    <Company>Dante</Company>
    <RepositoryUrl>https://github.com/lokinmodar/DearStory</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageProjectUrl>https://github.com/lokinmodar/DearStory</PackageProjectUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
  </PropertyGroup>
</Project>
```

Update the public package projects with explicit IDs and packability:

```xml
<PropertyGroup>
  <PackageId>DearStory.Sdk</PackageId>
  <Description>Thin DearStory authoring SDK for ImGui.NET stories.</Description>
  <IsPackable>true</IsPackable>
</PropertyGroup>
```

- [ ] **Step 4: Make the analyzer package behave like an analyzer package**

In `sdk/dotnet/DearStory.Sdk.Generator/DearStory.Sdk.Generator.csproj`, set package metadata and analyzer output intent.

```xml
<PropertyGroup>
  <PackageId>DearStory.Sdk.Generator</PackageId>
  <Description>Source generator and analyzer for DearStory .NET story registration.</Description>
  <IsPackable>true</IsPackable>
  <IncludeBuildOutput>true</IncludeBuildOutput>
</PropertyGroup>
```

- [ ] **Step 5: Rebuild and verify**

Run: `dotnet build .\DearStory.slnx --no-restore -c Release -warnaserror`

Expected: PASS with the public package metadata in place and no warnings/errors added.

- [ ] **Step 6: Commit**

```bash
git add Directory.Build.props README.md src/protocol/dotnet/DearStory.Protocol/DearStory.Protocol.csproj src/core/dotnet/DearStory.Core/DearStory.Core.csproj sdk/dotnet/DearStory.Sdk/DearStory.Sdk.csproj sdk/dotnet/DearStory.Sdk.Generator/DearStory.Sdk.Generator.csproj docs/architecture/library-product-boundary.md
git commit -m "feat: define DearStory public package boundary"
```

## Task 2: Add a pack pipeline and prove .NET package consumption

**Files:**
- Create: `eng/pack.ps1`
- Create: `tests/consumers/dotnet/DearStory.Consumer.Smoke/DearStory.Consumer.Smoke.csproj`
- Create: `tests/consumers/dotnet/DearStory.Consumer.Smoke/ButtonStories.cs`
- Create: `tests/consumers/dotnet/DearStory.Consumer.Smoke/PackageConsumptionTests.cs`
- Modify: `eng/test.ps1`
- Create: `docs/guides/consuming-dotnet-packages.md`
- Test: `pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release`

**Interfaces:**
- Consumes: public package IDs and shared version metadata from Task 1.
- Produces: local `.nupkg` artifacts plus a package-based .NET smoke consumer proving `[Story]`, `[StoryArg]`, `StoryContext`, and generator-based registration work through `PackageReference`.

- [ ] **Step 1: Write the failing consumer package reference**

Create `tests/consumers/dotnet/DearStory.Consumer.Smoke/DearStory.Consumer.Smoke.csproj` with package references that point to a local feed path to be supplied by `eng/pack.ps1`.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RestoreSources>$(RestoreSources);$(DearStoryLocalFeed)</RestoreSources>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="DearStory.Sdk" Version="0.1.0-alpha*" />
    <PackageReference Include="DearStory.Sdk.Generator" Version="0.1.0-alpha*" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Run pack to confirm the local feed does not exist yet**

Run: `pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release`

Expected: FAIL because `eng/pack.ps1` and the local feed/artifact layout do not exist yet.

- [ ] **Step 3: Implement the pack script**

Create `eng/pack.ps1` to:

- create `artifacts\packages\dotnet`
- run `dotnet pack` for `DearStory.Protocol`, `DearStory.Core`, `DearStory.Sdk`, `DearStory.Sdk.Generator`
- create `artifacts\packages\local-feed`
- copy produced `.nupkg` files into that feed

```powershell
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$packages = @(
    '.\src\protocol\dotnet\DearStory.Protocol\DearStory.Protocol.csproj',
    '.\src\core\dotnet\DearStory.Core\DearStory.Core.csproj',
    '.\sdk\dotnet\DearStory.Sdk\DearStory.Sdk.csproj',
    '.\sdk\dotnet\DearStory.Sdk.Generator\DearStory.Sdk.Generator.csproj'
)
```

- [ ] **Step 4: Add the smoke consumer source and test**

Create `ButtonStories.cs` with a minimal package-based story:

```csharp
using DearStory.Sdk;

public sealed class PrimaryButtonArgs
{
    [StoryArg("label")]
    public string Label { get; init; } = "Save";
}

public static class ButtonStories
{
    [Story("buttons/package-smoke", typeof(PrimaryButtonArgs))]
    public static void PrimaryButton(StoryContext context)
    {
        _ = context.Args;
    }
}
```

Create `PackageConsumptionTests.cs`:

```csharp
using Xunit;

public sealed class PackageConsumptionTests
{
    [Fact]
    public void Story_type_is_loadable_from_packaged_sdk()
    {
        Assert.Equal("ButtonStories", typeof(ButtonStories).Name);
    }
}
```

- [ ] **Step 5: Run the pack flow and the package-based consumer**

Run:

```powershell
pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release
$env:DearStoryLocalFeed = (Resolve-Path .\artifacts\packages\local-feed).Path
dotnet test .\tests\consumers\dotnet\DearStory.Consumer.Smoke\DearStory.Consumer.Smoke.csproj -c Release
```

Expected: PASS. The consumer restores from the local feed and builds against packages instead of project references.

- [ ] **Step 6: Commit**

```bash
git add eng/pack.ps1 eng/test.ps1 tests/consumers/dotnet/DearStory.Consumer.Smoke docs/guides/consuming-dotnet-packages.md
git commit -m "feat: prove packaged dotnet sdk consumption"
```

## Task 3: Export and install the C++ package and prove native consumption

**Files:**
- Modify: `CMakeLists.txt`
- Modify: `src/protocol/cpp/CMakeLists.txt`
- Modify: `src/core/cpp/CMakeLists.txt`
- Modify: `sdk/cpp/CMakeLists.txt`
- Create: `cmake/DearStoryConfig.cmake.in`
- Create: `cmake/DearStoryConfigVersion.cmake.in`
- Create: `tests/consumers/cpp/CMakeLists.txt`
- Create: `tests/consumers/cpp/main.cpp`
- Create: `tests/consumers/cpp/package_consumption_tests.cpp`
- Create: `docs/guides/consuming-cpp-package.md`
- Test: `cmake --install .\build\windows-msvc-debug --config Release --prefix .\artifacts\install\dearstory`

**Interfaces:**
- Consumes: existing `DearStory::ProtocolCpp`, `DearStory::CoreCpp`, `DearStory::SdkCpp` targets.
- Produces: installable/exported CMake package `DearStoryConfig.cmake` plus a consumer that builds through `find_package(DearStory CONFIG REQUIRED)`.

- [ ] **Step 1: Write the failing external consumer**

Create `tests/consumers/cpp/CMakeLists.txt`:

```cmake
cmake_minimum_required(VERSION 3.30)
project(dearstory_consumer_smoke LANGUAGES CXX)

find_package(DearStory CONFIG REQUIRED)

add_executable(dearstory-consumer-smoke main.cpp package_consumption_tests.cpp)
target_link_libraries(dearstory-consumer-smoke PRIVATE DearStory::SdkCpp)
```

- [ ] **Step 2: Confirm the package is not installable yet**

Run:

```powershell
cmake --install .\build\windows-msvc-debug --config Release --prefix .\artifacts\install\dearstory
cmake -S .\tests\consumers\cpp -B .\artifacts\build\cpp-consumer -DCMAKE_PREFIX_PATH=.\artifacts\install\dearstory
```

Expected: FAIL because no exported `DearStoryConfig.cmake` package exists yet.

- [ ] **Step 3: Add install/export rules and config generation**

Update the C++ library CMake files with install rules:

```cmake
install(TARGETS dearstory_protocol_cpp dearstory_core_cpp dearstory_sdk_cpp
    EXPORT DearStoryTargets
    ARCHIVE DESTINATION lib
    LIBRARY DESTINATION lib
    RUNTIME DESTINATION bin)

install(DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/include/ DESTINATION include)
```

Update the root `CMakeLists.txt` with package config generation:

```cmake
include(CMakePackageConfigHelpers)
configure_package_config_file(
    ${CMAKE_SOURCE_DIR}/cmake/DearStoryConfig.cmake.in
    ${CMAKE_BINARY_DIR}/DearStoryConfig.cmake
    INSTALL_DESTINATION lib/cmake/DearStory)
```

- [ ] **Step 4: Add a native smoke consumer source**

Create `main.cpp`:

```cpp
#include <dearstory/sdk/story_registry.hpp>

int main()
{
    dearstory::sdk::story_registry registry;
    return registry.stories().empty() ? 0 : 1;
}
```

Create `package_consumption_tests.cpp`:

```cpp
#include <dearstory/sdk/story_context.hpp>

void package_surface_compiles() {}
```

- [ ] **Step 5: Install the package and build the consumer**

Run:

```powershell
cmake --build --preset windows-msvc-debug --config Release
cmake --install .\build\windows-msvc-debug --config Release --prefix .\artifacts\install\dearstory
cmake -S .\tests\consumers\cpp -B .\artifacts\build\cpp-consumer -DCMAKE_PREFIX_PATH=.\artifacts\install\dearstory
cmake --build .\artifacts\build\cpp-consumer --config Release
ctest --test-dir .\artifacts\build\cpp-consumer -C Release --output-on-failure
```

Expected: PASS. The external consumer finds the installed package and links only through exported targets.

- [ ] **Step 6: Commit**

```bash
git add CMakeLists.txt src/protocol/cpp/CMakeLists.txt src/core/cpp/CMakeLists.txt sdk/cpp/CMakeLists.txt cmake tests/consumers/cpp docs/guides/consuming-cpp-package.md
git commit -m "feat: export installable cpp dearstory package"
```

## Task 4: Wire pack/install proof into repository verification and CI

**Files:**
- Modify: `eng/build.ps1`
- Modify: `eng/test.ps1`
- Modify: `.github/workflows/ci.yml`
- Test: `pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release`

**Interfaces:**
- Consumes: pack script from Task 2 and C++ install/export package from Task 3.
- Produces: one canonical repository path that validates package generation and consumer proof in CI and local scripts.

- [ ] **Step 1: Write the failing CI expectation**

Add a comment block in `.github/workflows/ci.yml` documenting the new required artifact outputs:

```yaml
# Required release-surface artifacts:
# - .nupkg files for public .NET packages
# - local-feed copy for smoke consumption
# - installed C++ DearStory package tree
```

- [ ] **Step 2: Confirm the current scripts do not run package-consumer proof**

Run: `pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release`

Expected: PASS or FAIL without any package-consumer proof being executed; the scripts currently lack pack/install verification stages.

- [ ] **Step 3: Extend `eng/test.ps1` with package-consumption verification**

Add steps after the existing managed/native tests:

```powershell
Invoke-DearStoryCommand -Executable 'pwsh' -Arguments @('-NoProfile', '-File', '.\eng\pack.ps1', '-Configuration', $Configuration)
$env:DearStoryLocalFeed = (Resolve-Path '.\artifacts\packages\local-feed').Path
Invoke-DearStoryCommand -Executable 'dotnet' -Arguments @('test', '.\tests\consumers\dotnet\DearStory.Consumer.Smoke\DearStory.Consumer.Smoke.csproj', '-c', $Configuration)
Invoke-DearStoryCommand -Executable 'cmake' -Arguments @('--install', '.\build\windows-msvc-debug', '--config', $Configuration, '--prefix', '.\artifacts\install\dearstory')
```

- [ ] **Step 4: Add CI artifact publication**

Update `.github/workflows/ci.yml` to upload:

- `artifacts/packages/dotnet`
- `artifacts/packages/local-feed`
- `artifacts/install/dearstory`

```yaml
- name: Upload public package artifacts
  uses: actions/upload-artifact@v4
  with:
    name: dearstory-public-packages
    path: |
      artifacts/packages/dotnet
      artifacts/packages/local-feed
      artifacts/install/dearstory
```

- [ ] **Step 5: Run the full Release verification**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release
```

Expected: PASS. The repository now verifies library packaging and consumer proof as part of its canonical flow.

- [ ] **Step 6: Commit**

```bash
git add eng/build.ps1 eng/test.ps1 .github/workflows/ci.yml
git commit -m "ci: verify dearstory public package consumption"
```

## Task 5: Finish product-facing documentation and release guidance

**Files:**
- Modify: `README.md`
- Modify: `docs/guides/building-windows.md`
- Modify: `docs/standards/documentation-and-quality.md`
- Create: `docs/guides/releasing-packages.md`
- Test: `dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- build .\examples\workspaces\windows-slice --configuration Release`

**Interfaces:**
- Consumes: implemented package/install flows from Tasks 1-4.
- Produces: canonical docs for consumers and maintainers that explain how to build, pack, install, validate, and release the public DearStory library surface.

- [ ] **Step 1: Write the failing documentation checklist**

Create `docs/guides/releasing-packages.md` with the required headings only:

```markdown
# Releasing DearStory packages

## Versioning
## .NET package publishing
## C++ install artifact publishing
## Verification gates
```

- [ ] **Step 2: Confirm the current docs still describe DearStory primarily as an internal repo slice**

Run:

```powershell
Get-Content .\README.md | Select-String 'public API surface is still pre-1.0'
Get-Content .\docs\guides\building-windows.md | Select-String 'Windows-first repository'
```

Expected: PASS, showing that consumer-facing package guidance is still incomplete and needs to be added.

- [ ] **Step 3: Expand the consumer and release docs**

Document:

- how to run `eng/pack.ps1`;
- how to restore the .NET smoke consumer from the local feed;
- how to install the C++ package and set `CMAKE_PREFIX_PATH`;
- which projects are public packages and which remain internal;
- how tagged release publication will work for NuGet and C++ archives.

Example release snippet:

```markdown
Run `pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release`, verify `eng\test.ps1 -Configuration Release`, then publish the `.nupkg` files and attach the installed C++ package archive to the tagged release.
```

- [ ] **Step 4: Verify docs and static build flow still work**

Run:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- build .\examples\workspaces\windows-slice --configuration Release
git diff --check
```

Expected: PASS. Documentation changes do not break the current Windows slice and all quality gates remain green.

- [ ] **Step 5: Commit**

```bash
git add README.md docs/guides/building-windows.md docs/standards/documentation-and-quality.md docs/guides/releasing-packages.md docs/guides/consuming-dotnet-packages.md docs/guides/consuming-cpp-package.md
git commit -m "docs: document dearstory library package workflows"
```

## Self-review

- Spec coverage: Tasks 1-5 cover the public package boundary, .NET packaging, C++ install/export packaging, consumer proof, CI verification, and release-facing docs.
- Placeholder scan: No `TODO`, `TBD`, or “similar to previous task” placeholders remain.
- Type consistency: Package IDs, CMake target names, and script names are consistent across tasks.

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-18-dearstory-library-surface-and-packaging.md`. Two execution options:

1. Subagent-Driven (recommended) - I dispatch a fresh subagent per task, review between tasks, fast iteration
2. Inline Execution - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
