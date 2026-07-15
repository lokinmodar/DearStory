# DearStory Design Specification

- Status: Design approved; written specification awaiting review
- Date: 2026-07-15
- Repository: `lokinmodar/DearStory`
- Initial platform: Windows
- License: MIT

## 1. Summary

DearStory is a language-neutral component workshop for Dear ImGui. It combines
an interactive catalog, living documentation, and automated interaction and
visual regression testing. Its developer experience is inspired by Storybook,
but its architecture follows the constraints of immediate-mode native user
interfaces.

DearStory is Dear ImGui-first rather than .NET-first. Native C++ is a primary
consumer. C# is supported through an official .NET SDK and adapters for
Dear ImGui bindings. Additional language hosts can be added without changing
the catalog or reimplementing the product.

The standalone product uses one catalog and isolated host processes per
language. The embedded product reuses the same contracts with a local transport
when running in-process is safe.

## 2. Goals

DearStory will provide:

1. One searchable catalog containing C++, C#, and future language stories.
2. Interactive arguments, controls, actions, logs, themes, DPI, and viewport
   configuration.
3. Build orchestration, file watching, and hot reload in development.
4. Markdown documentation, automatic API documentation, and typed Doc Blocks.
5. Deterministic screenshots, interaction tests, and visual regression tests.
6. A static documentation build suitable for ordinary web hosting.
7. Standalone and embedded integration modes.
8. Small, idiomatic SDKs that do not wrap the Dear ImGui widget API.
9. Versioned contracts and a conformance suite for every language host.
10. Strong maintainability through explicit module boundaries, automated
    quality gates, extensive code documentation, and architectural records.
11. Optional semantic metadata for interaction targets, documentation, and
    accessibility completeness reports.

## 3. Initial non-goals

The first public milestones will not provide:

- execution of native or .NET stories inside a web browser;
- a security sandbox for untrusted story code;
- simultaneous support for Windows, Linux, and macOS;
- a replacement or wrapper for the Dear ImGui widget API;
- automatic creation of a complete operating-system accessibility tree for
  arbitrary Dear ImGui code;
- binary compatibility between arbitrary Dear ImGui builds;
- a general-purpose remote desktop or streaming protocol;
- a public extension marketplace.

These exclusions keep the first vertical slices testable while preserving
extension points for later work.

## 4. Design principles

### 4.1 Dear ImGui remains native

C++ stories call `ImGui::` directly. C# stories call the API exposed by their
selected binding. DearStory provides story lifecycle, metadata, arguments,
documentation, actions, targets, and testing facilities; it does not mirror the
widget API.

### 4.2 Isolation is the standalone default

Language hosts run in separate processes. A native crash, managed exception,
or failed hot reload affects the responsible host instead of terminating the
catalog. Process isolation is a reliability boundary, not a security boundary.

### 4.3 Contracts precede implementations

The control protocol, argument schema, documentation blocks, frame transport,
and conformance behavior are specified independently of a language host. New
hosts implement those contracts and run the same conformance suite.

### 4.4 Every feature is diagnosable

Builds, handshakes, sessions, argument patches, frames, actions, and failures
produce structured diagnostics with correlation identifiers. Error messages
must identify the failing host, story, operation, and recovery action whenever
that information exists.

### 4.5 Quality is a release requirement

Public behavior is tested. Public APIs are documented. Examples compile and
run in continuous integration. Missing documentation, protocol incompatibility,
and test regressions block releases.

## 5. Architecture

### 5.1 Components

`Protocol`
: Defines versioned control messages, capability negotiation, schemas, error
  envelopes, and generated language models. It contains no runner, renderer,
  build-system, or operating-system policy.

`Core`
: Owns catalog models, story identity, sessions, argument validation,
  documentation indexing, and lifecycle state machines. It depends on protocol
  abstractions but not concrete hosts or transports.

`Runner`
: Implements the `dearstory` command-line application, configuration loading,
  builder orchestration, host supervision, artifact collection, and command
  execution for `dev`, `build`, and `test`.

`Catalog`
: Implements the native Dear ImGui user interface. It provides navigation,
  search, live preview, controls, documentation, actions, logs, and test
  reports. The catalog uses DearStory itself as the project matures.

`Transports`
: Implements control and frame channels behind replaceable interfaces. Windows
  initially uses named pipes for control and shared memory for RGBA frames.

`Builders`
: Convert project configuration and source changes into launchable host
  descriptors. Builders execute tools through explicit executable and argument
  arrays, never by concatenating untrusted shell commands.

