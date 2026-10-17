# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Logs

Read these before starting work — they contain decisions made, problems hit, and unsolved issues:

- [`docs/logs/2026-04-24.md`](docs/logs/2026-04-24.md) — Initial implementation session; gRPC DLL setup journey; switched from Grpc.AspNetCore to Grpc.Core; unsolved: `grpc_csharp_ext` DllNotFoundException in server build
- [`docs/logs/2026-04-24-2.md`](docs/logs/2026-04-24-2.md) — Phase 1 confirmed working end-to-end; Phase 2 implemented: multi-input proto v2, PlayerInputState, PlayerInputStateRegistry, batched input delivery, HorizontalSpeed NetworkVariable
- [`docs/logs/2026-04-25.md`](docs/logs/2026-04-25.md) — Death detection (Y-position check); in-game debug console UI with ClientRpc; architecture shift to server-owned birds (no Unity client required per player); Phase 2.1 log format only
- [`docs/logs/2026-04-27.md`](docs/logs/2026-04-27.md) — Phase 3 full implementation (space shooter); Phase 3.1 humidity; Phase 3.2 gravity/spawn-rate swap; protoc fix for SendEnvironmentEvent; first play test debugging (CalloutManager crash, false win condition, boss falling too fast, round reset leaving stale entities)
- [`docs/logs/2026-04-29.md`](docs/logs/2026-04-29.md) — Phase 4 folder migration to IoTGame/Game split; Phase 5 full implementation (horizontal shooter redesign, BirdProjectile, PlayerShipUI, GameSessionManager simplification)
- [`docs/logs/2026-05-06.md`](docs/logs/2026-05-06.md) — Phase 6 mTLS: replaced Grpc.Core.Server with Kestrel + mutual TLS; removed shared-secret validation; added CLI cert path args; new Assets/Certs/ folder

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
 
---

# Phase 3 — Space Shooter: Full Game Implementation

## What This Phase Builds

Phase 3 replaces the Flappy Bird POC with a complete asymmetric multiplayer space
shooter. The pipeline (ESP32 → MQTT → ASP.NET → gRPC → Unity) is unchanged.
All changes are within Unity itself.
 
---

## Game Design

### Concept
An asymmetric multiplayer space shooter. Two sides with opposing goals, one stage
player who controls the environment for everyone.

### Roles

| Role | Players | Controls | Goal |
|---|---|---|---|
| Ship | Player 1, Player 2 | Joystick (move) + button (fire) | Destroy both boss birds |
| Boss bird | Player 3, Player 4 | Button (flap) | Survive / reach the bottom |
| Stage | Stage player | DHT11 + ultrasonic + button | Chaos — affects everyone |

### Win Conditions

| Condition | Winner |
|---|---|
| Both boss birds reach 0 HP | Ship players win |
| All ships reach 0 HP | Bird players win |
| Boss bird reaches bottom of screen | Bird players win |
| Round timer expires | Side with more HP% remaining wins |

### Screen Layout

```
╔══════════════════════════════════════╗
║  [Boss HP bars]    [Zone indicator]  ║  ← top HUD
║                                      ║
║   Boss Bird 1      Boss Bird 2       ║  ← boss zone (top half)
║      🐦                🐦            ║
║   [Minion birds scattered]           ║
║ ════════════════════════════════════ ║  ← boundary line
║   🚀 Ship 1          🚀 Ship 2       ║  ← ship zone (bottom half)
║                                      ║
║  [Ship HP bars]   [Round timer]      ║  ← bottom HUD
╚══════════════════════════════════════╝
```

### Pipes (optional mechanic)
Horizontal barriers with a gap periodically descend from the top. `pipe-flip`
button toggles whether the gap is in the upper or lower half. Affects both
boss birds (navigation) and projectiles (blocked by solid sections).
If no stage device registers `pipe-flip`, pipes do not spawn — the base
game runs without them.
 
---

## Round Lifecycle

```
Waiting
  → Ship players press "fire" to ready up (ShipReady flags)
  → Boss players press "flap" to ready up (BirdReady flags)
  → ALL four players ready OR admin POST /api/admin/start
        ↓
Countdown (5 seconds)
  → Any player disconnects → back to Waiting
        ↓
Active
  → IoT input processed, physics running, spawning active
  → Win condition met → Ended
  → Admin POST /api/admin/end → Ended
        ↓
Ended (10 second results screen)
  → Callout: "SHIPS WIN" or "BIRDS WIN"
  → All ready flags reset
  → Timer expires → back to Waiting
```

