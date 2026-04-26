# Unity Editor Setup — Phase 3.1 (Humidity + Stage Panel)

> Assumes Phase 3 setup is already complete. Only the delta is listed here.

---

## What changed in code (no editor action needed)

| Change | Detail |
|---|---|
| Env key bug fix | `EnvironmentController` now listens for `"ultrasonic_zone"` instead of `"zone"`. The ASP.NET server was already sending `"ultrasonic_zone"` — Unity was silently ignoring it. No Unity setup required. |
| `Humidity` NetworkVariable | Added to `EnvironmentController`, auto-replicated. No prefab or scene changes needed. |
| Zone effects event-driven | Internal refactor only. No setup change. |

---

## What needs editor setup

### Stage Info Panel (4 new TMP fields on `RoundUIManager`)

The stage player display now shows temperature, humidity, zone, and cooldown.
All four fields are optional — the game runs without them.

**Step 1 — Create the panel**

Inside the Canvas that has `RoundUIManager`, create a child panel:

1. Right-click Canvas → **UI → Panel**, rename to `StageInfoPanel`
2. Position it where it won't overlap the main HUD (e.g. bottom-left corner)
3. Leave it **active** — unlike phase panels, this one is always visible

**Step 2 — Create four TMP child text objects**

Inside `StageInfoPanel`, create four child `TextMeshProUGUI` objects:

| GameObject name | Displays | Example text |
|---|---|---|
| `TemperatureText` | Raw Celsius from DHT11 | `Temp: 28.5°C` |
| `HumidityText` | Humidity percentage from DHT11 | `Humidity: 65%` |
| `ZoneDetailText` | Current zone and its effect label | `Zone: Zone B — TIME SLOW` |
| `CooldownText` | Seconds until effect can trigger again | `Cooldown: 14s` or `Ready` |

Use a `Vertical Layout Group` on `StageInfoPanel` to stack them automatically.

**Step 3 — Wire to `RoundUIManager`**

Select the Canvas root (where `RoundUIManager` is attached). In the Inspector under
**Stage Info Panel**, assign:

| Field | Assign |
|---|---|
| **Temperature Text** | `TemperatureText` |
| **Humidity Text** | `HumidityText` |
| **Zone Detail Text** | `ZoneDetailText` |
| **Cooldown Text** | `CooldownText` |

Leave any field empty to skip that display — `RoundUIManager` null-checks all of them.

---

# Phase 3.2 — Design Fix: Swap Temperature and Humidity Effects

## What Changed in Phase 3.2

Temperature and humidity effects are swapped based on real hardware behaviour.
DHT11 temperature is slow-changing and suits an ambient world parameter.
DHT11 humidity is fast-changing and suits a dynamic real-time mechanic.

**Corrected effect assignment:**

| envKey received | Source sensor | Effect | Reason |
|---|---|---|---|
| `"spawn_rate"` | DHT11 humidity | Minion spawn rate | Fast response — visible within seconds |
| `"gravity"` | DHT11 temperature | Global gravity strength | Slow drift — ambient world parameter |

The server now sends `envKey: "spawn_rate"` and `envKey: "gravity"` instead
of `"humidity"` and `"temperature"`. Unity receives self-describing semantic
keys and never needs to know which physical sensor produced them.
 
---

## `EnvironmentController` Phase 3.2 Update

Replace `"temperature"` and `"humidity"` cases with `"gravity"` and
`"spawn_rate"` in `OnEnvironmentUpdate()`:

```csharp
public void OnEnvironmentUpdate(string envKey, float value)
{
    switch (envKey)
    {
        case "gravity":
            Temperature.Value = value; // retain raw Celsius for UI display
            ApplyGravityEffect(value);
            break;
 
        case "spawn_rate":
            Humidity.Value = value; // retain raw % for UI display
            ApplySpawnRateEffect(value);
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

## Gravity Effect (`ApplyGravityEffect`)

Temperature changes slowly so gravity shifts gradually over the session.
Applied via a `GlobalGravity` NetworkVariable that all Rigidbody objects
read each `FixedUpdate` — never use `Physics.gravity` directly as that is
not networked and would desync clients.

```csharp
NetworkVariable<float> GlobalGravity; // default: 9.81
 
