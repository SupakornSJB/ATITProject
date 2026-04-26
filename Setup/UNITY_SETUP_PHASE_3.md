# Unity Editor Setup — Phase 3 (Space Shooter)

> Phase 1/2 setup (gRPC DLLs, stub generation, NetworkManager, gRPC DLL Inspector settings)
> is unchanged. See [`Setup/UNITY_SETUP_PHASE_1_2.md`](Setup/UNITY_SETUP_PHASE_1_2.md) for
> that one-time setup. This document covers only the new Phase 3 additions.

---

## 1. New Prefabs

Create each prefab in `Assets/Prefabs/` and save it. Register all of them in
**NetworkManager → Network Prefabs** (step 5).

---

### `PlayerShip` prefab

1. Create a Cube (or Quad mesh), rename to `PlayerShip`
2. Add components:
   - `NetworkObject`
   - `Rigidbody` — disable **Use Gravity**; freeze **Z position** and **X, Y, Z rotation** constraints
   - `BoxCollider`
   - `MeshRenderer` — assign a URP `UniversalRenderPipeline/Lit` material; color overridden at runtime
   - `PlayerShip` script
3. In `PlayerShip` Inspector:
   - **Move Speed** — `5`
   - **Projectile Prefab** — assign the `Projectile` prefab (create it first)
   - **Max Live Projectiles** — `3`
4. Save as prefab

Ships move in the X/Y plane only. The Rigidbody Z-freeze prevents drift into or out of the screen.

---

### `BossEnemyBird` prefab

1. Create a Cube, rename to `BossEnemyBird`
2. Add components:
   - `NetworkObject`
   - `Rigidbody` — enable **Use Gravity**; freeze **X and Z rotation** constraints
   - `BoxCollider`
   - `MeshRenderer` — URP Lit material; color overridden at runtime
   - `BossEnemyBird` script
3. In `BossEnemyBird` Inspector:
   - **Flap Force** — `5`
   - **Bottom Y** — `-10` (triggers "BIRDS WIN" if boss falls below this)
4. Save as prefab

---

### `EnemyBird` prefab (minion)

1. Create a smaller Cube, rename to `EnemyBird`
2. Add components:
   - `NetworkObject`
   - `Rigidbody` — enable **Use Gravity**; freeze **X and Z rotation** constraints
   - `BoxCollider` — used for `OnCollisionEnter` damage detection against ships
   - `MeshRenderer` — URP Lit material, red recommended
   - `EnemyBird` script
3. In `EnemyBird` Inspector:
   - **Flap Force** — `4`
   - **Flap Interval Min** — `1`
   - **Flap Interval Max** — `3`
4. Save as prefab

---

### `Projectile` prefab

1. Create a Capsule (or narrow Cube), rename to `Projectile`
2. Add components:
   - `NetworkObject`
   - `Rigidbody` — disable **Use Gravity**; freeze **X, Z position** and all **rotation** constraints
   - `BoxCollider` (or `CapsuleCollider`) — tick **Is Trigger** ✅
   - `MeshRenderer` — bright yellow or white URP material
   - `Projectile` script
3. In `Projectile` Inspector:
   - **Speed** — `10`
   - **Top Y** — `12` (despawns when it exits the top of the play area)
4. Save as prefab

The collider must be a **trigger** so it passes through birds and fires `OnTriggerEnter` for hit detection.

---

### `PipeBarrier` prefab (optional — only spawns when stage device connects)

1. Create an empty GameObject, rename to `PipeBarrier`
2. Add components:
   - `NetworkObject`
   - `Rigidbody` — disable **Use Gravity**; freeze all **rotation** constraints; freeze **X and Z position**
   - `PipeBarrier` script
3. Create two child GameObjects inside `PipeBarrier`:
   - `UpperSection` — a wide, flat Cube with a `BoxCollider`; represents the solid top half
   - `LowerSection` — same setup; represents the solid bottom half
   - Wire both into **Upper Section** and **Lower Section** fields on `PipeBarrier`
4. In `PipeBarrier` Inspector:
   - **Descent Speed** — `2`
   - **Despawn Y** — `-10`
5. Save as prefab

