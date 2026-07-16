# DearStory Control Protocol v1.0

## Overview

DearStory control protocol v1.0 defines the bootstrap handshake and the first
language-neutral story/session contract between the runner, catalog, and
official hosts. Every message is encoded as UTF-8 JSON inside a uint32
little-endian length-prefixed frame. Peers reject declared frame sizes larger
than 1,048,576 bytes before allocating the payload buffer.

## Envelope

All control messages use the same envelope.

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `protocol` | Yes | `protocol_version` | Requires `major` and `minor`; rejects extra fields. |
| `type` | Yes | `string` | One of the message names documented below. |
| `messageId` | Yes | `string` | RFC 4122 UUID string. |
| `correlationId` | No | `string` | RFC 4122 UUID string. |
| `sessionId` | No | `string` | RFC 4122 UUID string. |
| `timestamp` | Yes | `string` | RFC 3339 UTC date-time with millisecond precision. |
| `payload` | Yes | `object` | Shape is selected by `type`. |

Envelope-level additional fields are allowed for forward-compatible optional
metadata.

## Message families

### Handshake

#### `hello`

Direction: initiating peer -> accepting peer

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `role` | Yes | `peer_role` | One of `runner`, `catalog`, `host`. |
| `implementation` | Yes | `implementation_identity` | Requires `name`, `version`, `language`, `toolchain`; may add optional identity metadata. |
| `supportedCapabilities` | Yes | `string[]` | Capability list offered by the sender. |
| `requiredCapabilities` | Yes | `string[]` | Capability list that must be shared for the session to continue. |

#### `welcome`

Direction: accepting peer -> initiating peer

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `peerId` | Yes | `uuid` | RFC 4122 UUID string identifying the accepting peer. |
| `negotiatedVersion` | Yes | `protocol_version` | Shared major plus the lower supported minor. |
| `acceptedCapabilities` | Yes | `string[]` | Sorted intersection of shared capabilities accepted for the session. |

`welcome.correlationId` must equal the initiating `hello.messageId`.

#### `reject`

Direction: accepting peer -> initiating peer

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `error` | Yes | `protocol_error` | Requires `code`, `message`, and `recovery`; may include optional `details`. |

`reject.correlationId` must equal the initiating `hello.messageId`.

### Story discovery

#### `story_index_published`

Direction: host -> runner/catalog

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `hostId` | Yes | `string` | Stable host identity for diagnostics and merge attribution. |
| `stories` | Yes | `story_descriptor[]` | Complete published story set for the sending host. |

Duplicate canonical story IDs across hosts are merge errors at the catalog/core
layer and are not silently accepted.

### Session lifecycle

#### `story_session_open`

Direction: runner/catalog -> host

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `sessionId` | Yes | `uuid` | Stable session identity. |
| `storyId` | Yes | `string` | Canonical story ID. |
| `initialArguments` | Yes | `json` | JSON-compatible initial argument snapshot. |
| `randomSeed` | Yes | `string` | Deterministic seed value encoded for transport. |
| `startTimeUtc` | Yes | `string` | RFC 3339 UTC timestamp for deterministic services. |

#### `story_session_opened`

Direction: host -> runner/catalog

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `sessionId` | Yes | `uuid` | Echoes the opened session. |
| `storyId` | Yes | `string` | Canonical story ID. |
| `activeArguments` | Yes | `json` | Accepted live argument snapshot. |
| `randomSeed` | Yes | `string` | Effective seed after host initialization. |
| `startTimeUtc` | Yes | `string` | Effective deterministic session start time. |

#### `story_session_reset`

Direction: runner/catalog -> host

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `sessionId` | Yes | `uuid` | Session to reset. |
| `arguments` | Yes | `json` | Default or replacement argument snapshot. |
| `randomSeed` | Yes | `string` | Reset deterministic seed. |
| `startTimeUtc` | Yes | `string` | Reset deterministic clock value. |

#### `story_session_closed`

Direction: either peer -> the other

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `sessionId` | Yes | `uuid` | Session being closed. |
| `storyId` | Yes | `string` | Canonical story ID for diagnostics. |
| `reason` | No | `string` | Optional human-readable close reason. |

### Argument updates

