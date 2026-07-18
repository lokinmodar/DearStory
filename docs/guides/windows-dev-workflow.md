# Windows development workflow

This guide captures the current `dearstory dev` workflow for the Windows-first
slice.

## What the interactive dev loop owns

- workspace loading from `dearstory.toml`;
- runner-owned catalog initialization;
- host-builder selection by workspace host entry;
- selective restart planning from changed paths;
- serializable story selection and argument preservation primitives;
- a watcher abstraction that can publish changed paths into the restart loop.

## One-shot visual capture

`dearstory dev` also supports one-shot capture without entering the long-running
interactive loop:

```powershell
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- `
  dev .\examples\workspaces\windows-slice `
  --capture-story buttons/primary `
  --visual-backend warp
```

The command prints the resulting `capture-results.json` path and uses the same
shared capture core as `dearstory build`.

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

## Current limitations

- catalog-triggered capture state exists, but the current slice does not yet
  expose a full end-user button/menu workflow for issuing captures through the
  interactive UI;
- full filesystem-backed watch registration from `dearstory dev` remains
  limited;
- host-specific rebuild execution is still intentionally thin.

Those layers build on the abstractions already introduced here.
