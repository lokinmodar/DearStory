# Building DearStory on Windows

DearStory is a Windows-first repository. Prepare these native prerequisites before building:

- Visual Studio 2022 Build Tools 17.10 or newer with Desktop development with C++
- .NET 10 SDK
- CMake 3.30 or newer
- Git 2.45 or newer
- PowerShell 7.4 or newer
- A local `vcpkg` checkout referenced by `VCPKG_ROOT`

Run `pwsh -NoProfile -File .\eng\doctor.ps1 -Json` from a clean checkout to inspect the current machine state before building.

Use `pwsh -NoProfile -File .\eng\build.ps1 -Configuration Debug` to run the native and managed build steps, and `pwsh -NoProfile -File .\eng\test.ps1 -Configuration Debug` to run the baseline native and managed test suites.

## Building and validating library packages

The repository contains Windows-first runtime tooling, but its supported
library products are consumable independently in C++ and .NET. The public .NET
packages are `DearStory.Protocol`, `DearStory.Core`, `DearStory.Sdk`, and
`DearStory.Sdk.Generator`. The public C++ targets are
`DearStory::ProtocolCpp`, `DearStory::CoreCpp`, and `DearStory::SdkCpp`.
Runner, Catalog, Host, Capture, Docs, Transport.Windows, and the static-docs
workflow remain internal products.

Build the Release configuration before creating consumer artifacts:

```powershell
pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release
```

Create the local NuGet feed with:

```powershell
pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release
```

The command writes `.nupkg` files to `artifacts\packages\dotnet` and mirrors
them to `artifacts\packages\local-feed` for the .NET smoke consumer. Run the
canonical Release verification afterwards; it restores the .NET consumer from
that local feed and installs, configures, builds, and tests the C++ consumer:

```powershell
pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release
```

For the exact consumer commands, see [consuming DearStory .NET
packages](consuming-dotnet-packages.md) and [consuming the C++
package](consuming-cpp-package.md). For tagged publication, see [releasing
DearStory packages](releasing-packages.md).

Use `dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- build .\examples\workspaces\windows-slice --configuration Release` to emit the current static-docs slice into `artifacts\docs`.

The runner resolves host artifacts against the active Debug or Release build output. `dearstory build --configuration <value>` overrides that selection explicitly; when the option is omitted, the runner uses the current process configuration so Debug and Release verification stay aligned with the built host binaries.

For Release verification with coverage, use:

- `pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release`
- `pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release -Coverage`

If OpenCppCoverage is available outside the default machine-wide location, set `DEARSTORY_OPENCPPCOVERAGE_PATH` to the executable path before running the coverage command. This is intended for non-admin local tool provisioning; CI continues to use pinned machine-level installation.

If Doxygen is not on `PATH`, invoke the resolved executable directly against `.\Doxyfile`.

This repository never creates WSL or WSL2 for you. Any system-wide installation remains an explicit user decision.
