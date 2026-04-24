# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Logs

Read these before starting work — they contain decisions made, problems hit, and unsolved issues:

- [`Updates/2026-04-24-session.md`](Updates/2026-04-24-session.md) — Initial implementation session; gRPC DLL setup journey; switched from Grpc.AspNetCore to Grpc.Core; unsolved: `grpc_csharp_ext` DllNotFoundException in server build
- [`Updates/2026-04-24-session2.md`](Updates/2026-04-24-session2.md) — Phase 1 confirmed working end-to-end; Phase 2 implemented: multi-input proto v2, PlayerInputState, PlayerInputStateRegistry, batched input delivery, HorizontalSpeed NetworkVariable
- [`Updates/2026-04-25-session.md`](Updates/2026-04-25-session.md) — Death detection (Y-position check); in-game debug console UI with ClientRpc; architecture shift to server-owned birds (no Unity client required per player); Phase 2.1 log format only

## What This Project Is

This Unity project is the **game client and dedicated server** component of a larger
IoT-powered multiplayer game system. Physical ESP32 button devices act as game
controllers — a player presses a real button, and their in-game bird flaps.

This is a **proof-of-concept multiplayer 3D Flappy Bird** game. Each connected player
is represented by a colored block (bird) that falls due to gravity. Pressing the
physical button makes the bird flap upward. The goal of the POC is to prove the
full input chain works end-to-end: physical button → cloud → Unity → all clients.

> **This project is one of three components.** See the full system architecture below.
> The other two components are the ESP32 firmware (`/firmware`) and the ASP.NET
> relay server (`/server`).

---

## Full System Architecture

```
[ESP32 #1..N]
  Physical button press
        │
        │  MQTT publish over TLS
        │  topic: game/controllers/{deviceId}/button
        │  broker: HiveMQ Cloud
        ▼
[ASP.NET Core Server]  ← sibling project: /server
  - Subscribes to all button MQTT topics
  - Maps deviceId → playerId via config
  - Forwards to Unity over gRPC
        │
        │  gRPC (HTTP/2 + Protobuf)
        │  contract: game_controller.proto
        ▼
[Unity Dedicated Server]  ← THIS PROJECT (server build)
  - Embedded Kestrel gRPC server (port 9090, background thread)
  - ConcurrentQueue bridges gRPC thread → Unity main thread
  - Receives ButtonEvent { playerId, eventType }
  - Applies flap impulse to the correct PlayerBird
  - Replicates game state to all clients via NGO
        │
        │  Netcode for GameObjects (NGO) over Unity Transport
        │  game clients connect on port 7777
        ▼
[Unity Game Client]  ← THIS PROJECT (client build)
  - Connects to dedicated server with playerId in ConnectionApproval payload
  - Renders all PlayerBird objects with their synced colors
  - No direct input handling — all input comes from physical buttons via gRPC
```

---

## POC Game Design & Scope

### What the POC does
- Each Unity client connects with a `playerId` (e.g. `"player-1"`)
- On connect, a `PlayerBird` NetworkObject is spawned and assigned a unique color
- Gravity pulls all birds downward continuously
- When the matching ESP32 button is pressed, the server applies an upward velocity
  impulse to that player's bird (the "flap")
- All clients see all birds moving in real time via NGO replication

### Explicitly out of scope for POC
- Pipes / obstacles
- Scoring or win condition
- Death and respawn
- Lobby or matchmaking
- More than 4 simultaneous players
- Mobile client

---

## Project Overview

ATITProject is an early-stage multiplayer game built with **Unity 6 LTS (6000.3.11f1)**
using **Netcode for GameObjects** (NGO v2.9.1) for real-time multiplayer synchronization.
The rendering pipeline is URP 17.3.0.

---

## Development Commands

Unity projects are built and run through the Unity Editor — there is no CLI build
command for normal development. Open `ATITProject.sln` in Rider or Visual Studio
for C# editing with full IntelliSense.

To run Play Mode tests via CLI (requires Unity installed):
```
"<UnityEditorPath>/Unity.exe" -runTests -testPlatform editmode -projectPath . -testResults results.xml
```

To build as a dedicated server (headless, no graphics):
```
File > Build Settings → Dedicated Server platform → Enable "Server Build" → Build
```

Run the dedicated server build:
```bash
./ATITProject.x86_64 -batchmode -nographics -grpcPort 9090 -gamePort 7777 -logFile server.log
```

---

## Code Architecture

### Namespace & Directory Layout

All custom scripts live under `Assets/Scripts/` organized by namespace:

