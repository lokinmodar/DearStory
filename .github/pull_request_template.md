## Summary

- What changed?
- Why was this needed?

## Scope

- [ ] C++ public package surface
- [ ] .NET public package surface
- [ ] SDK / generator behavior
- [ ] Windows-first runner / host / catalog
- [ ] Docs / release guidance
- [ ] CI / build / test workflow

## Validation

- [ ] `pwsh -NoProfile -File .\eng\build.ps1 -Configuration Release`
- [ ] `pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release`
- [ ] `pwsh -NoProfile -File .\eng\build.ps1 -Configuration Debug`
- [ ] `pwsh -NoProfile -File .\eng\test.ps1 -Configuration Debug`
- [ ] `dotnet run --project .\src\runner\dotnet\DearStory.Runner\DearStory.Runner.csproj -- build .\examples\workspaces\windows-slice --configuration Release`
- [ ] `git diff --check`
- [ ] Other focused checks (describe below)

## Notes for reviewers

- Any risks, tradeoffs, or follow-up items?
- Anything intentionally left for a later phase?