When `GapIsUpper` is false (default): upper section is hidden, lower section is solid.
When `GapIsUpper` is true: upper section is solid, lower section is hidden.

---

## 2. Manager GameObjects in SampleScene

Every manager that extends `NetworkSingleton<T>` needs a `NetworkObject` component.
The table below lists **all** managers — existing ones already in the scene and new ones
to add for Phase 3.

| GameObject name | Status | Scripts | Inspector wiring changes |
|---|---|---|---|
| `NetworkManager` | existing | `NetworkManager`, `UnityTransport` | No change |
| `PlayerDistinguisherManager` | existing | `NetworkObject`, `PlayerDistinguisherManager` | No change |
| `BirdSpawnManager` | existing — **update** | `NetworkObject`, `BirdSpawnManager` | Remove **Bird Prefab**; set **Ship Prefab** → `PlayerShip`, **Boss Bird Prefab** → `BossEnemyBird` |
| `IoTInputManager` | existing | `NetworkObject`, `IoTInputManager` | No change |
| `PlayerInputStateRegistry` | existing (Phase 2) | `NetworkObject`, `PlayerInputStateRegistry` | No change |
| `GameRoundManager` | **new** | `NetworkObject`, `GameRoundManager` | — |
| `EnvironmentController` | **new** | `NetworkObject`, `EnvironmentController` | — |
| `EnemySpawnManager` | **new** | `NetworkObject`, `EnemySpawnManager` | **Enemy Bird Prefab** → `EnemyBird`; set Spawn X Min/Max and Spawn Y |
| `CalloutManager` | **new** | `NetworkObject`, `CalloutManager` | — |
| `PipeSpawnManager` | **new** | `NetworkObject`, `PipeSpawnManager` | **Pipe Barrier Prefab** → `PipeBarrier` |

> If `PlayerInputStateRegistry` is not in your scene yet, add it now — it was introduced
> in Phase 2 and is still used in Phase 3 for sequence number tracking.

---

## 3. UI Canvas — `RoundUIManager`

`RoundUIManager` is a plain `MonoBehaviour` (not a NetworkSingleton). Attach it to a
Canvas GameObject. All UI fields are optional — the game runs without them.

### Canvas setup

1. Create a **Canvas** (Screen Space — Overlay) in the scene
2. Attach `RoundUIManager` script to the Canvas root

### Phase panels

Create four child panels and wire them to `RoundUIManager`:

| Field | Panel name | Contents |
|---|---|---|
| **Waiting Panel** | `WaitingPanel` | 4 child GameObjects each with one `Image` (see below) |
| **Countdown Panel** | `CountdownPanel` | One child `TextMeshProUGUI` in the center |
| **Active Hud Panel** | `ActiveHudPanel` | HP bars, round timer, zone indicator (see below) |
| **Ended Panel** | `EndedPanel` | One child `TextMeshProUGUI` for the result text |

Set only **Waiting Panel** active by default; all others inactive.

### WaitingPanel child layout

Create four child GameObjects inside `WaitingPanel`, one per slot. Each child has a
single `Image` component that `RoundUIManager` tints green (ready) or red (not ready):

| Child name | `RoundUIManager` field |
|---|---|
| `Ship1Ready` | **Ship 1 Ready Indicator** |
| `Ship2Ready` | **Ship 2 Ready Indicator** |
| `Bird1Ready` | **Bird 1 Ready Indicator** |
| `Bird2Ready` | **Bird 2 Ready Indicator** |

Position them however suits the layout (e.g. a horizontal group at the top of the panel).
Start color doesn't matter — it's overridden every frame.

### Active HUD contents

Inside `ActiveHudPanel`, create and wire:

| Field | Component | Notes |
|---|---|---|
| **Boss 1 Hp Bar** | `Slider` | Max value `1`, display only |
| **Boss 2 Hp Bar** | `Slider` | Max value `1` |
| **Ship 1 Hp Bar** | `Slider` | Max value `1` |
| **Ship 2 Hp Bar** | `Slider` | Max value `1` |
| **Round Timer Text** | `TextMeshProUGUI` | Displays seconds remaining |
| **Zone Indicator Text** | `TextMeshProUGUI` | Current zone label |

