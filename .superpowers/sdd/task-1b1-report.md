# Task 1b1 report

Date: 2026-07-17

Repo: `C:\Dante\DearStory\.worktrees\core-story-model-and-schemas`

## Scope completed

- Added `dearstory::core::story_visual_descriptor` with Doxygen comments.
- Extended `dearstory::core::story_descriptor` with `visual` metadata.
- Added `dearstory::sdk::visual_story_options` and threaded it through `story_registration::create(...)`.
- Persisted canonical visual-corpus enrollment onto the produced descriptor.
- Updated the Windows slice example to opt `Buttons/Primary` into the canonical corpus.
- Added a regression test covering explicit canonical visual enrollment.

## TDD notes

- Added the requested regression test first.
- The literal requested snippet used `dearstory::sdk::argument_metadata{}`, but this repository does not provide a default constructor for `argument_metadata`.
- To preserve the intended behavior check while keeping the test buildable, the test uses `dearstory::sdk::argument_metadata::empty()`.

## Verification run

Build command used to refresh the targeted native test binary:

```powershell
cmake --build .\build\windows-msvc-debug --config Release --target dearstory-sdk-cpp-tests
```

Required verification command:

```powershell
ctest --test-dir .\build\windows-msvc-debug -C Release -R "story_registry" --output-on-failure
```

Observed result:

- Exit code: `0`
- Reported tests: `1/1`
- Passing test: `sdk_story_registry produces a canonical descriptor without wrapping ImGui`

## Concern

The required CTest filter `-R "story_registry"` does not match the new test name `sdk_story_registration keeps explicit canonical visual enrollment`, so the requested verification slice stays green without exercising the new regression test. The binary was rebuilt successfully, but the constrained verification command does not cover the newly added case.

## Updated verification after test-name alignment

- Renamed the new Catch2 case to `sdk_story_registry keeps explicit canonical visual enrollment` so it is included by the required `-R "story_registry"` filter.

Rebuild command rerun:

```powershell
cmake --build .\build\windows-msvc-debug --config Release --target dearstory-sdk-cpp-tests
```

Verification command rerun:

```powershell
ctest --test-dir .\build\windows-msvc-debug -C Release -R "story_registry" --output-on-failure
```

Updated observed result:

- Exit code: `0`
- Reported tests: `2/2`
- Passing tests:
  - `sdk_story_registry produces a canonical descriptor without wrapping ImGui`
  - `sdk_story_registry keeps explicit canonical visual enrollment`