- **`Players/`** — Per-player components attached to the player prefab (`PlayerContainer.prefab`)
  - `PlayerMovement` — `CharacterController`-based movement with NGO ownership checks
  - `PlayerDistinguisher` — Owns and syncs each player's color via `NetworkVariable<Color>`
  - `PlayerBird` — `NetworkBehaviour` on the bird prefab; owns `NetworkVariable<Color> PlayerColor`
    and `NetworkVariable<bool> IsAlive`; server-side `Flap()` method applies upward Rigidbody impulse

- **`Managers/`** — Centralized game systems, persist across scenes
  - `PlayerDistinguisherManager` — Maintains a pool of assignable colors; distributes/reclaims
    colors on player connect/disconnect
  - `BirdSpawnManager` — `NetworkSingleton<BirdSpawnManager>`; spawns a `PlayerBird` NetworkObject
    when a client is approved; tracks `playerId → NetworkObject` mapping for gRPC input routing
  - `IoTInputManager` — `NetworkSingleton<IoTInputManager>`; drains `IoTGameServiceImpl.InputQueue`
    each `Update()`; routes button events to the correct `PlayerBird` via `BirdSpawnManager`

- **`Server/`** — Dedicated server only; all code in this folder must be guarded with `#if UNITY_SERVER`
  - `ServerBootstrap` — `MonoBehaviour`; starts NGO as server and starts `GrpcServerHost` in `Start()`;
    parses `-grpcPort` and `-gamePort` command-line args; calls `GrpcServerHost.Stop()` in `OnDestroy()`
  - `GrpcServerHost` — Static class; builds and runs an embedded Kestrel/ASP.NET Core host on a
    background thread; exposes `Start(int port)` and `Stop()`
  - `IoTGameServiceImpl` — gRPC service implementation; enqueues received events into
    `static ConcurrentQueue<ButtonInputEvent> InputQueue`; never touches Unity API directly
  - `ConnectionApprovalHandler` — Validates NGO `ConnectionApprovalRequest`; reads `playerId`
    from the approval payload bytes; rejects unknown or duplicate player IDs

- **`Shared/`** — Reusable base classes
  - `NetworkSingleton<T>` — Thread-safe singleton base extending `NetworkBehaviour`;
    uses `DontDestroyOnLoad`; all networked managers should inherit this

- **`Generated/`** — Auto-generated gRPC stubs; **do not edit manually**
  - `GameController.cs` — Protobuf generated from `Proto/game_controller.proto`
  - `GameControllerGrpc.cs` — gRPC stub generated from `Proto/game_controller.proto`

### Key Patterns

**NetworkSingleton** is the standard base for any manager that must exist once per
session and be network-aware. Always extend `NetworkSingleton<T>` rather than a plain
`MonoBehaviour` singleton for managers that touch NGO state.

**NetworkVariable** is used for any player state that all clients must observe (e.g.,
color, flap state). Server-authoritative writes, client reads.

**Ownership checks** (`IsOwner`) guard local-only logic inside `NetworkBehaviour`
components (e.g., input handling in `PlayerMovement`).

**`#if UNITY_SERVER`** guards all gRPC and server-only code. Client builds must never
include `GrpcServerHost`, `IoTGameServiceImpl`, or Kestrel dependencies.

**ConcurrentQueue threading rule:** gRPC callbacks arrive on Kestrel background threads.
Never call any Unity or NGO API from inside `IoTGameServiceImpl`. Only enqueue to
`InputQueue`. All Unity calls happen in `IoTInputManager.Update()` on the main thread.
This is the most critical correctness constraint in the codebase — violations cause
non-deterministic crashes.

---

## gRPC Server Setup (Dedicated Server Only)

The Unity dedicated server embeds a Kestrel gRPC server alongside NGO.

### Proto Contract (`Proto/game_controller.proto`)

Must be **identical** to the copy in `/server/Proto/game_controller.proto`.
Both projects generate stubs from the same file. Any change must be coordinated
and version-bumped across both projects simultaneously.

```proto
syntax = "proto3";

option csharp_namespace = "GameController";

package game_controller;

service GameControllerService {
  rpc SendButtonEvent (ButtonEventRequest) returns (ButtonEventResponse);
}

message ButtonEventRequest {
  string device_id  = 1;
  string player_id  = 2;
  string event_type = 3;  // "pressed" or "released"
  int64  timestamp  = 4;
}

message ButtonEventResponse {
  bool   accepted = 1;
  string message  = 2;
}
```

Generate stubs and place in `Assets/Scripts/Generated/`:
```bash
protoc --csharp_out=Assets/Scripts/Generated \
       --grpc_out=Assets/Scripts/Generated \
       --plugin=protoc-gen-grpc=grpc_csharp_plugin \
       Proto/game_controller.proto
```