### Ready-Up Logic
- During `Waiting` phase only, `"fire"` input on ship player = toggle ready
- During `Waiting` phase only, `"flap"` input on bird player = toggle ready
- Same buttons used for gameplay — `IoTInputManager` routes by current phase
- Individual ready state shown in waiting screen UI per player slot
### Environmental Sensor During Waiting
- Sensor events still received and stored in `EnvironmentState`
- Effects not applied — `EnvironmentController` checks `RoundPhase.Active`
- Zone indicator still visible on waiting screen (good for demo setup verification)
---

## Disconnect Policy

| Scenario | Result |
|---|---|
| One ship disconnects, other remains | Round continues, disconnected ship despawns |
| Both ships disconnect | Round ends immediately, bird players win |
| One bird disconnects, other remains | Round continues, disconnected bird despawns |
| Both birds disconnect | Round ends immediately, ship players win |
| Any disconnect during Countdown | Back to Waiting, all ready flags reset |
| Stage device disconnects | Last known zone/temp values held, warning logged |
 
---

## NetworkObject Hierarchy

### `PlayerShip` (new)
- `Rigidbody2D` — kinematic, velocity set from joystick each `FixedUpdate`
- `NetworkVariable<int> ShipHealth` — starts at 100, reduced by minion collision
- `NetworkVariable<bool> ShieldActive` — driven by `EnvironmentController`
- `NetworkVariable<int> ShieldHealth` — starts at 50, absorbs damage when active
- `NetworkVariable<Color> ShipColor` — assigned by `PlayerDistinguisherManager`
- Server-only: `TakeDamage(int amount)` — checks shield first, then ship HP
- Server-only: `SetVelocity(Vector2 v)` — called by `IoTInputManager` from joystick
### `BossEnemyBird` (replaces `PlayerBird` for bird role)
- Reuses Phase 1 `PlayerBird` Rigidbody + flap physics verbatim
- `NetworkVariable<int> BossHealth` — starts at 100, reduced by projectile hits
- `NetworkVariable<Color> BossColor` — assigned by `PlayerDistinguisherManager`
- Server-only: `Flap()` — same as Phase 1, triggered by `"flap"` input from bird player
- Server-only: `TakeDamage(int amount)` — reduces `BossHealth`, checks win condition
- Despawn condition: reaches bottom of screen OR `BossHealth <= 0`
### `EnemyBird` (minion — server-controlled)
- Same Rigidbody + flap physics as `PlayerBird`
- No `playerId` owner — server-controlled entirely
- Server-side coroutine: `Flap()` called every `Random.Range(1f, 3f)` seconds
- On collision with ship boundary: calls `PlayerShip.TakeDamage(10)`, then despawns
- On round end: all live minions despawned by `EnemySpawnManager`
### `Projectile` (new)
- Spawned by `PlayerShip` on `"fire"` input during `Active` phase
- Travels upward at fixed speed
- On collision with `BossEnemyBird`: calls `BossEnemyBird.TakeDamage(20)`, despawns
- On collision with `EnemyBird`: despawns both (projectile + minion), add score
- Destroyed when reaching top of screen
- Max 3 live projectiles per ship at once — enforced server-side
### `PipeBarrier` (optional — only if stage device present)
- Horizontal barrier NetworkObject with a gap
- `NetworkVariable<bool> GapIsUpper` — toggled by `"pipe-flip"` input
- Descends from top at fixed speed, despawns at bottom
- Spawned by `PipeSpawnManager` every 8 seconds if stage device is registered
---

## New Singletons (all extend `NetworkSingleton<T>`)

### `GameRoundManager`
```csharp
public class GameRoundManager : NetworkSingleton<GameRoundManager>
{
    public NetworkVariable<RoundPhase> CurrentPhase;
    public NetworkVariable<float>      RoundTimer;       // counts down in Active
    public NetworkVariable<bool>       Ship1Ready;
    public NetworkVariable<bool>       Ship2Ready;
    public NetworkVariable<bool>       Bird1Ready;
    public NetworkVariable<bool>       Bird2Ready;
 
    // Server-only
    void Update();                              // drives countdown + round timer
    void CheckWinConditions();                  // called each Update during Active
    public void TransitionTo(RoundPhase phase); // fires callout + state change
    public void HandleAdminCommand(string cmd); // "start_round", "end_round", "reset"
    public void HandlePlayerDisconnect(string playerId);
}
 
public enum RoundPhase { Waiting, Countdown, Active, Ended }
```

