using System.Collections.Generic;
using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class EnvironmentController : NetworkSingleton<EnvironmentController>
    {
        // Raw sensor values retained for UI display — game logic uses semantic keys
        public NetworkVariable<float> Temperature = new(
            22f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> Humidity = new(
            50f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> UltrasonicZone = new(
            3,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> ActiveZoneEffect = new(
            3,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> GlobalGameSpeed = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Replaces Physics.gravity — all registered Rigidbodies receive this in FixedUpdate.
        // Default 30 gives snappier feel. DHT11 temperature shifts it 40→20 over 15–40°C.
        public NetworkVariable<float> GlobalGravity = new(
            30f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Server-only: all game Rigidbodies that need GlobalGravity applied
        private readonly List<Rigidbody> _gravityBodies = new();

        // Server-only cooldown timestamps
        private float _lastShieldTrigger = float.MinValue;
        private float _lastTimeSlow = float.MinValue;
        private const float ShieldCooldown = 15f;
        private const float TimeSlowCooldown = 20f;

        private float _zone4DrainAccum;
        private bool _surgeCalloutFired;

        // Called by BossEnemyBird, EnemyBird, Projectile on OnNetworkSpawn (server only)
        public void RegisterGravityBody(Rigidbody rb)
        {
            if (rb != null && !_gravityBodies.Contains(rb))
                _gravityBodies.Add(rb);
        }

        // Called on OnNetworkDespawn (server only)
        public void UnregisterGravityBody(Rigidbody rb)
        {
            _gravityBodies.Remove(rb);
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            float g = GlobalGravity.Value;
            foreach (var rb in _gravityBodies)
                if (rb != null)
                    rb.AddForce(Vector3.down * g, ForceMode.Acceleration);
        }

        private void Update()
        {
            if (!IsServer) return;
            DrainEnvironmentQueue();
            if (GameRoundManager.Instance?.CurrentPhase.Value == RoundPhase.Active &&
                UltrasonicZone.Value == 4)
                ApplyBossDrainZone4();
        }

        private void DrainEnvironmentQueue()
        {
#if UNITY_SERVER
            while (Server.IoTGameServiceImpl.EnvironmentQueue.TryDequeue(out var evt))
                OnEnvironmentUpdate(evt.EnvKey, evt.RawValue);
#endif
        }

        // envKey convention (set by ASP.NET, never assume physical sensor name):
        //   "gravity"         — from DHT11 temperature; slow-changing ambient parameter
        //   "spawn_rate"      — from DHT11 humidity; fast-changing dynamic mechanic
        //   "ultrasonic_zone" — from HC-SR04; zone 1–4
        public void OnEnvironmentUpdate(string envKey, float value)
        {
            switch (envKey)
            {
                case "gravity":
                    Temperature.Value = value; // raw Celsius retained for UI
                    ApplyGravityEffect(value);
                    break;

                case "spawn_rate":
                    Humidity.Value = value; // raw humidity % retained for UI
                    ApplySpawnRateEffect(value);
                    break;

                case "ultrasonic_zone":
                    int zone = Mathf.Clamp(Mathf.RoundToInt(value), 1, 4);
                    if (zone == UltrasonicZone.Value) break;
                    int previousZone = UltrasonicZone.Value;
                    UltrasonicZone.Value = zone;
                    ActiveZoneEffect.Value = zone;
                    OnZoneChanged(previousZone, zone);
                    ApplyZoneEffects(zone);
                    FireZoneCallout(zone);
                    break;
            }
        }

        // Higher temp → lower gravity → birds float higher, harder to hit.
        // Range: 15°C → 40, 40°C → 20
        private void ApplyGravityEffect(float celsius)
        {
            GlobalGravity.Value = Mathf.Lerp(40f, 20f, Mathf.InverseLerp(15f, 40f, celsius));
        }

        // Higher humidity → faster spawning.
        // Range: 30% → 8s interval, 90% → 1.5s interval
        private void ApplySpawnRateEffect(float humidity)
        {
            float interval = Mathf.Lerp(8.0f, 1.5f, Mathf.InverseLerp(30f, 90f, humidity));
            EnemySpawnManager.Instance?.SetSpawnInterval(interval);

            if (humidity >= 70f && !_surgeCalloutFired)
            {
                _surgeCalloutFired = true;
                CalloutManager.Instance?.TryShowCallout("MINION SURGE", 2f);
            }
            else if (humidity < 50f && _surgeCalloutFired)
            {
                _surgeCalloutFired = false;
                CalloutManager.Instance?.TryShowCallout("SWARM THINNING", 2f);
            }
        }

        private void OnZoneChanged(int from, int to)
        {
            if (from == 2 && to != 2)
                GlobalGameSpeed.Value = 1f;
            if (from == 4 && to != 4)
                _zone4DrainAccum = 0f;
        }

        private void ApplyZoneEffects(int zone)
        {
            float now = Time.time;
            switch (zone)
            {
                case 1:
                    if (now - _lastShieldTrigger >= ShieldCooldown)
                    {
                        _lastShieldTrigger = now;
                        ActivateShields();
                    }
                    else
                    {
                        CalloutManager.Instance?.TryShowCallout("SHIELD COOLDOWN", 2f);
                        Debug.Log($"[EnvironmentController] Shield cooldown: {ShieldCooldown - (now - _lastShieldTrigger):F0}s remaining");
                    }
                    break;

                case 2:
                    if (now - _lastTimeSlow >= TimeSlowCooldown)
                    {
                        _lastTimeSlow = now;
                        GlobalGameSpeed.Value = 0.6f;
                    }
                    break;

                case 3:
                    break; // speed restored in OnZoneChanged

                case 4:
                    break; // frenzy is continuous while in Zone D — EnemySpawnManager checks zone directly
            }
        }

        private void FireZoneCallout(int zone)
        {
            switch (zone)
            {
                // Zone 1 callout handled inside ApplyZoneEffects (shield vs cooldown)
                case 2: CalloutManager.Instance?.TryShowCallout("TIME SLOW", 2f); break;
                case 3: CalloutManager.Instance?.TryShowCallout("NORMAL", 1.5f); break;
                case 4: CalloutManager.Instance?.TryShowCallout("FRENZY", 2f); break;
            }
        }

        private void ApplyBossDrainZone4()
        {
            _zone4DrainAccum += 2f * Time.deltaTime;
            if (_zone4DrainAccum < 1f) return;

            int damage = Mathf.FloorToInt(_zone4DrainAccum);
            _zone4DrainAccum -= damage;

            var spawnMgr = BirdSpawnManager.Instance;
            if (spawnMgr == null) return;
            if (spawnMgr.TryGetBossBird("player-3", out var boss3) && boss3 != null)
                boss3.TakeDamage(damage);
            if (spawnMgr.TryGetBossBird("player-4", out var boss4) && boss4 != null)
                boss4.TakeDamage(damage);
        }

        private void ActivateShields()
        {
            var spawnMgr = BirdSpawnManager.Instance;
            if (spawnMgr == null) return;
            if (spawnMgr.TryGetShip("player-1", out var ship1) && ship1 != null)
            {
                ship1.ShieldActive.Value = true;
                ship1.ShieldHealth.Value = 50;
            }
            if (spawnMgr.TryGetShip("player-2", out var ship2) && ship2 != null)
            {
                ship2.ShieldActive.Value = true;
                ship2.ShieldHealth.Value = 50;
            }
        }

        public void ResetEffects()
        {
            if (!IsServer) return;
            GlobalGameSpeed.Value = 1f;
            GlobalGravity.Value = 30f;
            _lastShieldTrigger = float.MinValue;
            _lastTimeSlow = float.MinValue;
            _zone4DrainAccum = 0f;
            _surgeCalloutFired = false;
        }

        public float GetBossFlapBonus() => UltrasonicZone.Value == 1 ? 1.3f : 1f;

        public float GetCooldownRemaining()
        {
            float now = Time.time;
            return UltrasonicZone.Value switch
            {
                1 => Mathf.Max(0f, ShieldCooldown - (now - _lastShieldTrigger)),
                2 => Mathf.Max(0f, TimeSlowCooldown - (now - _lastTimeSlow)),
                _ => 0f
            };
        }
    }
}