### gRPC DLLs (`Assets/Plugins/grpc/`)

Place `netstandard2.1` DLLs here. Required:
- `Grpc.AspNetCore.Server.dll`
- `Grpc.Core.Api.dll`
- `Google.Protobuf.dll`

### IL2CPP Stripping (`Assets/link.xml`)

Required to prevent IL2CPP from stripping gRPC reflection internals in server builds.
Without this the dedicated server build will crash at runtime with no clear error.

```xml
<linker>
  <assembly fullname="Google.Protobuf"        preserve="all"/>
  <assembly fullname="Grpc.Core.Api"          preserve="all"/>
  <assembly fullname="Grpc.AspNetCore.Server" preserve="all"/>
  <assembly fullname="System.Net.Http"        preserve="all"/>
</linker>
```

---

## Player Connection Flow

1. Unity client calls `NetworkManager.Singleton.StartClient()` with a
   `ConnectionApprovalRequest` payload containing the UTF-8 encoded `playerId` string
2. `ConnectionApprovalHandler` on the server decodes and validates the `playerId`
3. On approval, `BirdSpawnManager.OnClientConnectedCallback()` fires:
   - Spawns a `PlayerBird` NetworkObject at a staggered start position
   - Assigns a color from `PlayerDistinguisherManager`
   - Registers `playerId → NetworkObject` in its internal dictionary
4. When the client disconnects, `BirdSpawnManager` despawns the bird and
   `PlayerDistinguisherManager` reclaims the color

---

## Prefabs

### `PlayerBird` (NetworkObject)
- `Rigidbody` — `useGravity = true`, freeze X/Z rotation constraints
- `MeshRenderer` — URP-compatible `UniversalRenderPipeline/Lit` material; color
  driven by `PlayerColor` NetworkVariable change callback
- `PlayerBird` (NetworkBehaviour):
  - `NetworkVariable<Color> PlayerColor` — write: server only, read: all
  - `NetworkVariable<bool> IsAlive` — reserved for future use, default true
  - `public void Flap()` — server-only; call `Rigidbody.AddForce(Vector3.up * flapForce, ForceMode.Impulse)`
  - `OnPlayerColorChanged()` — NetworkVariable change callback; sets `MeshRenderer.material.color`

---

## Scenes

`Assets/Scenes/SampleScene.unity` is the only scene currently. New scenes should be
added to Build Settings to be reachable via `SceneManager`.

**Server scene setup:** `SampleScene` contains a persistent GameObject with
`ServerBootstrap` attached. `ServerBootstrap.Start()` checks `#if UNITY_SERVER` and
only activates the gRPC host and NGO server in dedicated server builds. In client
builds the same scene starts NGO as a client instead.

---

## Packages of Note

| Package | Version | Purpose |
|---|---|---|
| `com.unity.netcode.gameobjects` | 2.9.1 | Core multiplayer |
| `com.unity.inputsystem` | 1.19.0 | Input (use new Input System, not legacy) |
| `com.unity.render-pipelines.universal` | 17.3.0 | URP — shaders must be URP-compatible |
| `com.unity.ai.navigation` | 2.0.11 | NavMesh / pathfinding |
| `com.unity.test-framework` | 1.6.0 | Edit/Play mode tests |

---

## Configuration

Runtime config passed as command-line args to the dedicated server build.
Parse in `ServerBootstrap.Start()` using `System.Environment.GetCommandLineArgs()`.

| Arg | Default | Description |
|---|---|---|
| `-grpcPort` | `9090` | Port for embedded gRPC server (ASP.NET connects here) |
| `-gamePort` | `7777` | Port for NGO/UTP game clients |


# Phase 2 — Multi-Input, Multi-Device, Multi-Player

## What Changed in Phase 2

Phase 1 assumed one button per player, one device per player, and a single input type.
Phase 2 expands Unity's side to handle:

- **Multiple named inputs per player** — jump, fire, speed, tilt, etc.
- **Multiple devices per player** — Unity doesn't care; ASP.NET resolves this before
  events arrive. Unity only ever sees `playerId`, never device topology.
- **Multiple input types** — buttons (digital), knobs (continuous 0–1), sensors (float)
- **Batched input delivery** — events arrive as `SendInputBatch` gRPC calls, which may
  contain multiple events from multiple players accumulated over a buffer window
---

## Architectural Decisions That Affect Unity

### Decision: Unity receives only fully-resolved events

