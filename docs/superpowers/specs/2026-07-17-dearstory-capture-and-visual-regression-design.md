# DearStory Capture and Visual Regression Design

## Summary

This design defines the next Windows-first DearStory subproject after the
merged host/catalog/docs vertical slice. The subproject adds real screenshot
capture for both `dearstory build` and `dearstory dev`, introduces canonical
visual baselines and diffs, and supports explicit approval of updated
baselines.

The core constraint is determinism. D3D11 WARP is the canonical capture
backend for CI, baseline generation, and approval. GPU-backed capture is
supported for local development convenience, but it is not the truth source for
canonical baseline promotion.

## Goal

Deliver one shared Windows capture/regression core that both the build flow and
the live dev flow use to capture real frames from the official C++ and .NET
hosts, compare them to baselines, emit diffs, and approve canonical baselines.

## Non-Goals

This subproject intentionally does not include:

- scripted input automation;
- packaging or installer work;
- Linux or macOS support;
- GPU-backed canonical baselines;
- automatic enrollment of every future story into the canonical corpus.

## Acceptance Criteria

This subproject is complete when all of the following are true:

- `dearstory build` generates real screenshots for the Windows slice stories;
- `dearstory dev` can trigger real capture from both CLI and catalog UI;
- both the C++ host and the .NET host can participate in the capture flow;
- baseline, diff, and explicit approval work end-to-end;
- canonical baselines are versioned in the repository;
- experimental and transient artifacts are kept outside the repository by
  default;
- CI validates the canonical corpus using WARP;
- build and dev both call the same capture/regression core rather than separate
  pipelines.

## Recommended Approach

Use one shared capture/regression core with two thin entrypoints:

- the `dearstory build` flow for batch capture, docs generation, and
  deterministic CI validation;
- the `dearstory dev` flow for on-demand preview capture through both UI and
  CLI.

This is preferred over separate `build` and `dev` pipelines because it keeps
backend selection, artifact semantics, diff behavior, and approval rules in one
place. It is also preferred over a larger “capture plus input automation plus
packaging” cycle because that would couple three independent subsystems and
inflate risk without improving the capture foundation.

## Architecture

### 1. Shared capture/regression core

The capture/regression core owns:

- backend selection (`warp` versus local `gpu`);
- story/session capture orchestration;
- artifact path resolution;
- actual/baseline/diff result classification;
- approval and promotion rules;
- metadata emission for captures and comparisons.

The core is responsible for policy. It decides whether a capture is canonical,
whether it is eligible for approval, and how artifacts are persisted.

### 2. Thin front doors

`dearstory build` and `dearstory dev` remain thin callers:

- `dearstory build` resolves a capture corpus, performs batch capture, updates
  docs screenshots, compares against baselines, and emits structured results;
- `dearstory dev` issues targeted capture requests against the currently active
  preview session through either CLI or catalog UI actions.

Both surfaces call the same internal capture contract.

### 3. Host capture adapters

Each official host gets a small adapter layer that exposes a stable capture
contract to the core:

- C++ host adapter;
- .NET host adapter.

Their responsibility is limited to producing a stable RGBA frame for a selected
story and reporting capture diagnostics. They do not decide baseline policy or
artifact semantics.

### 4. Artifact storage model

Artifacts are split into two classes.

Canonical artifacts:

- versioned in the repository;
- generated and approved only through WARP;
- used by CI and reviewed baseline updates.

Experimental artifacts:

- written outside the repository by default;
- may come from WARP or GPU capture;
- used for local inspection, ad hoc debugging, and proposed updates.

Each capture writes metadata recording at least:

- story identifier;
- host kind;
- backend kind;
- output dimensions;
- pixel format;
- timestamp;
- hashes of relevant files;
- classification result (`match`, `mismatch`, `missing-baseline`,
  `backend-mismatch`, `capture-fault`, and similar).

## Canonical Corpus Selection

The canonical visual corpus is explicit, not implicit.

Selection uses both:

- story-level metadata; and
- workspace-level overrides in `dearstory.toml`.

The resulting corpus is the resolved policy view, not simply “all official
stories.” This allows the project to keep a small reviewed canonical set while
still supporting experimental stories and local-only capture.

The initial recommended canonical set for the first execution plan is the
existing Windows-slice stories, but the mechanism must be generic from the
start.

## Backend Policy

### WARP

WARP is the canonical backend for:

- CI execution;
- baseline generation;
- baseline approval;
- deterministic review artifacts.

### GPU

GPU-backed capture is supported for:

- local `dearstory dev` convenience;
- local validation and exploratory comparison;
- non-canonical capture artifacts.

GPU capture is never treated as canonical by default. Promotion to a repository
baseline must rerun under WARP before approval.

## Data Flow

### `dearstory build`

1. Resolve the canonical or requested corpus.
2. Start the required host processes.
3. Capture one real frame per selected story.
4. Write screenshot artifacts for static docs.
5. Compare actual images against existing baselines.
6. Emit diff artifacts and structured manifests.
7. Optionally approve/promote only when explicitly requested.

### `dearstory dev`

1. User triggers capture through CLI or catalog UI.
2. The current preview session is captured through the same core.
3. The chosen backend is honored (`warp` or local `gpu`).
4. Actual/diff artifacts and manifests are written.
5. If the user requests canonical approval, the promotion path re-enters WARP
   before baseline replacement.

## Failure Handling

Capture failure states are first-class results, not generic crashes.

Important cases:

- missing baseline;
- host timeout during capture;
- host fault while warming up or presenting a frame;
- backend mismatch against canonical policy;
- capture request against a story outside the resolved canonical corpus.

These outcomes should appear as structured diagnostics and machine-readable
result states so that CLI, catalog UI, and CI can all surface them consistently.

## Testing Strategy

### Unit tests

- corpus resolution from story metadata plus workspace overrides;
- backend selection rules;
- artifact path and metadata generation;
- diff/baseline result classification;
- approval guardrails.

### Integration tests

- C++ host capture through the runner path;
- .NET host capture through the runner path;
- WARP capture end-to-end;
- workspace override behavior;
- docs screenshot generation through the build path.

### End-to-end tests

- `dearstory build` writes real screenshots, diffs, and manifests;
- `dearstory dev` capture works from CLI and catalog UI;
- explicit approval promotes an actual capture to a canonical baseline using
  WARP policy.

## Approval Model

Approval is always explicit.

The system must never overwrite canonical baselines automatically. A capture may
produce:

- an actual image;
- a diff image when comparison is possible;
- a result manifest.

Promotion of an actual image to a canonical baseline requires:

- an explicit command or UI action;
- WARP output;
- membership in the resolved canonical corpus;
- matching baseline contract semantics for host kind and dimensions.

## Decomposition Recommendation

This work should be executed as its own subproject before input automation or
packaging.

Recommended order after this subproject:

1. input automation and deterministic interaction scenarios;
2. packaging and local distribution polish for the Windows runner/catalog.

This sequence keeps the visual foundation stable before adding scenario
automation or distribution concerns.
