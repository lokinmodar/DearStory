# Capture and visual regression architecture

## Responsibilities

- `DearStory.Capture` owns backend policy, artifact layout, RGBA-to-PNG
  conversion, baseline comparison, manifest emission, and approval rules.
- `DearStory.Runner` owns host launch, session orchestration, and real RGBA
  frame acquisition from the official C++ and .NET hosts.
- `DearStory.Catalog` owns user-facing capture workflow state and presentation
  only.

## Truth sources

- WARP output is canonical.
- GPU output is diagnostic.
- Repository baselines are reviewed artifacts, not transient outputs.

## Flow

1. The runner resolves workspace and story selection.
2. `RunnerHostCaptureAdapter` captures RGBA frames from the host that publishes
   the requested story.
3. `VisualCaptureService` writes actual PNGs, optionally promotes approved WARP
   output into `tests/visual/windows/baselines`, compares against the canonical
   baseline, and emits `capture-results.json`.
4. `dearstory build` copies screenshots and manifests into `artifacts/docs`.
5. `dearstory dev` can run one-shot capture without entering the interactive
   supervision loop, while the catalog tracks pending/completed capture state.
