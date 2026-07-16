# ADR 0001: DearStory control protocol bootstrap

## Status

Accepted

## Context

DearStory needs a language-neutral control protocol that works across the native Dear ImGui host, the official .NET adapter, and future ecosystem adapters without depending on a shared ABI. The bootstrap also needs validation artifacts that both code generators and hand-written codecs can consume.

## Decision

- Control traffic and frame traffic are separate channels. Control stays JSON-only; frames and future binary payloads do not enter the control channel.
- Each control frame is prefixed by an unsigned 32-bit little-endian payload length.
- Control payloads are UTF-8 JSON without BOM.
- The maximum control payload size is 1 MiB.
- The wire contract is described by a checked-in JSON message manifest plus a JSON Schema Draft 2020-12 envelope schema.
- Protocol evolution is additive within a major line. Peers negotiate the lower supported minor when majors match, and reject a major mismatch.
- Generated C++ and C# protocol models are checked into the repository so regeneration can be validated in CI.
- `ImDrawData`, image buffers, and other binary blobs are explicitly excluded from this control protocol and remain transport concerns for later plans.

## Consequences

- The schema and vectors provide a language-neutral compatibility surface before transport adapters exist.
- Tooling can validate sample envelopes without spinning up native code.
- The control channel remains small and deterministic, while later plans can optimize frame transport independently.
