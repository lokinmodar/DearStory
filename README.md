# DearStory

DearStory is a language-neutral component workshop, documentation system, and
visual test runner for [Dear ImGui](https://github.com/ocornut/imgui).

The project is being designed Dear ImGui-first, with isolated language hosts.
The initial hosts will support native C++ and .NET while sharing one catalog,
one versioned protocol, one documentation model, and one conformance suite.

## Project status

DearStory now has active implementation for the Windows-first protocol bootstrap
and the shared core story model. The public API surface is pre-1.0 and may
change as host/runtime work continues, but the Phase 2 library products are
already intended for external C++ and .NET consumers.

Phase 2 public products are the .NET packages `DearStory.Protocol`,
`DearStory.Core`, `DearStory.Sdk`, and `DearStory.Sdk.Generator`, plus the C++
targets `DearStory::ProtocolCpp`, `DearStory::CoreCpp`, and
`DearStory::SdkCpp`. Runner, Catalog, Host, Capture, Docs, and
Transport.Windows remain internal while the Windows-first runtime tooling
matures.

The public packages are library-only contracts and SDKs. They do not require a
DearStory runner, host, catalog, capture pipeline, documentation service, or
Windows transport at consumption time. The Windows-first runtime layer is an
internal implementation concern, not a dependency of the supported library
surface.

For local package validation and external-consumer setup, see the
[.NET package guide](docs/guides/consuming-dotnet-packages.md) and the
[C++ package guide](docs/guides/consuming-cpp-package.md). Maintainers should
use the [package release guide](docs/guides/releasing-packages.md) for the
canonical pack, verification, and release workflow. DearStory release
automation now uses `eng/version.json` as the only public version source.
Maintainers update that file in a reviewed PR, then release the coordinated
public package surface through `.github/workflows/release.yml`.

The approved architecture is documented in
[`docs/superpowers/specs/2026-07-15-dearstory-design.md`](docs/superpowers/specs/2026-07-15-dearstory-design.md).

The first execution-ready plan is the
[cross-language protocol bootstrap](docs/superpowers/plans/2026-07-15-dearstory-protocol-bootstrap.md).

Current implementation rationale and repository policy are tracked in:

- [library product boundary](docs/architecture/library-product-boundary.md)
- [protocol bootstrap architecture](docs/architecture/protocol-bootstrap.md)
- [core story model architecture](docs/architecture/core-story-model.md)
- [story authoring guide](docs/guides/authoring-stories.md)
- [Windows build guide](docs/guides/building-windows.md)
- [package release guide](docs/guides/releasing-packages.md)
- [static docs guide](docs/guides/static-docs.md)
- [documentation and quality policy](docs/standards/documentation-and-quality.md)

## Direction

- Windows-first, with Linux and macOS tracked as explicit backlog work.
- Standalone and embedded operation.
- C++ and C# stories in one catalog through isolated language hosts.
- Build, file watching, hot reload, static documentation, interaction tests,
  and visual regression tests.
- Markdown documentation with typed Doc Blocks.
- JSON Schema-based arguments and controls.
- Source-generated .NET story registration with explicit reflection fallback.
- Extensive automated tests and public API documentation from the first
  implementation milestone.

## License

DearStory is licensed under the [MIT License](LICENSE).