### `EnvironmentController`
```csharp
public class EnvironmentController : NetworkSingleton<EnvironmentController>
{
    public NetworkVariable<float> Temperature;    // raw Celsius from DHT11
    public NetworkVariable<int>   UltrasonicZone; // 1-4, resolved server-side
    public NetworkVariable<int>   ActiveZoneEffect; // mirrors UltrasonicZone for UI
 
    // Per-effect cooldown timestamps (server-only, not NetworkVariables)
    float _lastShieldTrigger;   // Zone A — 15s cooldown
    float _lastTimeSlow;        // Zone B — 20s cooldown
    float _lastFrenzy;          // Zone D — 10s cooldown
 
    // Called by IoTGameServiceImpl when EnvironmentEventRequest arrives
    public void OnEnvironmentUpdate(string envKey, float value);
 
    // Called each Update during Active phase only
    void ApplyZoneEffects();
    void ApplyTemperatureEffects();
}
```

### Zone effects (applied in `ApplyZoneEffects()`)

| Zone | Ship effect | Bird effect | Cooldown |
|---|---|---|---|
| 1 (0–20cm) | Shield activates (50 HP buffer) | Boss bird flap impulse +30% | 15s |
| 2 (20–60cm) | `GlobalGameSpeed = 0.6` | `GlobalGameSpeed = 0.6` (affects both equally) | 20s |
| 3 (60–120cm) | No effect | No effect | — |
| 4 (120cm+) | More minions + minions deal +50% damage | Boss bird loses 2 HP/sec | 10s |

Zone effects are removed immediately when zone changes (except shield HP which
persists until depleted or recharged).

### Temperature effects

```
temp < 20°C  → SpawnInterval = 8s  (slow)
20–28°C      → SpawnInterval = 5s  (normal)
28–34°C      → SpawnInterval = 3s  (fast)
> 34°C       → SpawnInterval = 1.5s (aggressive)
```

Zone D frenzy halves the current `SpawnInterval` for its duration.
Cap: `SpawnInterval` floor of 1s regardless of combined effects.

### `GlobalGameSpeed` pattern (NOT `Time.timeScale`)

```csharp
// NetworkVariable on EnvironmentController
public NetworkVariable<float> GlobalGameSpeed; // default 1.0
 
// All physics-driven objects read this each FixedUpdate:
rb.velocity = targetVelocity * EnvironmentController.Instance.GlobalGameSpeed.Value;
```

Never use `Time.timeScale` — it breaks NGO tick rate. All velocities, spawn
timers, and animation speeds multiply by `GlobalGameSpeed` manually.

### `EnemySpawnManager`
```csharp
public class EnemySpawnManager : NetworkSingleton<EnemySpawnManager>
{
    float _spawnInterval;       // driven by EnvironmentController.Temperature
    List<NetworkObject> _liveMinions;
 
    void Update();              // only runs during Active phase
    void SpawnMinion();         // random X position at top of screen
    public void DespawnAll();   // called on round end
    public void SetSpawnInterval(float seconds);
}
```

### `RoundUIManager` (client-side — not a NetworkSingleton)
Observes NetworkVariables and switches UI panels:

| Phase | UI shown |
|---|---|
| Waiting | Player slots with ready indicators, zone status strip |
| Countdown | Large countdown number (5,4,3,2,1) |
| Active | HUD: boss HP bars, ship HP bars, zone indicator, round timer |
| Ended | "SHIPS WIN" or "BIRDS WIN", score summary |

F4 key toggles tutorial overlay at any time, any phase. Tutorial shows all
controls for all roles — purely local, no networking.

### `CalloutManager` (NetworkSingleton — server fires, all clients display)
```csharp
public class CalloutManager : NetworkSingleton<CalloutManager>
{
    [ClientRpc]
    public void ShowCalloutClientRpc(string text, float duration);
}
```

Callout triggers:

| Event | Text | Duration |
|---|---|---|
| Countdown 3,2,1 | "3" / "2" / "1" | 0.8s each |
| Round start | "FIGHT!" | 1.5s |
| Zone A entered | "SHIELD ACTIVATED" | 2s |
| Zone A cooldown | "SHIELD COOLDOWN" | 2s |
| Zone B entered | "TIME SLOW" | 2s |
| Zone D entered | "FRENZY" | 2s |
| Zone → normal | "NORMAL" | 1.5s |
| Boss HP ≤ 25% | "BOSS CRITICAL" | 2s |
| Ship destroyed | "SHIP DOWN" | 2s |
| Shield broken | "SHIELD BROKEN" | 2s |
| Minion surge | "MINION SURGE" | 2s |
| Ship players win | "SHIPS WIN" | 8s |
| Bird players win | "BIRDS WIN" | 8s |
 
