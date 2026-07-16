# DearStory

DearStory is a language-neutral component workshop, documentation system, and
visual test runner for [Dear ImGui](https://github.com/ocornut/imgui).

The project is being designed Dear ImGui-first, with isolated language hosts.
The initial hosts will support native C++ and .NET while sharing one catalog,
one versioned protocol, one documentation model, and one conformance suite.

## Project status

DearStory has an approved architecture and is entering implementation planning.
No runtime API is stable yet.

The approved architecture is documented in
[`docs/superpowers/specs/2026-07-15-dearstory-design.md`](docs/superpowers/specs/2026-07-15-dearstory-design.md).

The first execution-ready plan is the
[cross-language protocol bootstrap](docs/superpowers/plans/2026-07-15-dearstory-protocol-bootstrap.md).

Current implementation rationale and repository policy are tracked in:

- [protocol bootstrap architecture](docs/architecture/protocol-bootstrap.md)
- [documentation and quality policy](docs/standards/documentation-and-quality.md)

## Direction

- Windows-first, with Linux and macOS tracked as explicit backlog work.
- Standalone and embedded operation.
- C++ and C# stories in one catalog through isolated language hosts.
- Build, file watching, hot reload, static documentation, interaction tests,
  and visual regression tests.
- Markdown documentation with typed Doc Blocks.
- JSON Schema-based arguments and controls.
- Extensive automated tests and public API documentation from the first
  implementation milestone.

## License

DearStory is licensed under the [MIT License](LICENSE).