`Language hosts`
: Own a Dear ImGui context, renderer, loaded stories, story state, and language
  integration. Initial implementations are `cpp-host` and `dotnet-host`.

`SDKs`
: Provide idiomatic story definitions for C++ and .NET. SDKs depend on protocol
  models and host contracts, never on the catalog or runner.

`Documentation builder`
: Parses Markdown into a safe document model, resolves typed Doc Blocks, and
  emits both native catalog content and static HTML.

### 5.2 Dependency direction

Dependencies point inward toward contracts:

```text
SDKs and hosts -> Protocol
Runner          -> Core + Protocol + transport/build interfaces
Catalog         -> Core-facing interfaces
Concrete IPC    -> transport interfaces
Concrete builds -> builder interfaces
Static docs     -> Core document model
```

The catalog does not reference CMake, MSBuild, the CLR, or a concrete host. A
host does not reference catalog UI code. Protocol types do not reference Dear
ImGui internal structures.

### 5.3 Standalone topology

The standalone runner owns the catalog process and supervises one or more host
processes. A workspace may start both C++ and .NET hosts. Stories from every
healthy host are merged into one catalog index. Duplicate canonical story IDs
are rejected with diagnostics identifying both registrations.

### 5.4 Embedded topology

Embedded integrations reuse the same session, schema, and lifecycle contracts.
An in-process local transport replaces IPC when requested. Embedded failures
cannot receive process-level isolation, so the API and documentation must state
that tradeoff. Embedded mode may also choose an out-of-process host while
embedding only the catalog panel.

## 6. Host rendering and frame transport

Each host renders its own frames. The catalog sends input, viewport, DPI,
theme, argument patches, and capture requests. The host returns completed frame
descriptors and structured events.

DearStory does not send `ImDrawData` across the process boundary. Dear ImGui
does not guarantee a stable ABI for its internal data, and textures have
renderer-specific ownership. Keeping rendering in the host supports different
bindings, forks, and Dear ImGui commits.

The initial frame transport is a portable abstraction implemented on Windows
with shared memory containing RGBA8 pixels. It uses multiple buffers so the
host does not overwrite a frame currently being consumed. Frame descriptors
contain sequence number, width, height, stride, color space, timestamp, and
shared-memory slot. Stale frames may be dropped; control messages may not.

A D3D11 shared-texture transport is a later Windows optimization behind the
same interface. Linux and macOS transports will implement the same semantic
contract with platform-appropriate primitives.

CI uses D3D11 WARP for deterministic offscreen rendering. Development may use
hardware rendering.

## 7. Story model

### 7.1 Identity and metadata

A story has a stable canonical ID, display title, optional hierarchy, tags,
description, source location, argument schema, default arguments, capabilities,
and lifecycle callbacks. IDs are independent of language and file-system path
so documentation links and baselines survive refactoring.

### 7.2 Lifecycle

A story session exposes these phases:

1. `setup` allocates session resources.
2. `render` runs once per requested frame.
3. `reset` restores defaults and deterministic services.
4. `teardown` releases session resources.

Hosts guarantee balanced teardown after ordinary failures. Process termination
is the final recovery mechanism for native corruption or an unresponsive host.

### 7.3 Story context

The context provides:

- typed serializable arguments;
- observable actions;
- structured logging;
- controlled asset resolution;
- replaceable clock and random source;
- viewport, DPI, theme, and capability information;
- named interaction targets with optional role, accessible name, and
  description;
- cancellation and session identity.

It does not provide replacement Dear ImGui widgets.

### 7.4 C++ shape

```cpp
DB_STORY("Buttons/Primary", primary_button)
{
    auto label = context.args.string("label", "Save");

    if (ImGui::Button(label.c_str()))
        context.actions.emit("clicked");

    context.targets.capture_last_item("save-button");
}
```

The C++ builder produces a small host executable that statically links the
selected Dear ImGui build and the workspace stories. Hot reload rebuilds and
restarts that process. This avoids loading arbitrary story DLLs through an
unstable C++ ABI.

### 7.5 C# shape

```csharp
[Story("Buttons/Primary")]
public static void PrimaryButton(StoryContext context)
{
    var label = context.Args.String("label", "Save");

    if (ImGui.Button(label))
        context.Actions.Emit("clicked");

    context.Targets.CaptureLastItem("save-button");
}
```

The .NET host loads story assemblies into a collectible `AssemblyLoadContext`
when possible. It restarts when unload cannot be proven safe. Binding-specific
adapters integrate ImGui.NET-compatible APIs and future bindings without
changing the story protocol.