---

## `IoTInputManager` Phase 3 Update

Routes by `playerId` AND `inputName`. Phase guard unchanged.

```csharp
void Update()
{
    if (GameRoundManager.Instance.CurrentPhase.Value == RoundPhase.Waiting)
    {
        // Special case: ready-up inputs only
        DrainReadyUpInputs();
        return;
    }
    if (GameRoundManager.Instance.CurrentPhase.Value != RoundPhase.Active)
        return;
 
    DrainGameplayInputs();
}
 
void DrainGameplayInputs()
{
    while (IoTGameServiceImpl.InputQueue.TryDequeue(out var batch))
        foreach (var evt in batch.Events)
            RouteGameplayEvent(evt);
}
 
void RouteGameplayEvent(InputEventRequest evt)
{
    switch (evt.PlayerId)
    {
        case "player-1":
        case "player-2":
            RouteShipInput(evt); break;
        case "player-3":
        case "player-4":
            RouteBirdInput(evt); break;
        case "stage":
            RouteStageInput(evt); break;
    }
}
```

### Ship input routing
```csharp
void RouteShipInput(InputEventRequest evt)
{
    var ship = BirdSpawnManager.Instance.GetShip(evt.PlayerId);
    if (ship == null) return;
    switch (evt.InputName)
    {
        case "move-x":  ship.SetJoystickX(evt.Value); break;  // -1 to 1 (0.5 = center)
        case "move-y":  ship.SetJoystickY(evt.Value); break;
        case "fire":    if (evt.EventType == "pressed") ship.Fire(); break;
        case "special": break; // reserved
    }
}
```

### Bird input routing
```csharp
void RouteBirdInput(InputEventRequest evt)
{
    var bird = BirdSpawnManager.Instance.GetBossBird(evt.PlayerId);
    if (bird == null) return;
    if (evt.InputName == "flap" && evt.EventType == "pressed")
        bird.Flap();
}
```

### Stage input routing
```csharp
void RouteStageInput(InputEventRequest evt)
{
    if (evt.InputName == "pipe-flip" && evt.EventType == "pressed")
        PipeSpawnManager.Instance?.FlipGap();
}
```
 
---

## `IoTGameServiceImpl` Phase 3 Update

Now implements both `SendInputBatch` and `SendEnvironmentEvent`:

```csharp
public override Task<InputEventResponse> SendEnvironmentEvent(
    EnvironmentEventRequest request, ServerCallContext context)
{
    // Enqueue to a separate environment queue — never touches Unity API here
    EnvironmentQueue.Enqueue(new EnvironmentEvent {
        EnvKey    = request.EnvKey,
        RawValue  = request.RawValue,
        Timestamp = request.Timestamp
    });
    return Task.FromResult(new InputEventResponse { Accepted = true });
}
 
public static ConcurrentQueue<EnvironmentEvent> EnvironmentQueue = new();
```

`EnvironmentController` drains `EnvironmentQueue` each `Update()` on main thread,
same `ConcurrentQueue` threading pattern as `InputQueue`.

Also implements `SendAdminCommand`:
```csharp
public override Task<AdminCommandResponse> SendAdminCommand(
    AdminCommandRequest request, ServerCallContext context)
{
    AdminCommandQueue.Enqueue(request.Command);
    return Task.FromResult(new AdminCommandResponse { Accepted = true });
}
 
public static ConcurrentQueue<string> AdminCommandQueue = new();
```

`GameRoundManager` drains `AdminCommandQueue` each `Update()`.
 
---

## Shield System Detail

```csharp
// On PlayerShip
void TakeDamage(int amount)
{
    if (ShieldActive.Value && ShieldHealth.Value > 0)
    {
        int absorbed = Mathf.Min(amount, ShieldHealth.Value);
        ShieldHealth.Value -= absorbed;
        amount -= absorbed;
        if (ShieldHealth.Value <= 0)
        {
            ShieldActive.Value = false;
            CalloutManager.Instance.ShowCalloutClientRpc("SHIELD BROKEN", 2f);
        }
    }
    if (amount > 0)
        ShipHealth.Value -= amount;
}
 
// Shield recharge (in EnvironmentController)
// When zone returns to A and cooldown elapsed: restore ShieldHealth to 50,
// set ShieldActive = true on all ships that are still alive
```
 
---

## Phase 3 Project Structure

