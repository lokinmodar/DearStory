# DearStory library product boundary

Phase 2 public products:

| Language | Public product |
| --- | --- |
| .NET | DearStory.Protocol |
| .NET | DearStory.Core |
| .NET | DearStory.Sdk |
| .NET | DearStory.Sdk.Generator |
| C++ | DearStory::ProtocolCpp |
| C++ | DearStory::CoreCpp |
| C++ | DearStory::SdkCpp |

Only the four .NET products explicitly opt in to NuGet packaging; all other
.NET projects are non-packable by default.

The public products must not depend on Runner, Catalog, Host, Capture, Docs,
or Transport.Windows.
