# Consuming the C++ package

Install DearStory from a configured build tree:

```powershell
cmake --install .\build\windows-msvc-debug --config Release --prefix .\artifacts\install\dearstory
```

The install prefix contains the exported `DearStoryConfig.cmake` package and
the public `DearStory::ProtocolCpp`, `DearStory::CoreCpp`, and
`DearStory::SdkCpp` targets. It is the C++ consumer artifact; the repository's
runner, host, capture, docs, and Windows transport targets are not part of this
package surface.

Configure a CMake consumer with the install prefix:

```powershell
cmake -S .\tests\consumers\cpp -B .\artifacts\build\cpp-consumer -DCMAKE_PREFIX_PATH=.\artifacts\install\dearstory
cmake --build .\artifacts\build\cpp-consumer --config Release
```

The consumer must provide DearStory's public `nlohmann_json` dependency. When
using this repository's vcpkg manifest, add its toolchain and manifest paths:

```powershell
cmake -S .\tests\consumers\cpp -B .\artifacts\build\cpp-consumer `
    -DCMAKE_PREFIX_PATH:PATH=$((Resolve-Path .\artifacts\install\dearstory).Path) `
    -DCMAKE_TOOLCHAIN_FILE:FILEPATH="$env:VCPKG_ROOT\scripts\buildsystems\vcpkg.cmake" `
    -DVCPKG_MANIFEST_DIR:PATH=$PWD
```

In the consumer `CMakeLists.txt`, find and link the exported SDK target:

```cmake
find_package(DearStory CONFIG REQUIRED)

target_link_libraries(my_app PRIVATE DearStory::SdkCpp)
```

For the canonical end-to-end proof, run
`pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release`. It installs the
configured Release build to `artifacts\install\dearstory`, sets an absolute
`CMAKE_PREFIX_PATH`, builds the external consumer, and runs its tests.
