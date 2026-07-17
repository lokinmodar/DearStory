# Visual baselines

## Canonical approval

Use WARP when creating or updating canonical baselines:

```powershell
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- `
  build .\examples\workspaces\windows-slice `
  --configuration Release `
  --visual-backend warp `
  --approve
```

This promotes the captured PNGs into `tests/visual/windows/baselines` and emits
the same `capture-results.json` manifest shape used by the rest of the visual
pipeline.

## Canonical validation

To validate only the canonical corpus without rewriting baselines:

```powershell
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- `
  build .\examples\workspaces\windows-slice `
  --configuration Release `
  --visual-backend warp `
  --canonical-only
```

## Diagnostic capture

Use GPU output only for local investigation:

```powershell
dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- `
  dev .\examples\workspaces\windows-slice `
  --capture-story buttons/primary `
  --visual-backend gpu
```

Those files are transient by default and must not be treated as canonical.
