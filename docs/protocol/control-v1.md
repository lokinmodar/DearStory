# DearStory Control Protocol v1.0

## Overview

DearStory control protocol v1.0 defines the bootstrap handshake between protocol peers. Every message is encoded as UTF-8 JSON inside a uint32 little-endian length-prefixed frame. Peers reject declared frame sizes larger than 1,048,576 bytes before allocating the payload buffer.

## Envelope

All messages use the same envelope.

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `protocol` | Yes | `protocol_version` | Requires `major` and `minor`; rejects extra fields. |
| `type` | Yes | `string` | One of `hello`, `welcome`, `reject`. |
| `messageId` | Yes | `string` | RFC 4122 UUID string. |
| `correlationId` | No | `string` | RFC 4122 UUID string. |
| `sessionId` | No | `string` | RFC 4122 UUID string. |
| `timestamp` | Yes | `string` | RFC 3339 UTC date-time with millisecond precision. |
| `payload` | Yes | `object` | Shape is selected by `type`. |

Envelope-level additional fields are allowed for forward-compatible optional metadata.

## Payloads

### `hello`

Direction: initiating peer -> accepting peer

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `role` | Yes | `peer_role` | One of `runner`, `catalog`, `host`. |
| `implementation` | Yes | `implementation_identity` | Requires `name`, `version`, `language`, `toolchain`; may add optional identity metadata. |
| `supportedCapabilities` | Yes | `string[]` | Capability list offered by the sender. |
| `requiredCapabilities` | Yes | `string[]` | Capability list that must be shared for the session to continue. |

### `welcome`

Direction: accepting peer -> initiating peer

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `peerId` | Yes | `uuid` | RFC 4122 UUID string identifying the accepting peer. |
| `negotiatedVersion` | Yes | `protocol_version` | Shared major plus the lower supported minor. |
| `acceptedCapabilities` | Yes | `string[]` | Sorted intersection of shared capabilities accepted for the session. |

`welcome.correlationId` must equal the initiating `hello.messageId`.

### `reject`

Direction: accepting peer -> initiating peer

| Field | Required | Type | Validation |
| --- | --- | --- | --- |
| `error` | Yes | `protocol_error` | Requires `code`, `message`, and `recovery`; may include optional `details`. |

`reject.correlationId` must equal the initiating `hello.messageId`.

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

## Error codes

| Code | Meaning | Typical trigger |
| --- | --- | --- |
| `protocol.major_mismatch` | Peer majors differ. | `hello.protocol.major` is unsupported. |
| `protocol.required_capability_missing` | Required capability is absent. | No shared entry for one of `requiredCapabilities`. |
| `protocol.unknown_message_type` | Envelope `type` is unsupported. | Unknown control message name. |
| `protocol.invalid_envelope` | Envelope or payload shape is invalid. | Missing required field, invalid UUID, invalid timestamp, malformed JSON. |
| `protocol.frame_too_large` | Declared frame size exceeds 1 MiB. | Prefix value greater than `1,048,576`. |

## State transitions

1. Initiating peer sends one `hello`.
2. Accepting peer validates frame size, UTF-8, JSON syntax, envelope shape, and payload semantics.
3. If majors match and required capabilities are shared, the accepting peer returns `welcome`.
4. Otherwise the accepting peer returns `reject`.
5. The initiating peer either proceeds using `welcome.negotiatedVersion` or terminates the session after `reject`.

## Complete transcript

Request:

```json
{
  "protocol": { "major": 1, "minor": 0 },
  "type": "hello",
  "messageId": "11111111-1111-4111-8111-111111111111",
  "timestamp": "2026-07-15T12:00:00.000Z",
  "payload": {
    "role": "host",
    "implementation": {
      "name": "DearStory.ProtocolProbe.DotNet",
      "version": "0.1.0",
      "language": "csharp",
      "toolchain": ".NET 10.0",
      "binding": "ImGui.NET 1.91.6.1",
      "dearImGuiVersion": "1.91.6",
      "dearImGuiIdentity": "ImGui.NET/ImGui.NET@8e26803be78b344fd68834817905405b3cdffb94"
    },
    "supportedCapabilities": ["control.handshake.v1"],
    "requiredCapabilities": ["control.handshake.v1"]
  }
}
```

Response:

```json
{
  "protocol": { "major": 1, "minor": 0 },
  "type": "welcome",
  "messageId": "22222222-2222-4222-8222-222222222222",
  "correlationId": "11111111-1111-4111-8111-111111111111",
  "timestamp": "2026-07-15T12:00:00.100Z",
  "payload": {
    "peerId": "33333333-3333-4333-8333-333333333333",
    "negotiatedVersion": { "major": 1, "minor": 0 },
    "acceptedCapabilities": ["control.handshake.v1"]
  }
}
```
