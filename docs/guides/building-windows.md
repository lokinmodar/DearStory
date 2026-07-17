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

Use `dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- build .\examples\workspaces\windows-slice --configuration Release` to emit the current static-docs slice into `artifacts\docs`.

For Release verification with coverage, use:

- `pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release`
- `pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release -Coverage`

If OpenCppCoverage is available outside the default machine-wide location, set `DEARSTORY_OPENCPPCOVERAGE_PATH` to the executable path before running the coverage command. This is intended for non-admin local tool provisioning; CI continues to use pinned machine-level installation.

If Doxygen is not on `PATH`, invoke the resolved executable directly against `.\Doxyfile`.

This repository never creates WSL or WSL2 for you. Any system-wide installation remains an explicit user decision.
