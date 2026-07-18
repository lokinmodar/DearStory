# Consuming DearStory .NET packages

DearStory's public .NET library surface is published as four packages:

- `DearStory.Protocol`
- `DearStory.Core`
- `DearStory.Sdk`
- `DearStory.Sdk.Generator`

The SDK provides `[Story]`, `[StoryArg]`, and `StoryContext`. Add the generator as a private analyzer dependency so story registrations are generated at build time. The generated `DearStory.Sdk.GeneratedStoryRegistryFactory.Create()` method returns the registry for the stories in the consuming assembly.

```xml
<ItemGroup>
  <PackageReference Include="DearStory.Protocol" Version="0.1.0-alpha*" />
  <PackageReference Include="DearStory.Core" Version="0.1.0-alpha*" />
  <PackageReference Include="DearStory.Sdk" Version="0.1.0-alpha*" />
  <PackageReference Include="DearStory.Sdk.Generator" Version="0.1.0-alpha*" PrivateAssets="all" />
</ItemGroup>
```

For local package validation, first create the package feed:

```powershell
pwsh -NoProfile -File .\eng\pack.ps1 -Configuration Release
$env:DearStoryLocalFeed = (Resolve-Path .\artifacts\packages\local-feed).Path
dotnet test .\tests\consumers\dotnet\DearStory.Consumer.Smoke\DearStory.Consumer.Smoke.csproj -c Release
```

Consumers that use the local feed should append it to their restore sources:

```xml
<RestoreSources>$(RestoreSources);$(DearStoryLocalFeed)</RestoreSources>
```