## 8. Configuration, builders, and hot reload

The workspace configuration file is `dearstory.toml`. The CLI locates it from
the current directory upward. The configuration declares project identity,
story globs, builders, hosts, themes, viewports, output paths, environment
allowlists, and test profiles.

`dearstory dev` performs this sequence:

1. Parse and validate configuration.
2. Start configured builders.
3. Produce host artifacts and launch descriptors.
4. Start hosts and negotiate capabilities.
5. Merge story indexes and open the catalog.
6. Watch declared source and documentation inputs.
7. Rebuild and restart only affected hosts.
8. Preserve catalog selection and serializable arguments when compatible.

Build output is streamed as structured diagnostics. On native Windows,
subprocess failures include the executable, arguments, exit code, standard
error, and operating-system error information. The process launcher uses
Windows-safe executable names and argument handling.

## 9. Control protocol

### 9.1 Transport and envelope

Windows uses named pipes carrying length-prefixed UTF-8 JSON control messages.
The envelope contains protocol version, message type, message ID, correlation
ID, session ID when applicable, timestamp, payload, and structured error data.

Binary frames and large attachments use side channels referenced by control
messages. They are not base64-encoded into the control stream.

### 9.2 Core exchanges

The protocol covers:

- hello, welcome, version, and capability negotiation;
- story index publication and incremental invalidation;
- session open, reset, and close;
- argument snapshots and validated patches;
- input batches and viewport changes;
- frame availability and capture completion;
- actions, logs, metrics, and diagnostics;
- interaction test commands and results;
- heartbeat, graceful shutdown, and forced termination notices.

### 9.3 Compatibility

Protocol versions use major and minor numbers. A major difference is rejected.
Minor versions are additive: receivers ignore unknown optional fields and
advertise capabilities before optional messages are used. Required unknown
capabilities fail the handshake with an actionable error.

SDKs, hosts, and the runner use semantic versioning. Every session records the
protocol version, SDK version, language, toolchain, binding, Dear ImGui version,
and Dear ImGui commit or package identity. Compatibility is determined by the
handshake, not by assuming that equal Dear ImGui version strings imply equal
ABI.

## 10. Arguments, schemas, controls, and reflection

The canonical argument contract is a documented subset of JSON Schema Draft
2020-12. DearStory annotations use the `x-dearstory-*` namespace for control
type, category, order, visibility, formatting, and other presentation hints.

Initial values support booleans, integers, floating-point numbers, strings,
enums, colors, vectors, arrays, and JSON-compatible objects. Native complex
values are mapped by the host from serializable wire values. Raw native object
pointers and arbitrary managed objects never cross the protocol.

The catalog generates controls and argument tables from schemas. Patches are
validated in the catalog and validated again in the host. A rejected patch
leaves the previous value intact and returns a field-level diagnostic.

The .NET SDK uses attributes and a source generator to produce schemas and
registries at compile time. XML documentation supplies descriptions. Runtime
reflection is a fallback for supported dynamic scenarios, not the default.

The C++ SDK provides typed descriptors based on templates, member pointers, and
small registration macros. Doxygen content may enrich generated descriptions.
A Clang-based metadata generator is an optional future convenience; the core
contract does not depend on it.

## 11. Documentation

DearStory accepts CommonMark/GitHub-Flavored Markdown with typed Doc Blocks.
It does not execute MDX or arbitrary JavaScript.

Example:

```markdown
# Primary Button

Use the primary button for the main action in a view.

:::story id="buttons/primary"
:::

## Properties

:::controls
:::

:::arg-types
:::

:::source language="cpp"
:::
```

Documentation may be adjacent to stories or located under configured docs
globs. Pages may be attached to a story group or exist as free-standing catalog
entries. When manual documentation is absent, Autodocs generates a title,
description, primary screenshot, argument table, controls description, source
reference, and story list.

The Markdown parser produces a safe document model consumed by both the native
catalog and the static HTML builder. Raw HTML is disabled by default. Links,
images, code blocks, and directives are validated. Documentation changes hot
reload without rebuilding a language host unless generated metadata changed.

The initial static HTML output is searchable and navigable and contains
Markdown, schemas, source snippets, screenshots, and test status. It does not
execute native or .NET stories. Browser execution through WebAssembly is
explicit backlog work. When stories provide semantic target metadata, the
static build includes it and reports missing accessible names without claiming
that DearStory can infer complete accessibility semantics from arbitrary
immediate-mode code.

