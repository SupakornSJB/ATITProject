# Unity Editor Setup — Phase 3.2 (Gravity + Spawn Rate)

> Assumes Phase 3 and 3.1 setup are already complete. Only the delta is listed here.

---

## What changed in code (no editor action needed)

| Change | Detail |
|---|---|
| Env key rename | `"temperature"` → `"gravity"`, `"humidity"` → `"spawn_rate"`. ASP.NET server sends semantic keys — Unity never sees raw sensor names. |
| `GlobalGravity` NetworkVariable | Added to `EnvironmentController`. All game Rigidbodies now read this instead of `Physics.gravity`. |
| `EnemySpawnManager` refactor | Frenzy timer removed. Zone D frenzy (halves interval) is now checked live via `UltrasonicZone.Value == 4` each `Update`. |
| Spawn rate threshold callouts | "MINION SURGE" fires once when humidity crosses 70%; "SWARM THINNING" fires once when it drops below 50%. |

---

## What needs editor setup

### 1. Disable `Use Gravity` on all game Rigidbody prefabs

`GlobalGravity` replaces Unity's built-in gravity. The built-in gravity is not
networked and would desync clients. Each affected prefab now sets `useGravity = false`
in `Awake()` — but **verify in the Inspector** that the prefab itself doesn't have
`Use Gravity` ticked, as a prefab override could conflict.

Open each prefab and confirm the `Rigidbody` component shows:

| Prefab | Use Gravity setting |
|---|---|
| `BossEnemyBird` | ☐ off (set in Awake, verify no override) |
| `EnemyBird` | ☐ off |
| `Projectile` | ☐ off |

`PlayerShip` is kinematic — gravity does not apply regardless.

### 2. No other editor changes required

`GlobalGravity` is a `NetworkVariable` — it replicates automatically like all other
network state. No new GameObjects, no new prefab fields, no new Inspector wiring.

---

## Behaviour summary

| Sensor reading | envKey | Effect in Unity |
|---|---|---|
| DHT11 temperature | `"gravity"` | `GlobalGravity` 7.0–12.0 (hot = floaty, cold = heavy) |
| DHT11 humidity | `"spawn_rate"` | Minion spawn interval 1.5–8s (humid = fast) |
| HC-SR04 distance | `"ultrasonic_zone"` | Zone 1–4 effects (unchanged) |

Zone D frenzy stacks with spawn rate: `interval × 0.5`, floored to `1.0s`.