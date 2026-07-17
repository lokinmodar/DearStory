# ADR 0003: Windows host baseline uses one runner-plus-catalog executable with isolated hosts

## Status

Accepted on July 17, 2026.

## Context

DearStory needs a first vertical slice that proves the end-to-end Windows
workflow without collapsing language boundaries. The same product must support
an official native C++ host, an official .NET host, deterministic captures,
and a unified catalog without assuming a shared ABI or a C#-only ecosystem.

## Decision

DearStory's first executable baseline uses:

- one Windows runner-plus-catalog executable;
- one or more isolated language host processes;
- named pipes for control traffic;
- shared memory carrying RGBA8 frames for preview and capture;
- a unified catalog assembled from merged host story indexes.

The runner owns workspace configuration, builder orchestration, supervision,
restart policy, diagnostics, and deterministic capture coordination. Hosts own
Dear ImGui contexts, rendering, story execution, and frame publication.

## Consequences

### Positive

- C++ and .NET remain first-class without pretending they share one ABI.
- Host crashes do not have to terminate the catalog process.
- The catalog can standardize controls, docs, and diagnostics above a stable
  wire contract.
- Later Windows transport optimizations can preserve the same public control
  contract.

### Negative

- The baseline introduces cross-process transport complexity before embedded
  mode exists.
- Project scaffolding must account for multiple executables and transport
  libraries early.
- Build, supervision, and diagnostics become first-slice requirements rather
  than optional tooling.

## Rejected alternatives

### Single-process mixed-language runtime

Rejected because it couples the first slice to one runtime boundary and weakens
crash isolation.

### Transferring Dear ImGui internal draw data across processes

Rejected because Dear ImGui does not offer a stable ABI for that data and the
renderer/texture ownership model is host-specific.
