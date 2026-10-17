# ATITProject — IoT Co-op Shooter (Unity)

Unity 6 dedicated server and spectator client for
[ATITFinalProject](https://github.com/SupakornSJB/ATITFinalProject). Two players fly ships
with **physical ESP32 controllers** — a thumbstick on one board, a knob and buttons on the
other — while the room itself shapes the game: humidity drives how fast enemies spawn and a
distance sensor sets the game speed.

The dedicated server receives controller input over **gRPC with mutual TLS** from the
[relay server](https://github.com/SupakornSJB/ATITInfraNode) and replicates the
authoritative game state to every connected client with Netcode for GameObjects.

```
ESP32s ─MQTT/mTLS─▶ Mosquitto ─▶ ASP.NET relay ─gRPC/mTLS :9090─▶ Unity dedicated server ─NGO :7777─▶ clients
                                                                   (this repo)                       (this repo)
```

---

## Gameplay

- **Co-op, two ships** — each ship has HP and a shield; players move and fire with their
  controllers. Ships are spawned by the server when their player's input arrives — clients
  never own a ship.
- **AI enemies** — enemy birds spawn from the right, fly left and shoot at the ships; pipe
  barriers scroll through the level.
- **Rounds** — started and ended by the operator (round buttons on ESP32 #1, or the relay's
  admin API). Players win when the session timer runs out with a ship alive and lose when
  both ships reach 0 HP.
- **Environment** (via `EnvironmentController`):

  | Signal | Source | Effect |
  |---|---|---|
  | `spawn_rate` | DHT11 humidity | 40 % → enemy every 3 s … 80 % → every 0.6 s; "SPAWN SURGE" callout at 65 % |
  | `ultrasonic_zone` | HC-SR04 distance, zones 1–4 | Game speed 0.5× / 0.75× / 1× / 1.5× with a callout |
  | `gravity` | DHT11 temperature | 15 °C → gravity 40 … 40 °C → 20 (supported; not mapped in the current relay registry) |

- **Spectator clients** — every Unity client is a spectator; a client can optionally
  highlight one player's UI.

---

## How input reaches the game

```
gRPC (background thread)                         Unity main thread
IoTGameServiceImpl ─▶ ConcurrentQueue ─▶ IoTInputManager ─▶ InputEventDispatcher ─▶ GameInputRouter ─▶ PlayerShip
                                                          ├▶ EnvironmentEventDispatcher ─▶ EnvironmentController
                                                          └▶ AdminCommandDispatcher ─▶ GameSessionManager
```

gRPC calls arrive on a background thread, so they are only queued there and drained on the
main thread each frame. `GameInputRouter` maps semantic inputs from the relay (`move-x`,
`move-y`, `move-y-up`, `move-y-down`, `fire`, `admin-start`, `admin-end`) to game actions.

---

## Project structure

```
Assets/
├── IoTGame/                 # Reusable IoT input layer (game-agnostic)
│   ├── Core/                # GrpcServerHost (mTLS), IoTGameServer facade, IoTGameServiceImpl,
│   │                        # IoTInputManager + input / environment / admin dispatchers
│   ├── Models/              # IoTInputEvent, EnvironmentEvent, AdminCommand
│   └── Generated/           # gRPC stubs from Proto/ (do not edit)
├── Scripts/
│   ├── Managers/            # GameSessionManager, GameInputRouter, EnvironmentController,
│   │                        # EnemySpawnManager, PipeSpawnManager, BirdSpawnManager, CalloutManager, …
│   ├── Players/             # PlayerShip (+UI), EnemyBird, PipeBarrier, projectiles
│   ├── Server/              # ServerBootstrap (CLI args, server/client start), ConnectionApprovalHandler
│   └── UI/                  # RoundUIManager, DebugConsoleUI
├── Editor/BuildServer.cs    # CLI dedicated-server build (copies certs next to the binary)
├── Certs/                   # mTLS certificates — git-ignored, see setup guide
└── Plugins/grpc/            # Grpc.Core native + managed DLLs
Proto/game_controller_v2.proto   # gRPC contract shared with the relay
```

| | |
|---|---|
| Unity | 6000.3.11f1 (Unity 6 LTS), URP 17.3 |
| Multiplayer | Netcode for GameObjects 2.9.1, Unity Transport |
| IoT transport | `Grpc.Core` server with `SslServerCredentials` — client certificate required and verified against the project CA |
| RPCs | `SendInputEvent`, `SendInputBatch`, `SendEnvironmentEvent`, `SendAdminCommand` |

---

## Building and running

Generate the certificates and copy `ca.crt`, `device.crt` and `device.key` (CN `unity-server`)
into `Assets/Certs/` first — see the
[setup guide](https://github.com/SupakornSJB/ATITFinalProject/blob/main/docs/setup.md).

**Dedicated server** — CLI build (also available from File → Build Settings → Dedicated Server):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "C:\path\to\ATITProject" `
  -executeMethod BuildServer.Build -logFile build.log
# → Builds/Server/ATITProject.exe, with Assets/Certs/ copied next to it

Builds/Server/ATITProject.exe -batchmode -nographics -grpcPort 9090 -gamePort 7777 `
  -grpcCert Assets/Certs/device.crt -grpcKey Assets/Certs/device.key -caCert Assets/Certs/ca.crt
```

The cert arguments default to those paths. If the files are missing the server logs a warning
and starts **without TLS** — check the log for `Starting with mTLS`.

**Spectator client** — Play Mode in the Editor, or a standalone build started with
`-serverAddress <server-host> -gamePort 7777`.

The Unity Editor setup checklists from each phase are in [`docs/setup/`](docs/setup/).

---

## Development history

Phase plans are in [`docs/plans/`](docs/plans/) and session logs — decisions, problems and
fixes — in [`docs/logs/`](docs/logs/).