## 12. Commands

`dearstory dev`
: Starts builders, hosts, watchers, and the interactive catalog.

`dearstory build`
: Runs deterministic captures and emits static documentation plus a machine-
  readable manifest.

`dearstory test`
: Runs configured interaction and visual suites headlessly and emits console,
  machine-readable, and HTML reports.

All commands provide stable nonzero exit codes for configuration, build,
protocol, host, test, and documentation failures.

## 13. Error handling and recovery

Failures are classified as:

- invalid configuration;
- builder or toolchain failure;
- host launch failure;
- incompatible protocol, SDK, binding, or Dear ImGui capability;
- managed exception;
- native crash;
- host timeout or lost heartbeat;
- rendering or frame-transport failure;
- invalid schema, arguments, or patch;
- documentation parse or resolution failure;
- interaction or visual test failure.

The supervisor records host executable, arguments, process ID, exit code,
active story, last heartbeat, recent correlated logs, and recovery attempts.
Automatic restart uses a bounded retry policy and stops on repeated failure to
avoid restart loops. The catalog remains usable and exposes retry and diagnostic
actions.

Story code is trusted local code and runs with the user's permissions. Process
isolation limits fault propagation but does not prevent malicious filesystem,
network, or process access.

## 14. Testing strategy

### 14.1 Test layers

- Unit tests cover parsing, state machines, schemas, patches, indexing,
  versioning, and recovery policies.
- Protocol contract tests cover every message, optional-field evolution,
  invalid data, size limits, cancellation, and timeouts.
- Host conformance tests run unchanged against every official host.
- Integration tests exercise runner, builder, host, control channel, and frame
  channel together.
- End-to-end tests run real C++ and .NET example workspaces through all three
  commands.
- Property and fuzz tests target protocol decoders, schemas, argument patches,
  Markdown, and Doc Blocks.
- Visual tests compare deterministic frames and publish baseline, actual, and
  difference images.

### 14.2 Deterministic visual environment

Visual profiles pin Dear ImGui identity, host SDK, fonts, theme, viewport, DPI,
locale, clock, random seed, delta time, renderer, and color space. Windows CI
uses D3D11 WARP. Pixel tolerance defaults to zero and may be relaxed only by a
reviewed test-specific rule with a documented reason.

Baseline updates are explicit source changes. Pull requests display baseline,
actual, difference, and metric summaries. A host or renderer change cannot
silently rewrite baselines.

### 14.3 Interaction targets

Stories may annotate the last emitted Dear ImGui item with a stable target ID.
The SDK captures its ID and rectangle without wrapping the widget call. Tests
interact with targets instead of brittle absolute coordinates. Coordinate and
raw-input operations remain available for behaviors that do not expose an
item.

### 14.4 Coverage and mutation testing

Core and protocol modules begin with minimum gates of 80 percent line coverage
and 70 percent branch coverage. Other modules enforce a no-regression baseline
once their first vertical slice lands. Coverage never replaces behavioral
review. Mutation testing is required selectively for protocol validation,
schema validation, and argument patch logic before the 1.0 release.

## 15. Documentation and code quality

Public C++ APIs require Doxygen comments. Public C# APIs require XML
documentation. Missing public documentation is a build failure. Documentation
generation treats warnings as errors and validates links and code snippets.

The repository includes:

- architecture and protocol specifications;
- Architecture Decision Records;
- tutorials and task-oriented guides;
- fully documented example workspaces;
- contributor, security, compatibility, and release policies;
- generated API references for C++ and .NET;
- changelog and migration guides.

C++ and .NET builds enable warnings as errors. Formatting, static analysis,
nullable analysis, dependency auditing, and deterministic builds run in CI.
Dedicated jobs run native sanitizers and fuzz targets. Dependencies are pinned,
isolated behind interfaces, and updated through reviewed automation.

## 16. Repository layout

```text
src/
  core/
  protocol/
  runner/
  catalog/
  transports/
  builders/
  hosts/
    cpp/
    dotnet/
sdk/
  cpp/
  dotnet/
tests/
  unit/
  contract/
  conformance/
  integration/
  e2e/
  visual/
examples/
  cpp/
  dotnet/
docs/
  architecture/
  guides/
  protocol/
  adr/
  superpowers/specs/
```

The monorepo allows protocol, runner, hosts, SDKs, tests, and documentation to
change atomically. Each directory exposes a narrow public target and keeps
implementation files private. Repository-wide build entry points orchestrate
CMake and .NET without hiding their native diagnostics.