```
Assets/Scripts/
├── Players/
│   ├── PlayerShip.cs           # New — ship NetworkBehaviour
│   ├── BossEnemyBird.cs        # New — boss bird (reuses PlayerBird physics)
│   ├── EnemyBird.cs            # New — server-controlled minion
│   ├── Projectile.cs           # New — fired by PlayerShip
│   └── PlayerDistinguisher.cs  # Unchanged
├── Managers/
│   ├── GameRoundManager.cs     # New — round lifecycle
│   ├── EnvironmentController.cs# New — zone + temp effects
│   ├── EnemySpawnManager.cs    # New — minion spawning
│   ├── BirdSpawnManager.cs     # Updated — manages ships + boss birds
│   ├── IoTInputManager.cs      # Updated — phase guard + role routing
│   ├── CalloutManager.cs       # New — networked callout system
│   ├── PipeSpawnManager.cs     # New (optional) — pipe barriers
│   └── PlayerDistinguisherManager.cs # Unchanged
├── UI/
│   └── RoundUIManager.cs       # New — client-side UI phase switching
├── Server/
│   ├── ServerBootstrap.cs      # Updated — handles AdminCommandQueue
│   ├── GrpcServerHost.cs       # Unchanged
│   ├── IoTGameServiceImpl.cs   # Updated — SendEnvironmentEvent + SendAdminCommand
│   └── ConnectionApprovalHandler.cs # Updated — validates 4 player roles
└── Shared/
    └── NetworkSingleton.cs     # Unchanged
```
 
---

## Phase 3 Prefabs

| Prefab | NetworkObject | Key components |
|---|---|---|
| `PlayerShip` | ✅ | `Rigidbody2D`, `PlayerShip`, `SpriteRenderer` |
| `BossEnemyBird` | ✅ | `Rigidbody`, `BossEnemyBird`, `MeshRenderer` |
| `EnemyBird` | ✅ | `Rigidbody`, `EnemyBird`, `MeshRenderer` |
| `Projectile` | ✅ | `Rigidbody2D`, `Projectile` |
| `PipeBarrier` | ✅ | `PipeBarrier`, two `BoxCollider2D` (upper + lower) |
 
---

## Phase 3 ConnectionApproval Update

`ConnectionApprovalHandler` now validates four specific player roles:

```csharp
static readonly HashSet<string> ValidPlayerIds = new()
    { "player-1", "player-2", "player-3", "player-4", "stage" };
```

On approval: `BirdSpawnManager` spawns the correct NetworkObject per role:
- `player-1`, `player-2` → `PlayerShip` prefab
- `player-3`, `player-4` → `BossEnemyBird` prefab
- `stage` → no NetworkObject spawned (stage player has no avatar)

# Phase 3.1 — Protocol Fix: Auxiliary Field for Multi-Output Sensors

## What Changed in Phase 3.1

Phase 3 had no mechanism for DHT11 to deliver both temperature and humidity —
humidity was silently discarded. Phase 3.1 adds an optional `auxiliary` map to
all three gRPC message types. Unity receives humidity as a separate
`EnvironmentEventRequest` (resolved server-side from the auxiliary field) — no
Unity-side changes are needed to handle the routing. The only Unity changes are:

1. Updated proto stubs (regenerate from updated `.proto`)
2. `EnvironmentController` now handles `"humidity"` env key
3. `EnvironmentState` gains a `Humidity` NetworkVariable
---

## Proto Update (Additive — Regenerate Stubs)

`auxiliary` added as optional field to all three request messages. Regenerate:

```bash
protoc --csharp_out=Assets/Scripts/Generated \
       --grpc_out=Assets/Scripts/Generated \
       --plugin=protoc-gen-grpc=grpc_csharp_plugin \
       Proto/game_controller_v2.proto
```

The generated stubs are backward compatible — existing code referencing
`InputEventRequest`, `EnvironmentEventRequest`, and `AdminCommandRequest`
continues to compile without changes. `auxiliary` is simply a new optional
field that existing code ignores.
 
---

## `auxiliary` Convention in Unity

Unity receives `auxiliary` on all three message types but has a strict rule:

> **Never require `auxiliary`.** Game logic must function correctly when
> `auxiliary` is empty. Only `EnvironmentController` actively reads `auxiliary`
> content — `IoTInputManager` and `GameRoundManager` ignore it entirely.

For `InputEventRequest` and `AdminCommandRequest`, `auxiliary` is reserved and
ignored in all current Unity code. Do not add logic that depends on it without
a documented protocol change.
 
---

## `EnvironmentController` Phase 3.1 Update

