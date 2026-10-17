# ATITProject — IoT Multiplayer Flappy Bird (Unity)

A proof-of-concept multiplayer game where physical ESP32 button devices act as
controllers. Press a real button → your in-game bird flaps. Built with Unity 6 LTS
and Netcode for GameObjects.

This is the **Unity game client + dedicated server** component of a three-part system.

---

## System Architecture

```
[ESP32 #1..N]
  Physical button press
        │  MQTT over TLS → HiveMQ Cloud
        ▼
[ASP.NET Core Server]
  Maps deviceId → playerId, forwards over gRPC
        │  gRPC (HTTP/2 + Protobuf)
        ▼
[Unity Dedicated Server]  ← this repo
  Receives input, runs physics, replicates state
        │  Netcode for GameObjects (NGO)
        ▼
[Unity Game Client]  ← this repo
  Renders all birds in real time
```

---

## Gameplay

- Each player has a colored block (bird) that falls due to gravity
- Pressing the physical button sends a `jump` input through the cloud → server → Unity
- The server applies an upward velocity impulse to that player's bird
- All connected clients see all birds moving in real time
- Birds that fall below the death threshold are despawned and respawn on next input
- Players do **not** need a connected Unity client — birds spawn purely from IoT input

---

## Tech Stack

| Layer | Technology |
|---|---|
| Unity version | 6000.3.11f1 (Unity 6 LTS) |
| Multiplayer | Netcode for GameObjects 2.9.1 |
| Rendering | URP 17.3.0 |
| IoT input protocol | gRPC via `Grpc.Core` (netstandard2.0) |
| Proto contract | `Proto/game_controller_v2.proto` |

---

## Project Structure

```
Assets/Scripts/
  Players/
    PlayerBird.cs          — NetworkBehaviour; handles flap, analog, death, color sync
    PlayerInputState.cs    — per-player input state struct (keyed by inputName)
  Managers/
    BirdSpawnManager.cs    — spawns/despawns server-owned birds on first IoT input
    IoTInputManager.cs     — drains gRPC input queue; dispatches to PlayerBird
    PlayerInputStateRegistry.cs — tracks input state + sequence numbers per player
    PlayerDistinguisherManager.cs — assigns unique colors to players
  Server/
    ServerBootstrap.cs     — starts NGO server + gRPC host; parses CLI args
    GrpcServerHost.cs      — embedded Grpc.Core server on background thread
    IoTGameServiceImpl.cs  — gRPC service impl; enqueues to ConcurrentQueue
    ConnectionApprovalHandler.cs — approves spectator Unity clients
  UI/
    DebugConsoleUI.cs      — in-game overlay showing live input feed (via ClientRpc)
  Shared/
    NetworkSingleton.cs    — base class for all networked manager singletons
  Generated/               — auto-generated gRPC stubs (do not edit)
Proto/
  game_controller_v2.proto — shared contract with ASP.NET server
Assets/Plugins/grpc/       — Grpc.Core DLLs (committed)
Assets/Editor/
  BuildServer.cs           — CLI build script for dedicated server
docs/logs/                   — session logs with decisions and resolved issues
```

---

## Running the Dedicated Server

```powershell
# Run in current console (all logs visible)
Start-Process ATITProject.exe `
  -ArgumentList "-batchmode -nographics -grpcPort 9090 -gamePort 7777" `
  -NoNewWindow -Wait
```

Default ports: gRPC `9090`, NGO game clients `7777`.

---

## Building

### Dedicated server (no platform switch needed)

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "C:\path\to\ATITProject" `
  -executeMethod BuildServer.Build `
  -logFile build.log
```

Output: `Builds/Server/ATITProject.exe`

### Game client

Open in Unity Editor → File → Build Settings → Standalone → Build.
Set `player-1` / `player-2` etc. on `ServerBootstrap` before building each client.

---

## First-Time Setup

See **[docs/setup/](docs/setup/)** (start with [unity-setup-phase-1-2.md](docs/setup/unity-setup-phase-1-2.md)) and the cross-repo [setup guide](https://github.com/SupakornSJB/ATITFinalProject/blob/main/docs/setup.md) for:
- gRPC DLL installation (already committed — skip if cloned from repo)
- Proto stub regeneration
- Scene and prefab configuration
- NetworkManager setup

---

## Session Logs

Development history, decisions, and resolved issues:

- [`docs/logs/2026-04-24.md`](docs/logs/2026-04-24.md) — Initial implementation; gRPC DLL setup journey
- [`docs/logs/2026-04-24-2.md`](docs/logs/2026-04-24-2.md) — Phase 1 E2E confirmed; Phase 2 multi-input implemented
- [`docs/logs/2026-04-25.md`](docs/logs/2026-04-25.md) — Death detection; debug console UI; server-owned bird architecture