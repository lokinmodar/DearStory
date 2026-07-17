# DearStory Frame Transport v1

## Overview

DearStory frame transport v1 defines the initial Windows-first side channel for
host-rendered frames. Control messages remain on named pipes. Pixel data moves
through shared memory and is referenced by control messages such as
`frame_channel_ready` and `frame_presented`.

## Baseline contract

- The transport is Windows-only in the initial implementation plan.
- The host owns rendering and writes completed RGBA frames into shared memory.
- The catalog and runner never receive `ImDrawData`, renderer-owned textures,
  or other Dear ImGui internal structures across the process boundary.
- The baseline pixel format is `rgba8`.
- Each frame channel exposes multiple slots so the host can publish a new frame
  without overwriting a slot that is still being consumed.
- Frame descriptors identify `mappingName`, `slotCount`, `width`, `height`,
  `stride`, `pixelFormat`, `colorSpace`, and a monotonic `sequence`.
- Consumers may ignore stale frames when a newer `sequence` is available.
- Control messages are not dropped to mimic frame dropping.

## Channel lifecycle

1. The host opens or creates a shared-memory mapping for one session.
2. The host announces the mapping through `frame_channel_ready`.
3. The host renders into a slot and emits `frame_presented`.
4. The consumer reads the latest slot and updates preview state.
5. Session teardown closes the mapping after the runner and catalog stop using
   the session.

## Windows mapping expectations

- Mapping names are treated as opaque strings by the protocol.
- Writers and readers must agree on slot count, frame dimensions, and stride.
- Slot payload layout is transport-private; only the announced descriptor is
  protocol-visible at this stage.
- The baseline contract is intentionally portable so a future D3D11
  shared-texture transport can sit behind the same control messages.
