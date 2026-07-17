# ADR 0004: Visual capture and canonical baselines

Date: 2026-07-17

## Status

Accepted

## Context

DearStory now has one shared visual-capture core that both `dearstory build`
and `dearstory dev` use. The remaining decision is where canonical visual truth
lives, which backend is authoritative, and how approvals flow into versioned
artifacts without letting transient local runs overwrite reviewed output.

## Decision

- Canonical baselines live under `tests/visual/windows/baselines`.
- Only WARP output may create or replace canonical baseline files.
- GPU output is diagnostic-only and must never become canonical by default.
- `dearstory build` and `dearstory dev` both call the shared
  `DearStory.Capture` core.
- Approval is always explicit through `--approve`.

## Consequences

- CI can validate the exact same visual corpus that developers approve locally.
- Reviewed PNGs stay versioned in the repository, while actual/diff/manifest
  artifacts stay transient unless a reviewer explicitly asks to check them in.
- Build and dev remain thin shells over one capture implementation, which keeps
  policy, comparison, manifest shape, and approval semantics aligned.
