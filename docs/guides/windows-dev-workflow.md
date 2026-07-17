# Windows development workflow

This guide captures the current `dearstory dev` baseline for the Windows-first
slice.

## What the dev loop owns today

- workspace loading from `dearstory.toml`;
- runner-owned catalog initialization;
- host-builder selection by workspace host entry;
- selective restart planning from changed paths;
- serializable story selection and argument preservation primitives;
- a watcher abstraction that can publish changed paths into the restart loop.

## Current host-selection rules

- paths containing `\cpp\` map to `cpp-host`;
- paths containing `\dotnet\` map to `dotnet-host`;
- all other paths are ignored by the restart planner.

## Session preservation

The runner persists:

- the selected story ID; and
- serializable arguments addressed by dotted paths such as `label` or
  `theme.primary`.

This state is designed to survive compatible host restarts without coupling the
runner to any host-specific runtime object graph.

## Baseline limitations

This slice does not yet provide:

- full filesystem-backed watch registration from `dearstory dev`;
- real build-command execution per host;
- UI-driven replay of restored state after a live host restart.

Those layers build on the abstractions introduced here.