## 17. Delivery milestones

### 17.1 Foundation and risk proof

Deliver the protocol handshake, supervisor, RGBA shared-memory transport,
minimal catalog, one C++ story, and one C# story. Both stories must appear in
the same catalog and respond to one argument change. The complete flow runs in
Windows CI.

### 17.2 Development workflow

Deliver builders, file watching, rebuild, restart, story discovery, schemas,
controls, actions, Markdown, logs, and actionable diagnostics.

### 17.3 Documentation

Deliver Autodocs, typed Doc Blocks, deterministic screenshots, search, source
snippets, and `dearstory build` static output.

### 17.4 Testing

Deliver named targets, interaction scenarios, visual baselines, difference
reports, conformance coverage, and `dearstory test`.

### 17.5 Embedded operation and distribution

Deliver documented C++ and .NET embedding APIs, installable CLI artifacts,
packages, project templates, semantic versioning, and upgrade guidance.

### 17.6 Windows optimization

Deliver D3D11 shared-texture transport, profiling, frame backpressure metrics,
and optimized hot-reload latency without changing public contracts.

## 18. Explicit backlog

The public backlog tracks these independent efforts:

- Linux runner, IPC, rendering, CI, packaging, and validation;
- macOS runner, IPC, Metal rendering, CI, packaging, and validation;
- interactive static documentation through C++ and .NET WebAssembly hosts;
- Rust, Python, and additional language hosts;
- adapters for Dalamud and other Dear ImGui forks and bindings;
- remote host execution and network transport;
- extension APIs and ecosystem distribution.

Platform work must satisfy the same host conformance and documentation
requirements as Windows. Platform-specific shortcuts may not leak into the
protocol or story model.

## 19. Versioning and governance

DearStory uses semantic versioning. Public APIs remain explicitly experimental
before 1.0, but breaking changes still require a changelog entry and migration
note. Protocol compatibility is tested across supported minor versions.

Changes to architecture, protocol semantics, compatibility guarantees, public
extension points, or security boundaries require an ADR. Pull requests require
tests and documentation proportional to their public behavior. Releases are
produced from CI with provenance, checksums, generated documentation, and a
complete dependency notice.

The project is public under the MIT License to support adoption in open-source
and proprietary engines and tools.

## 20. First-milestone acceptance criteria

The foundation milestone is accepted only when all of the following are true:

1. A clean Windows checkout builds through documented commands.
2. The runner starts one C++ host and one .NET host.
3. Both hosts pass the same protocol conformance suite.
4. One story from each language appears in one catalog.
5. A schema-generated control changes each story and produces a new frame.
6. A host crash is reported without terminating the catalog.
7. Deterministic offscreen captures pass in CI.
8. Unit, contract, integration, and end-to-end tests pass.
9. Public APIs and setup steps are fully documented.
10. CI artifacts contain logs and diagnostics sufficient to investigate a
    failed build or host launch.

## 21. Principal risks and mitigations

`Cross-process rendering latency`
: Begin with a measurable RGBA transport, allow frame dropping, keep control
  traffic independent, and add D3D11 shared textures only after profiling.

`Dear ImGui and binding version divergence`
: Record exact identities, negotiate capabilities, keep rendering within each
  host, and validate official combinations in conformance CI.

`C++ hot-reload complexity`
: Rebuild small host executables and restart processes instead of depending on
  unstable native plugin ABI.

`Visual-test instability`
: Pin every visual input, use WARP in CI, review baseline changes, and prohibit
  silent tolerance expansion.

`Protocol drift between languages`
: Generate models from one schema and require the same black-box conformance
  suite for every host.

`Scope expansion`
: Deliver vertical milestones in order. New languages and platforms remain
  backlog work until the Windows C++/.NET vertical slice is reliable.

## 22. Approved decisions

- Dear ImGui-first, not C#-first.
- One catalog with separate language host processes.
- Standalone and embedded products share contracts.
- Build, watch, and hot reload are product responsibilities.
- Windows is the initial platform; Linux and macOS are explicit backlog work.
- Hosts render their own frames.
- SDKs are thin and stories call native/binding Dear ImGui APIs directly.
- Markdown uses typed Doc Blocks without executable MDX.
- JSON Schema is the canonical argument and control contract.
- Initial static HTML is documentary rather than executable.
- Tests, extensive code documentation, Markdown guides, and maintainable module
  boundaries are release requirements.
- The monorepo is named `DearStory`, is public, and uses the MIT License.