`OnEnvironmentUpdate()` now handles `"humidity"` env key in addition to
`"temperature"` and `"ultrasonic_zone"`. The ASP.NET server unpacks DHT11
auxiliary values and sends two separate `EnvironmentEventRequest` calls —
one for temperature, one for humidity. Unity receives them as independent
events and does not need to know they originated from the same physical sensor.

```csharp
public void OnEnvironmentUpdate(string envKey, float value)
{
    switch (envKey)
    {
        case "temperature":
            Temperature.Value = value;
            ApplyTemperatureEffects();
            break;
 
        case "humidity":
            Humidity.Value = value;
            // currently informational only — displayed in stage player UI
            // reserved for future game mechanic
            break;
 
        case "ultrasonic_zone":
            int zone = Mathf.RoundToInt(value);
            if (zone == UltrasonicZone.Value) break;
            UltrasonicZone.Value = zone;
            ApplyZoneEffects(zone);
            FireZoneCallout(zone);
            break;
    }
}
```
 
---

## `EnvironmentState` NetworkVariables Update

```csharp
public class EnvironmentController : NetworkSingleton<EnvironmentController>
{
    public NetworkVariable<float> Temperature;     // raw Celsius
    public NetworkVariable<float> Humidity;        // percentage 0–100 — new in 3.1
    public NetworkVariable<int>   UltrasonicZone;  // 1–4
    public NetworkVariable<float> GlobalGameSpeed; // 1.0 default
    public NetworkVariable<int>   ActiveZoneEffect;
    // cooldown timestamps — server-only, not NetworkVariables
}
```

`Humidity` is replicated to all clients so the stage player UI can display it.
No game mechanic currently uses humidity — it is informational only and reserved
for a future phase. This is intentional: the data is available, the effect is
not yet defined.
 
---

## Stage Player UI Update (`RoundUIManager`)

The stage player display panel now shows all three environmental readings:

```
┌─────────────────────────────┐
│  STAGE CONTROL              │
│  Temp:     28.5°C           │
│  Humidity: 65%    ← new     │
│  Zone:     2 — TIME SLOW    │
│  Cooldown: 14s remaining    │
└─────────────────────────────┘
```

Humidity displayed as informational only — no effect label shown since no
mechanic is currently attached. Label reads "Humidity: X%" with no annotation.

---

# Phase 3.3 — Infrastructure Fix: gRPC Connection Stability

## What Changed in Phase 3.3

No Unity changes. Keep-alive and reconnection logic is handled entirely in the
ASP.NET server's `UnityRelayService`. Kestrel on the Unity side responds to
HTTP/2 PING frames automatically — no configuration needed here.

 
---
 
---

# Phase 4 — Library Foundation

## What Changed in Phase 4

Phase 4 restructures Unity into a clear library/game separation, formalises
the developer-facing API surface, documents the spectator-only client model,
and adds shared secret validation on incoming gRPC calls.
 
---

## Architectural Decision: All Unity Clients Are Spectators

Unity game clients have no player identity. They connect to the dedicated server
and receive replicated game state — they do not control any player object.
All player input comes exclusively from physical ESP32 devices via the
ASP.NET → gRPC pipeline.

**Consequences:**
- `ConnectionApprovalHandler` approves all connecting clients unconditionally
- No `playerId` required in the NGO approval payload
- All `PlayerShip`, `BossEnemyBird`, and `EnemyBird` NetworkObjects are
  server-owned — no client ever owns a NetworkObject
- Remove all `IsOwner` checks from player NetworkBehaviours —
  they are always server-authoritative
- Multiple clients can connect and all see an identical game view
  **Optional display preference:**
  A client may optionally send a `preferredPlayerId` string in the approval
  payload for UI highlighting purposes only — not for auth or control:

```csharp
// Client-side — optional
var payload = preferredPlayerId != null
    ? Encoding.UTF8.GetBytes($"spectator:{preferredPlayerId}")
    : Encoding.UTF8.GetBytes("spectator");
 
NetworkManager.Singleton.NetworkConfig.ConnectionData = payload;
NetworkManager.Singleton.StartClient();
```

`RoundUIManager` reads `preferredPlayerId` if present and highlights that
player's health bar. If absent, all health bars are displayed with equal weight.
This is purely a client-side display concern — no server logic changes.
 
---

## Architectural Decision: Source Library

The Unity library code lives in `Assets/IoTGame/` and should not be modified.
Developers write their game code in `Assets/Game/`. The boundary is enforced
by Unity Assembly Definitions (`.asmdef`):

- `IoTGame.Core.asmdef` — library assembly, no game dependencies
- `IoTGame.Game.asmdef` — game assembly, references `IoTGame.Core`
---

## Phase 4 Folder Structure