#### `argument_patch`

Direction: runner/catalog -> host

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `sessionId` | Yes | `uuid` | Session whose arguments should change. |
| `patch` | Yes | `json` | JSON-compatible patch document or replacement payload. |

#### `argument_patch_result`

Direction: host -> runner/catalog

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `sessionId` | Yes | `uuid` | Session whose patch was evaluated. |
| `accepted` | Yes | `boolean` | `true` when the patch was applied; otherwise `false`. |
| `updatedArguments` | Yes | `json` | New accepted value or the last unchanged accepted value after rejection. |
| `diagnostics` | Yes | `field_diagnostic[]` | Empty on success; field-level errors on rejection. |

Patch validation is performed in the catalog before the send and again in the
host before application. Rejected patches leave the previous accepted value
intact.

### Events and targets

#### `action_emitted`

Direction: host -> runner/catalog

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `sessionId` | Yes | `uuid` | Session that emitted the action. |
| `storyId` | Yes | `string` | Canonical story ID. |
| `action` | Yes | `action_event` | Structured action payload. |

#### `log_emitted`

Direction: host -> runner/catalog

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `sessionId` | Yes | `uuid` | Session that emitted the log. |
| `storyId` | Yes | `string` | Canonical story ID. |
| `log` | Yes | `log_event` | Structured log payload. |

#### `target_snapshot`

Direction: host -> runner/catalog

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `sessionId` | Yes | `uuid` | Session that owns the target snapshot. |
| `storyId` | Yes | `string` | Canonical story ID. |
| `targets` | Yes | `story_target[]` | Stable target IDs and optional semantic metadata. |

## Shared records

### `protocol_version`

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `major` | Yes | `uint16` | Breaking protocol generation. |
| `minor` | Yes | `uint16` | Additive protocol generation. |

### `implementation_identity`

Required fields: `name`, `version`, `language`, `toolchain`

Optional fields: `binding`, `dearImGuiVersion`, `dearImGuiIdentity`

### `protocol_error`

| Field | Required | Type | Notes |
| --- | --- | --- | --- |
| `code` | Yes | `string` | Stable error code. |
| `message` | Yes | `string` | Human-readable rejection summary. |
| `recovery` | Yes | `string` | Recovery guidance presented to the caller. |
| `details` | No | `object` | Structured diagnostic context. |

### `story_argument_schema`

| Field | Required | Type | Notes |
| --- | --- | --- | --- |
| `dialect` | Yes | `string` | Schema dialect identifier. |
| `schema` | Yes | `json` | JSON-compatible argument schema document. |

### `story_descriptor`

| Field | Required | Type | Notes |
| --- | --- | --- | --- |
| `id` | Yes | `string` | Canonical language-neutral story ID. |
| `title` | Yes | `string` | Human-facing story title. |
| `hierarchy` | Yes | `string[]` | Optional pre-split catalog hierarchy. |
| `tags` | Yes | `string[]` | Story classification tags. |
| `description` | No | `string` | Optional short description. |
| `sourcePath` | No | `string` | Optional source location hint. |
| `argumentSchema` | Yes | `story_argument_schema` | Story argument contract. |
| `defaultArguments` | Yes | `json` | Serializable default argument values. |
| `capabilities` | Yes | `string[]` | Story-specific advertised capabilities. |

### `semantic_metadata`

Optional fields: `role`, `accessibleName`, `description`

### `story_target`

| Field | Required | Type | Notes |
| --- | --- | --- | --- |
| `id` | Yes | `string` | Stable target identifier. |
| `bounds` | No | `json` | Host-defined serializable rectangle payload. |
| `semantic` | No | `semantic_metadata` | Optional semantic annotations. |

### `action_event`

| Field | Required | Type | Notes |
| --- | --- | --- | --- |
| `name` | Yes | `string` | Stable action name. |
| `payload` | Yes | `json` | JSON-compatible event payload. |
| `emittedAt` | Yes | `string` | RFC 3339 UTC timestamp. |
| `targetId` | No | `string` | Optional associated target identifier. |

### `log_event`