Unity never receives `deviceId` or raw hardware values that need interpretation.
By the time an event reaches the gRPC server in Unity, the ASP.NET server has already:
- Resolved `(deviceId, inputName) → playerId`
- Normalized knob/sensor values to `0.0–1.0`
- Batched high-frequency events into a single call
  Unity's job is purely: **receive `(playerId, inputName, value)` → apply to game state**.

### Decision: Input state aggregation lives in Unity

The ASP.NET server delivers individual events. Unity is responsible for tracking
*combined* input state per player — e.g. "player-1 is currently holding jump AND fire".

`PlayerInputState` (new in Phase 2) is a per-player struct that tracks the last known
value of every named input. `IoTInputManager` updates it on each incoming event.
`PlayerBird` reads from `PlayerInputState` each `FixedUpdate()` to apply physics.

This is intentional: what "holding jump + fire simultaneously" *means* is game logic,
not protocol logic.

### Decision: ConcurrentQueue threading rule is unchanged

`SendInputBatch` arrives on a Kestrel background thread, same as Phase 1's
`SendButtonEvent`. The entire batch is enqueued atomically. `IoTInputManager.Update()`
drains the queue on the main thread. **No Unity API calls inside gRPC handlers — ever.**
 
---

## Phase 2 Proto Contract (Breaking Change)

Package renamed to `game_controller.v2`. Must be **identical** to
`/server/Proto/game_controller_v2.proto`. Regenerate stubs in `Assets/Scripts/Generated/`
after any change.

```proto
syntax = "proto3";
 
option csharp_namespace = "GameController.V2";
 
package game_controller.v2;
 
service GameControllerService {
  rpc SendInputEvent  (InputEventRequest)  returns (InputEventResponse);
  rpc SendInputBatch  (InputBatchRequest)  returns (InputEventResponse);
}
 
message InputEventRequest {
  string player_id       = 1;
  string device_id       = 2;
  string input_name      = 3;
  string input_type      = 4;  // "button", "knob", "sensor"
  string event_type      = 5;  // "pressed", "released", "change"
  float  value           = 6;  // always normalized 0.0–1.0
  int64  timestamp       = 7;
  int64  sequence_number = 8;
}
 
message InputBatchRequest {
  repeated InputEventRequest events = 1;
}
 
message InputEventResponse {
  bool   accepted = 1;
  string message  = 2;
}
```

Regenerate stubs:
```bash
protoc --csharp_out=Assets/Scripts/Generated \
       --grpc_out=Assets/Scripts/Generated \
       --plugin=protoc-gen-grpc=grpc_csharp_plugin \
       Proto/game_controller_v2.proto
```
 
---

## Phase 2 Directory Layout Changes

New and updated scripts under `Assets/Scripts/`:

- **`Players/`**
  - `PlayerBird` — updated: reads from `PlayerInputState` in `FixedUpdate()` instead of
    being called directly; handles `"jump"` button and `"speed"` knob separately
  - `PlayerInputState` — **new**; plain C# struct (not a MonoBehaviour); holds last known
    `float` value per named input; updated by `IoTInputManager` on main thread
- **`Managers/`**
  - `IoTInputManager` — updated: drains `ConcurrentQueue<InputBatchEnvelope>` instead of
    single events; for each event in each batch, updates `PlayerInputState` for the
    correct player, then calls relevant handler on `PlayerBird`
  - `PlayerInputStateRegistry` — **new**; `NetworkSingleton<PlayerInputStateRegistry>`;
    owns a `Dictionary<string, PlayerInputState>` keyed by `playerId`; thread-safe reads
    from `IoTInputManager.Update()` only (main thread — no lock needed)
- **`Server/`**
  - `IoTGameServiceImpl` — updated: implements `SendInputBatch` RPC; enqueues the entire
    `InputBatchRequest` as one `InputBatchEnvelope` into the `ConcurrentQueue` atomically
  - `GrpcServerHost` — minor update: registers updated v2 service impl
---

## Phase 2 Input Handling Model

### `PlayerInputState` (new)

```csharp
public struct PlayerInputState
{
    // Keyed by inputName e.g. "jump", "speed", "fire"
    public Dictionary<string, float> Values;
 
    public float Get(string inputName) =>
        Values.TryGetValue(inputName, out var v) ? v : 0f;
 
    public void Set(string inputName, float value) =>
        Values[inputName] = value;
 
    public bool IsPressed(string inputName) => Get(inputName) >= 0.5f;
}
```

### `IoTInputManager.Update()` flow (updated)

```
Drain ConcurrentQueue<InputBatchEnvelope>
  For each InputBatchEnvelope:
    For each InputEventRequest in the batch:
      1. Get or create PlayerInputState for event.PlayerId
      2. Update state: state.Set(event.InputName, event.Value)
      3. Notify PlayerBird:
         - If inputType == "button" && event == "pressed" → call bird.OnButtonPressed(inputName)
         - If inputType == "button" && event == "released" → call bird.OnButtonReleased(inputName)
         - If inputType == "knob" || "sensor" → call bird.OnAnalogChanged(inputName, value)
```

