# Releasing DearStory packages

## Versioning

DearStory's library products are pre-1.0, so version changes may include
breaking changes when the contract requires them. A release must use one
coherent version for the repository's public .NET packages and the exported
CMake package. The current CMake project version is the source of the C++
package version; keep it aligned with the package versions selected for the
release.

The public product boundary is intentionally small:

- .NET: `DearStory.Protocol`, `DearStory.Core`, `DearStory.Sdk`, and
  `DearStory.Sdk.Generator`.
- C++: `DearStory::ProtocolCpp`, `DearStory::CoreCpp`, and
  `DearStory::SdkCpp` from the `DearStory` CMake package.

Runner, Catalog, Host, Capture, Docs, Transport.Windows, and static-docs
tooling are internal Windows-first runtime products. Do not publish them as
part of the library release, and do not introduce them as dependencies of the
public package surfaces.

## .NET package publishing

Build the Release configuration, then produce the package artifacts:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release
```

`eng\pack.ps1` packs exactly `DearStory.Protocol`, `DearStory.Core`,
`DearStory.Sdk`, and `DearStory.Sdk.Generator`. It writes `.nupkg` files to
`artifacts\packages\dotnet` and copies them to
`artifacts\packages\local-feed`. Treat `dotnet` as the publishing source and
`local-feed` as the local-consumer validation source.

After the verification gates pass, publish the `.nupkg` files from
`artifacts\packages\dotnet` to NuGet under the release tag's version. Publish
only the four public packages, and retain the generated artifacts associated
with the tag so a release can be audited or reproduced.

## C++ install artifact publishing

Create the C++ install tree from the configured Release build:

```powershell
cmake --install .\build\windows-msvc-debug --config Release --prefix .\artifacts\install\dearstory
```

Archive the contents of `artifacts\install\dearstory` as the C++ package
artifact and attach that archive to the tagged release. Consumers unpack the
archive and pass its extracted root through `CMAKE_PREFIX_PATH`, for example:

```powershell
cmake -S .\consumer -B .\consumer-build -DCMAKE_PREFIX_PATH:PATH=C:\path\to\dearstory
```

The consumer must supply the public `nlohmann_json` dependency. The repository
consumer proof uses its vcpkg manifest and toolchain; see
[consuming the C++ package](consuming-cpp-package.md) for the exact command.

## Verification gates

Before publishing a tagged release, run the canonical sequence from a clean
Release build:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- build .\examples\workspaces\windows-slice --configuration Release
git diff --check
```

`eng\test.ps1` is the package-consumer gate: it recreates the local NuGet
feed, restores and tests the .NET smoke consumer, installs the C++ targets,
configures an external CMake consumer with `CMAKE_PREFIX_PATH`, builds it, and
runs its tests. The runner command keeps the current Windows static-docs slice
covered without making that internal runtime tool a public library dependency.

Run `pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release`, verify
`eng\test.ps1 -Configuration Release`, then publish the `.nupkg` files and
attach the installed C++ package archive to the tagged release.
