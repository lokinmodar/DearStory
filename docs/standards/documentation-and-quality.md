# Documentation and quality policy

## Status

This document is canonical for the DearStory repository as of July 16, 2026.

## Why this exists

DearStory is intended to stay public, cross-language, and maintainable under long-lived evolution. That only works if rationale, contracts, and code intent are documented as part of delivery rather than as cleanup after the fact.

## Required documentation layers

Every substantive feature or subsystem change must update the relevant layers below.

### 1. Contract docs

Update Markdown under `docs/` whenever a change affects:

- protocol shape or semantics;
- operational behavior visible to users or contributors;
- architecture boundaries or supported platforms;
- quality gates, build expectations, or maintenance rules;
- static-docs output shape, screenshot expectations, or Doc Block syntax.

### 2. Code docs

- Public C++ APIs require Doxygen comments.
- Public C# APIs require XML documentation.
- Public documentation warnings are treated as build failures.
- Internal comments are required when behavior is non-obvious, stateful, or easy to misuse.

### 3. Rationale docs

When a change fixes or introduces an architectural constraint, capture the reason in:

- an ADR when the decision is durable and project-wide; or
- an architecture/standards document when it explains a subsystem or policy.

### 4. Diagrams

Use Mermaid or equivalent lightweight diagrams when structure is easier to understand visually than linearly. Diagrams are expected for:

- cross-process flows;
- protocol boundaries;
- build or coverage pipelines with multiple consumers;
- ownership or dependency structure spanning three or more components.

## Coverage policy

Coverage gates in this repository measure hand-authored runtime logic, not generated artifacts.

Current implemented scope:

- native runtime coverage counts hand-authored `.cpp` implementation files under:
  - `src/protocol/cpp/src`
  - `src/core/cpp/src`
  - `sdk/cpp/src`
- managed runtime coverage counts hand-authored `.cs` files under:
  - `src/catalog/dotnet/DearStory.Catalog`
  - `src/protocol/dotnet/DearStory.Protocol`
  - `src/core/dotnet/DearStory.Core`
  - `src/docs/dotnet/DearStory.Docs`
  - `src/runner/dotnet/DearStory.Runner`
  - `sdk/dotnet/DearStory.Sdk`
  - `sdk/dotnet/DearStory.Sdk.Generator`
  - `src/transports/dotnet/DearStory.Transport.Windows`
- generated `*.g.cs`, generated protocol C++ headers, and protocol generator implementation are excluded from coverage gates;
- generated code remains protected by regeneration checks, contract tests, and E2E conformance tests.

Coverage thresholds are minimum gates, not targets.

## Build and test expectations

No implementation is considered complete until all of the following are green for the affected scope:

- deterministic generation check;
- native and managed compilation with warnings as errors;
- unit/integration/E2E suites relevant to the change;
- coverage gate where applicable;
- documentation generation where applicable;
- static-docs generation where applicable;
- canonical verification through `eng/build.ps1` and `eng/test.ps1`;
- `git diff --check`.

## Review expectations

Code review is expected to reject changes that:

- introduce public API without documentation;
- change behavior without updating the relevant Markdown contract/rationale;
- add generated files without a deterministic regeneration path;
- weaken separation between protocol contract and language-specific host/runtime concerns.

## Platform policy

DearStory is Windows-first in the active implementation plans. Other platforms remain backlog work until explicitly planned and documented. Windows-first does not justify hard-coding CLR-specific or C++-ABI-specific assumptions into shared contracts.
