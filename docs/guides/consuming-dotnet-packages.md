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

`eng\pack.ps1` writes the publishable `.nupkg` files to
`artifacts\packages\dotnet` and copies the same packages to
`artifacts\packages\local-feed`. The smoke project resolves the DearStory
packages from the local-feed path supplied by `DearStoryLocalFeed` (while
NuGet remains available for third-party dependencies), proving that consumers
use the packaged public assemblies rather than repository project references.

Consumers that use the local feed should append it to their restore sources:

```xml
<RestoreSources>$(RestoreSources);$(DearStoryLocalFeed)</RestoreSources>
```

For the repository's complete Release validation, use
`pwsh -NoProfile -File .\eng\test.ps1 -Configuration Release` after packing.
That command includes this smoke consumer as well as the installed C++ package
consumer.
