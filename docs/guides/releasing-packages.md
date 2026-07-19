# Releasing DearStory packages

## Versioning

DearStory's library products are pre-1.0, so version changes may include
breaking changes when the contract requires them. A release uses one coherent
version for every public artifact. The public release version is declared only
in `eng/version.json`; update that file in a reviewed PR before releasing.
The build, package, and release scripts derive their version from this file, so
do not maintain a separate CMake version for the public release.

## Canonical version source

The public release version is declared only in `eng/version.json`.

The public product boundary is intentionally small:

- .NET: `DearStory.Protocol`, `DearStory.Core`, `DearStory.Sdk`, and
  `DearStory.Sdk.Generator`.
- C++: `DearStory::ProtocolCpp`, `DearStory::CoreCpp`, and
  `DearStory::SdkCpp` from the `DearStory` CMake package.

Runner, Catalog, Host, Capture, Docs, Transport.Windows, and static-docs
tooling are internal Windows-first runtime products. Do not publish them as
part of the library release, and do not introduce them as dependencies of the
public package surfaces.

The public release artifacts are exactly:

- `DearStory.Protocol.<version>.nupkg`
- `DearStory.Core.<version>.nupkg`
- `DearStory.Sdk.<version>.nupkg`
- `DearStory.Sdk.Generator.<version>.nupkg`
- `DearStory-cpp-<version>-windows-msvc-x64.zip`, the public C++ archive

The C++ archive and the four C#/.NET packages are first-class public library
surfaces. They remain independent of the internal Windows-first runtime layer.

## Release workflow entrypoints

DearStory release automation runs through `.github/workflows/release.yml`.
After `eng/version.json` is merged to `main`, maintainers can use either
entrypoint:

- Automatic tag release: push a tag named `vX.Y.Z`. The workflow checks that
  the tag matches the version in the selected commit's `eng/version.json`.
- Manual release: start the `release` workflow with the required
  `workflow_dispatch` inputs `ref` and `version`. The workflow checks that
  `version` matches `eng/version.json` and that `ref` resolves to a commit
  reachable from `origin/main`.

Both paths validate the source commit, build the same coordinated release unit,
and use the version from `eng/version.json` for its package and archive names.

## Atomic release behavior

The workflow treats the public artifacts as one atomic product unit. The GitHub
Release is created or retained as a `draft` until all of these files are
present:

- `DearStory.Protocol.0.1.0.nupkg`
- `DearStory.Core.0.1.0.nupkg`
- `DearStory.Sdk.0.1.0.nupkg`
- `DearStory.Sdk.Generator.0.1.0.nupkg`
- `DearStory-cpp-0.1.0-windows-msvc-x64.zip`
- `SHA256SUMS`
- `release-manifest.json`

The four NuGet packages are published and verified against the release unit,
then the public C++ archive, `SHA256SUMS`, and `release-manifest.json` are
uploaded. Only after all coordinated publication steps succeed does the
workflow change the GitHub Release from `draft` to published. If any step
fails, the GitHub Release remains in `draft` rather than presenting a partial
public release.

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

For local release-unit validation, use the release script after the build and
test gates:

```powershell
$version = (& .\eng\read-version.ps1).Version
$commit = (git rev-parse HEAD).Trim()
pwsh -NoProfile -File .\eng\release.ps1 -ReleaseMode Local `
  -ExpectedVersion $version -SourceRef refs/heads/feature/phase-3-release-automation `
  -SourceCommit $commit -SkipBuild -SkipTest
```

The local command regenerates the same versioned release unit that the GitHub
workflow uploads. Do not publish individual packages or a C++ archive outside
that coordinated product unit.