```
Assets/
├── IoTGame/                               ← LIBRARY — do not modify
│   ├── IoTGame.Core.asmdef
│   ├── Core/
│   │   ├── IoTGameServer.cs               ← static facade — developer entry point
│   │   ├── GrpcServerHost.cs              ← Kestrel host, background thread
│   │   ├── IoTGameServiceImpl.cs          ← gRPC service, ConcurrentQueues
│   │   └── IoTInputDispatcher.cs          ← MonoBehaviour, main thread drain
│   ├── Abstractions/
│   │   ├── IoTGamePlayerController.cs     ← abstract base for player objects
│   │   └── IoTGameEnvironmentController.cs ← abstract base for env controller
│   ├── Models/
│   │   ├── IoTInputEvent.cs
│   │   ├── EnvironmentEvent.cs
│   │   └── AdminCommand.cs
│   └── Generated/                         ← proto stubs — do not modify
│       ├── GameController.cs
│       └── GameControllerGrpc.cs
│
└── Game/                                  ← DEVELOPER CODE — modify freely
    ├── IoTGame.Game.asmdef
    ├── Players/
    │   ├── PlayerShip.cs                  ← extends IoTGamePlayerController
    │   ├── BossEnemyBird.cs               ← extends IoTGamePlayerController
    │   ├── EnemyBird.cs                   ← server-controlled, no base class
    │   └── Projectile.cs
    ├── Managers/
    │   ├── GameRoundManager.cs
    │   ├── BirdSpawnManager.cs
    │   ├── EnemySpawnManager.cs
    │   ├── CalloutManager.cs
    │   └── PipeSpawnManager.cs            ← optional
    ├── Environment/
    │   └── EnvironmentController.cs       ← extends IoTGameEnvironmentController
    └── UI/
        └── RoundUIManager.cs
```
 
---

## Public Library API (`IoTGameServer.cs`)

The only class developers interact with directly. Everything else in
`Assets/IoTGame/` is internal to the library assembly.

```csharp
public static class IoTGameServer
{
    // Initialize — call once in Start() after NetworkManager.Singleton.StartServer()
    // secret must match IoTGame:SharedSecret in ASP.NET appsettings.json
    public static void Initialize(int grpcPort = 9090, string secret = "");
 
    // Shutdown — call in OnDestroy()
    public static void Shutdown();
 
    // Status
    public static bool IsRunning { get; }
 
    // Events — all fired on Unity main thread, safe to call NGO APIs inside handlers
    public static event Action<IoTInputEvent>    OnInputReceived;
    public static event Action<EnvironmentEvent> OnEnvironmentUpdate;
    public static event Action<AdminCommand>     OnAdminCommand;
}
```

### Event Models

```csharp
public record IoTInputEvent(
    string PlayerId,
    string InputName,
    string InputType,
    string EventType,   // "pressed", "released", "change"
    float  Value,
    long   Timestamp,
    long   SequenceNumber,
    IReadOnlyDictionary<string, float> Auxiliary
);
 
public record EnvironmentEvent(
    string EnvKey,      // "gravity", "spawn_rate", "ultrasonic_zone"
    float  RawValue,
    long   Timestamp,
    IReadOnlyDictionary<string, float> Auxiliary
);
 
public record AdminCommand(
    string Command,     // "session_start", "end_round", "reset" etc.
    string IssuedBy,
    long   Timestamp,
    IReadOnlyDictionary<string, float> Auxiliary  // session config parameters
);
```
 
---

## Abstract Base Classes

Developers extend these for player and environment objects.
The library handles event routing; the developer handles game logic.

### `IoTGamePlayerController`

```csharp
public abstract class IoTGamePlayerController : NetworkBehaviour
{
    // Set by BirdSpawnManager on spawn — matches playerId from gRPC events
    public string PlayerId { get; internal set; }
 
    // Override to handle specific inputs for this player
    // Called on main thread — safe to access NGO APIs
    public abstract void OnInputReceived(IoTInputEvent evt);
 
    // Override to handle player-specific admin commands if needed
    public virtual void OnAdminCommand(AdminCommand cmd) { }
}
```

Example developer implementation:
```csharp
public class PlayerShip : IoTGamePlayerController
{
    public override void OnInputReceived(IoTInputEvent evt)
    {
        switch (evt.InputName)
        {
            case "move-x": SetJoystickX(evt.Value); break;
            case "move-y": SetJoystickY(evt.Value); break;
            case "fire":   if (evt.EventType == "pressed") Fire(); break;
        }
    }
}
```

### `IoTGameEnvironmentController`

