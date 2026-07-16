# Building DearStory on Windows

DearStory is a Windows-first repository. Prepare these native prerequisites before building:

- Visual Studio 2022 Build Tools 17.10 or newer with Desktop development with C++
- .NET 10 SDK
- CMake 3.30 or newer
- Git 2.45 or newer
- PowerShell 7.4 or newer
- A local `vcpkg` checkout referenced by `VCPKG_ROOT`

Run `pwsh -NoProfile -File .\eng\doctor.ps1 -Json` from a clean checkout to inspect the current machine state before building.

This repository never creates WSL or WSL2 for you. Any system-wide installation remains an explicit user decision.