### Callout overlay

1. Create a child panel `CalloutPanel` (large, centered, semi-transparent background)
2. Add a `TextMeshProUGUI` child inside it
3. Set `CalloutPanel` **inactive** by default
4. Wire **Callout Panel** and **Callout Text** fields on `RoundUIManager`

### Tutorial overlay (F4)

1. Create a child panel `TutorialOverlay` covering the full screen
2. Add control descriptions for all roles
3. Set **inactive** by default
4. Wire **Tutorial Overlay** field on `RoundUIManager`

---

## 4. NetworkManager — Register New Prefabs

Select `NetworkManager` → **Network Prefabs** list and add:

- `PlayerShip`
- `BossEnemyBird`
- `EnemyBird`
- `Projectile`
- `PipeBarrier`

`PlayerBird` can stay registered but is not spawned in Phase 3.

---

## 5. Physics Layer Setup (recommended)

### Step 1 — Create the layers

1. **Edit → Project Settings → Tags and Layers**
2. Expand the **Layers** section (slots 6 and above are user-defined)
3. Add these four names, one per empty slot:

| Slot | Name |
|---|---|
| User Layer 6 | `Ship` |
| User Layer 7 | `BossBird` |
| User Layer 8 | `Minion` |
| User Layer 9 | `Projectile` |

The slot numbers don't matter as long as the names match.

### Step 2 — Assign layers to prefabs

Open each prefab in `Assets/Prefabs/` and set the **Layer** dropdown (top of the
Inspector, next to the GameObject name):

| Prefab | Layer to set |
|---|---|
| `PlayerShip` | `Ship` |
| `BossEnemyBird` | `BossBird` |
| `EnemyBird` | `Minion` |
| `Projectile` | `Projectile` |

Unity will ask "Do you want to set the layer for all child objects as well?" — click
**Yes, change children** so colliders on child GameObjects inherit the same layer.

### Step 3 — Configure the collision matrix

1. **Edit → Project Settings → Physics**
2. Scroll to the bottom: **Layer Collision Matrix**
3. Find the rows/columns for the four layers above and **uncheck** these intersections:

| | Ship | BossBird | Minion | Projectile |
|---|:---:|:---:|:---:|:---:|
| **Ship** | ☐ off | — | ✅ on | ☐ off |
| **BossBird** | — | — | ☐ off | ✅ on |
| **Minion** | ✅ on | ☐ off | — | ☐ off |
| **Projectile** | ☐ off | ✅ on | ☐ off | — |

Meaning:
- `Ship` × `Ship` off — ships pass through each other
- `Ship` × `Projectile` off — friendly fire prevention
- `BossBird` × `Minion` off — minions don't push boss birds
- `Minion` × `Projectile` off — projectiles ignore minions (not implemented)
- `Minion` × `Ship` on — minion collision deals damage via `OnCollisionEnter`
- `BossBird` × `Projectile` on — projectile trigger hits boss via `OnTriggerEnter`

---

## 6. Scene Camera

Keep the existing camera setup. All objects are at `Z = 0`. Ships spawn at Y `−5`,
boss birds at Y `+5`. Adjust orthographic size or FOV so both zones are visible.

---

## 7. ServerBootstrap — No Changes

`ServerBootstrap` is unchanged. The three build modes (dedicated server, host, client)
work the same as Phase 1/2.

For local testing:
- One dedicated server build (or host in the Editor)
- Up to four client builds: `player-1`, `player-2`, `player-3`, `player-4`
- `stage` client is optional; pipes only spawn if a `stage` client connects

---

## 8. Obsolete Scripts (do not attach)

| Script | Reason |
|---|---|
| `BirdController.cs` | Phase 1 artifact |
| `BirdDistinguisher.cs` | Replaced by `BossEnemyBird` |
| `PlayerDistinguisher.cs` | Superseded |
| `PlayerMovement.cs` | CharacterController movement, not used |
| `PipeController.cs` | Old prototype, replaced by `PipeBarrier` |
| `PipeSpawner.cs` | Old prototype, replaced by `PipeSpawnManager` |
| `PlayerBird.cs` | Phase 1/2 flappy bird — not spawned in Phase 3 |