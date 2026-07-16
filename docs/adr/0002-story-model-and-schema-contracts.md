# ADR 0002: Story model and schema contracts

## Status

Accepted

## Context

DearStory needs a language-neutral contract for story discovery, session
lifecycle, argument schemas, validated patches, actions, logs, and named
interaction targets before the first host and catalog implementations can be
built safely. The contract must work for native C++, the official .NET path,
and future hosts without introducing a Dear ImGui wrapper API or coupling the
catalog to one language runtime.

## Decision

- Canonical story IDs are language-neutral string keys carried on the wire.
  They do not depend on source paths, assembly names, or host-specific type
  identities.
- Story discovery is published through `story_index_published` with a host ID
  plus a set of `story_descriptor` entries.
- Story descriptors carry a JSON Schema Draft 2020-12 dialect identifier plus a
  JSON-compatible schema document, default arguments, and capability metadata.
- Story sessions are explicit protocol concepts. The control contract defines
  `story_session_open`, `story_session_opened`, `story_session_reset`, and
  `story_session_closed`.
- Argument updates travel as JSON-compatible patches on `argument_patch`.
  Validation happens in the catalog before send and again in the host before
  application. Rejections return `argument_patch_result` with field-level
  diagnostics and the previous accepted value preserved.
- Actions, logs, and named interaction targets are transported as structured
  events through `action_emitted`, `log_emitted`, and `target_snapshot`.
- Target metadata may include optional semantic information such as role,
  accessible name, and description, but DearStory does not claim to infer a
  complete accessibility tree from arbitrary immediate-mode code.

## Consequences

- Catalog merge can reject duplicate canonical story IDs with deterministic,
  host-attributed diagnostics.
- Host and SDK work can depend on a checked-in contract instead of inventing
  ad-hoc runtime shapes.
- Schema validation, patch validation, and story/session lifecycle tests can be
  written before rendering and catalog UI exist.
- The control protocol remains Dear ImGui-first: it transports metadata and
  lifecycle state, not widget abstractions.