| Field | Required | Type | Notes |
| --- | --- | --- | --- |
| `level` | Yes | `string` | Host-defined log level. |
| `message` | Yes | `string` | Human-readable log message. |
| `emittedAt` | Yes | `string` | RFC 3339 UTC timestamp. |
| `details` | No | `json` | Optional structured diagnostic payload. |

### `field_diagnostic`

| Field | Required | Type | Notes |
| --- | --- | --- | --- |
| `field` | Yes | `string` | Affected field path. |
| `code` | Yes | `string` | Stable validation code. |
| `message` | Yes | `string` | Human-readable validation message. |
| `recovery` | No | `string` | Optional recovery guidance. |

## Error codes

| Code | Meaning | Typical trigger |
| --- | --- | --- |
| `protocol.major_mismatch` | Peer majors differ. | `hello.protocol.major` is unsupported. |
| `protocol.required_capability_missing` | Required capability is absent. | No shared entry for one of `requiredCapabilities`. |
| `protocol.unknown_message_type` | Envelope `type` is unsupported. | Unknown control message name. |
| `protocol.invalid_envelope` | Envelope or payload shape is invalid. | Missing required field, invalid UUID, invalid timestamp, malformed JSON. |
| `protocol.frame_too_large` | Declared frame size exceeds 1 MiB. | Prefix value greater than `1,048,576`. |

## Negotiation and validation semantics

- Successful handshake uses the lower supported minor version among peers that
  share the same major version.
- `welcome.acceptedCapabilities` is the sorted intersection of the local and
  remote supported capability sets.
- `welcome.correlationId` and `reject.correlationId` must echo the initiating
  `hello.messageId`.
- Duplicate entries in `supportedCapabilities` or `requiredCapabilities` are
  treated as `protocol.invalid_envelope`.
- Unknown optional envelope members are ignored for forward compatibility.
- Control messages may carry large JSON payloads only through side channels
  referenced from the control envelope in later plans; binary frames and large
  attachments are not base64-encoded into the control stream.

## Story/session semantics

- `story_index_published` publishes a host-local complete story set for merge.
- Story IDs are canonicalized outside the wire format and are compared as
  language-neutral keys.
- `story_session_open` starts deterministic services for one story session.
- `story_session_opened` reports the accepted active argument snapshot.
- `story_session_reset` restores a deterministic session state.
- `story_session_closed` terminates a session without implying host shutdown.
- `argument_patch_result.accepted=false` means the previous accepted value
  remains authoritative.
- `target_snapshot` reports named interaction targets without wrapping Dear
  ImGui widgets.

## Representative examples

### `story_index_published`

```json
{
  "protocol": { "major": 1, "minor": 0 },
  "type": "story_index_published",
  "messageId": "55555555-5555-4555-8555-555555555555",
  "timestamp": "2026-07-16T09:00:00.000Z",
  "payload": {
    "hostId": "dotnet-host",
    "stories": [
      {
        "id": "buttons/primary",
        "title": "Buttons/Primary",
        "hierarchy": ["Buttons"],
        "tags": ["controls", "docs"],
        "description": "Shows the primary button story.",
        "argumentSchema": {
          "dialect": "https://json-schema.org/draft/2020-12/schema",
          "schema": {
            "type": "object",
            "properties": {
              "label": { "type": "string" }
            }
          }
        },
        "defaultArguments": {
          "label": "Save"
        },
        "capabilities": ["args.patch.v1", "targets.snapshot.v1"]
      }
    ]
  }
}
```

### Rejected `argument_patch_result`

```json
{
  "protocol": { "major": 1, "minor": 0 },
  "type": "argument_patch_result",
  "messageId": "88888888-8888-4888-8888-888888888888",
  "correlationId": "77777777-7777-4777-8777-777777777777",
  "sessionId": "66666666-6666-4666-8666-666666666666",
  "timestamp": "2026-07-16T09:02:00.000Z",
  "payload": {
    "sessionId": "66666666-6666-4666-8666-666666666666",
    "accepted": false,
    "updatedArguments": {
      "size": "medium"
    },
    "diagnostics": [
      {
        "field": "size",
        "code": "args.enum",
        "message": "The value must be one of the declared enum members.",
        "recovery": "Retry with small, medium, or large."
      }
    ]
  }
}
```