```csharp
public abstract class IoTGameEnvironmentController : NetworkBehaviour
{
    // Override to handle environment updates
    // Called on main thread during Active phase only
    public abstract void OnEnvironmentUpdate(EnvironmentEvent evt);
}
```

Example developer implementation:
```csharp
public class EnvironmentController : IoTGameEnvironmentController
{
    public override void OnEnvironmentUpdate(EnvironmentEvent evt)
    {
        switch (evt.EnvKey)
        {
            case "gravity":    ApplyGravityEffect(evt.RawValue); break;
            case "spawn_rate": ApplySpawnRateEffect(evt.RawValue); break;
            case "ultrasonic_zone": ApplyZoneEffect((int)evt.RawValue); break;
        }
    }
}
```
 
---

## Authentication: Shared Secret Validation

Every incoming gRPC RPC validates the shared secret from metadata.
This closes the gap where anyone reaching port 9090 could send arbitrary events.

```csharp
// In IoTGameServiceImpl — called on every RPC
private void ValidateSecret(ServerCallContext context)
{
    var secret = context.RequestHeaders.GetValue("x-iotgame-secret");
    if (secret != IoTGameServer.SharedSecret)
        throw new RpcException(
            new Status(StatusCode.Unauthenticated, "Invalid shared secret"));
}
 
public override Task<InputEventResponse> SendInputBatch(
    InputBatchRequest request, ServerCallContext context)
{
    ValidateSecret(context);
    // ... enqueue to InputQueue
}
```

The shared secret is configured in `IoTGameServer.Initialize()`:
```csharp
// In developer's ServerBootstrap.cs
IoTGameServer.Initialize(grpcPort: 9090, secret: "your-shared-secret");
```

Must match `IoTGame:SharedSecret` in the ASP.NET `appsettings.json`.
 
---

## `ConnectionApprovalHandler` Phase 4 Update

All clients are spectators — approve unconditionally. Optionally parse
`preferredPlayerId` for UI display purposes only:

```csharp
NetworkManager.Singleton.ConnectionApprovalCallback = (request, response) =>
{
    // All clients are spectators — no identity validation needed
    response.Approved          = true;
    response.CreatePlayerObject = false;
 
    // Parse optional display preference — not used for auth or control
    string payload = Encoding.UTF8.GetString(request.Payload);
    if (payload.StartsWith("spectator:"))
    {
        string preferredPlayerId = payload.Replace("spectator:", "");
        // Store per-client for RoundUIManager highlight logic
        _clientPreferences[request.ClientNetworkId] = preferredPlayerId;
    }
};
```
 
---

## `GameRoundManager` Phase 4 Update

Handles session config delivered via `SendAdminCommand("session_start")`:

```csharp
void HandleAdminCommand(AdminCommand cmd)
{
    switch (cmd.Command)
    {
        case "session_start":
            // Read session config from auxiliary
            _roundDuration = cmd.Auxiliary.GetValueOrDefault("round_duration", 120f);
            _maxMinions    = (int)cmd.Auxiliary.GetValueOrDefault("max_minions", 20f);
            _playerCount   = (int)cmd.Auxiliary.GetValueOrDefault("player_count", 4f);
            break;
 
        case "start_round":
            if (CurrentPhase.Value == RoundPhase.Waiting)
                TransitionTo(RoundPhase.Countdown);
            break;
 
        case "end_round":
            if (CurrentPhase.Value == RoundPhase.Active)
                TransitionTo(RoundPhase.Ended);
            break;
 
        case "reset":
            TransitionTo(RoundPhase.Waiting);
            ResetAllReadyFlags();
            break;
    }
}
```
 
---

## Threading Model — Unchanged, Documented Explicitly

Three separate `ConcurrentQueue`s, all drained on the main thread by
`IoTInputDispatcher.Update()`:

```
[Kestrel background thread]
  IoTGameServiceImpl.SendInputBatch      → enqueue to InputQueue
  IoTGameServiceImpl.SendEnvironmentEvent → enqueue to EnvironmentQueue
  IoTGameServiceImpl.SendAdminCommand    → enqueue to AdminCommandQueue
 
[Unity main thread — every Update()]
  IoTInputDispatcher.Update()
    → drain InputQueue      → IoTGameServer.OnInputReceived
    → drain EnvironmentQueue → IoTGameServer.OnEnvironmentUpdate
    → drain AdminCommandQueue → IoTGameServer.OnAdminCommand
```

**Never call any Unity API from inside `IoTGameServiceImpl`.**
This rule applies to all library code and all developer code in `Assets/Game/`.
 