### `PlayerBird` updates (Phase 2)

- `OnButtonPressed(string inputName)` — switch on `inputName`:
  - `"jump"` → apply flap impulse (same as Phase 1)
  - `"fire"` → reserved for future game mechanic
- `OnButtonReleased(string inputName)` — handle release logic if needed
- `OnAnalogChanged(string inputName, float value)` — switch on `inputName`:
  - `"speed"` → store value; applied in `FixedUpdate()` as horizontal force multiplier
- `FixedUpdate()` — applies continuous effects from current `PlayerInputState`
  (e.g. sustained horizontal movement from knob value)
---

## Phase 2 Sequence Number Handling

Each `InputEventRequest` carries a `sequenceNumber` from the firmware. Unity should:
- Log a warning if `sequenceNumber` is non-consecutive for a given `deviceId`
  (indicates a dropped MQTT message)
- **Not** attempt reordering or replay — the game is non-competitive and dropped inputs
  are acceptable; just log for diagnostics
  Track last seen sequence number per `deviceId` in `PlayerInputStateRegistry`.

---

## Phase 2 New NetworkVariables on `PlayerBird`

| NetworkVariable | Type | Purpose |
|---|---|---|
| `PlayerColor` | `Color` | Unchanged from Phase 1 |
| `IsAlive` | `bool` | Unchanged, still reserved |
| `HorizontalSpeed` | `float` | Synced knob value → affects client-side visual feedback |

`HorizontalSpeed` is written server-side from the knob input and read client-side
to drive any visual speed indicator (e.g. color intensity, trail effect).

---

# Phase 2.1 — Protocol Fix: Separate Physical Slot Identity from Semantic Input Name

## What Changed in Phase 2.1

Phase 2 had a naming flaw where `inputName` carried both physical slot identity and
semantic game meaning. Phase 2.1 fixes this by making the ASP.NET server the sole
translation layer between physical hardware and game semantics.

**Unity is unaffected by this change.** The fix is entirely in the firmware and
ASP.NET server. Unity already received `inputName` (semantic) and `playerId` — and
it continues to do so. The difference is that these values are now correctly resolved
server-side from a clean registry, rather than being hacked into the slot name with
prefixes like `"p1-jump"`.
 
---

## What Unity Sees — Unchanged, Now Actually Correct

Before Phase 2.1, the naming convention `"p1-jump"` / `"p2-jump"` was leaking device
topology into Unity's input namespace. Unity was implicitly dependent on a naming
convention it had no control over.

After Phase 2.1, Unity receives clean semantic events with no trace of physical layout:

```
player-1  →  inputName: "jump"   value: 1.0   (from btn1 on esp32-shared)
player-2  →  inputName: "jump"   value: 1.0   (from btn2 on esp32-shared)
```

Both players pressing their respective `"jump"` buttons now produces identical,
unambiguous events distinguished only by `playerId`. `PlayerBird` and
`PlayerInputStateRegistry` require no changes — they already key on `playerId` +
`inputName`, which is exactly the right key.
 
---

## No Code Changes Required in Unity

Phase 2.1 is a **firmware + server-side fix only.** Every Unity script, NetworkBehaviour,
and input handler written for Phase 2 continues to work without modification.

For reference, the gRPC proto is unchanged:

```proto
// Phase 2.1 — proto identical to Phase 2
message InputEventRequest {
  string player_id       = 1;
  string device_id       = 2;  // retained for diagnostics only, not used in game logic
  string input_name      = 3;  // semantic label — this is what all Unity code keys on
  string input_type      = 4;
  string event_type      = 5;
  float  value           = 6;
  int64  timestamp       = 7;
  int64  sequence_number = 8;
}
```

`device_id` is present for logging/diagnostics but Unity game logic must never branch
on `device_id`. All game logic keys exclusively on `player_id` + `input_name`.
 
---

## Diagnostic Logging Recommendation

Since `device_id` and `slotId` are no longer semantically meaningful to Unity,
log them at `Debug` level only — useful for tracing input routing issues without
cluttering game logic logs:

```csharp
// In IoTInputManager.Update() when processing each event:
Debug.Log($"[IoT] player={evt.PlayerId} input={evt.InputName} " +
          $"device={evt.DeviceId} value={evt.Value}");
```

This gives a full trace from physical device to game action without coupling game
logic to physical device identity.
 
 
 
 