void ApplyGravityEffect(float celsius)
{
    // Higher temp = lower gravity = birds float higher, harder to hit
    // Range: ~15°C (cold room) to ~40°C (very warm) → gravity 12.0 to 7.0
    float gravity = Mathf.Lerp(12.0f, 7.0f, Mathf.InverseLerp(15f, 40f, celsius));
    GlobalGravity.Value = gravity;
}
```

All `Rigidbody` and `Rigidbody2D` components read `GlobalGravity` in
`FixedUpdate` and apply it manually:

```csharp
void FixedUpdate()
{
    float g = EnvironmentController.Instance.GlobalGravity.Value;
    rb.AddForce(Vector3.down * g * rb.mass, ForceMode.Acceleration);
    // disable Unity's built-in gravity: rb.useGravity = false in Awake()
}
```

**Important:** Set `useGravity = false` on all game Rigidbodies in `Awake()`.
Unity's built-in gravity is not networked — `GlobalGravity` replaces it entirely.

### Gravity effect on each role

| Role | High temp (low gravity) | Low temp (high gravity) |
|---|---|---|
| Boss birds | Float higher, harder to hit, flap sends them further | Sink faster, easier to hit, harder to survive |
| Minion birds | Easier to dodge (float slower) | Harder to dodge (fall faster) |
| Ships | Slight upward drift (cosmetic) | Heavier feel (cosmetic) |
| Projectiles | Travel further upward | Drop off faster |

Gravity affects all sides — neither team is purely advantaged or disadvantaged.
The effect changes the skill expression required rather than directly buffing one side.
 
---

## Spawn Rate Effect (`ApplySpawnRateEffect`)

Humidity changes quickly so spawn rate responds within seconds of interaction.
Replaces the temperature-driven spawn rate from Phase 3.

```csharp
void ApplySpawnRateEffect(float humidity)
{
    // Higher humidity = more aggressive spawning
    // Range: 30% (dry) to 90% (very humid) → interval 8s to 1.5s
    float interval = Mathf.Lerp(8.0f, 1.5f, Mathf.InverseLerp(30f, 90f, humidity));
    EnemySpawnManager.Instance.SetSpawnInterval(interval);
}
```

Spawn interval floor remains 1.0s regardless of humidity + Zone D frenzy stack.
 
---

## Spawn Rate + Zone D Stacking Rule (Updated)

With humidity now driving spawn rate, Zone D frenzy (120cm+) still halves the
current interval. To prevent overwhelming the ship players, stacking is capped:

```csharp
float effectiveInterval = Mathf.Max(
    1.0f,                              // absolute floor
    _baseInterval * (isZoneD ? 0.5f : 1.0f)
);
```

`_baseInterval` is set by `ApplySpawnRateEffect`. Zone D multiplies on top.
Both effects together at maximum values: `1.5s × 0.5 = 0.75s → floored to 1.0s`.
 
---

## Stage Player UI Update (`RoundUIManager`)

Stage player panel updated to show semantic labels rather than raw sensor names:

```
┌─────────────────────────────────┐
│  STAGE CONTROL                  │
│  Gravity:    9.2  (28.5°C)      │
│  Spawn rate: fast (65% humid)   │
│  Zone:       2 — TIME SLOW      │
│  Cooldown:   14s remaining      │
└─────────────────────────────────┘
```

Show both the semantic effect label and the raw sensor reading in parentheses
so the stage player understands both the game effect and the physical cause.
 
---

## Callout Updates

Two new callouts triggered by spawn rate threshold crossings:

| Condition | Callout | Duration |
|---|---|---|
| Humidity crosses 70% (fast spawn) | "MINION SURGE" | 2s |
| Humidity drops below 50% (slow spawn) | "SWARM THINNING" | 2s |

Callouts only fire on threshold crossing, not continuously. Use a flag
`_surgeCalloutFired` to prevent repeated callouts at the same humidity level.
 