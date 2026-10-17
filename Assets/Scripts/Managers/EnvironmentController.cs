using System.Collections.Generic;
using IoTGame.Core;
using IoTGame.Models;
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

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            EnvironmentEventDispatcher.OnReceived += OnEnvironmentEventReceived;
        }

        public override void OnNetworkDespawn()
        {
            EnvironmentEventDispatcher.OnReceived -= OnEnvironmentEventReceived;
        }

        private void OnEnvironmentEventReceived(EnvironmentEvent evt) =>
            OnEnvironmentUpdate(evt.EnvKey, evt.RawValue);

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
        // Range: 40% → 3s interval, 80% → 0.6s target (EnemySpawnManager clamps to a 1s minimum)
        private void ApplySpawnRateEffect(float humidity)
        {
            float interval = Mathf.Lerp(3f, 0.6f, Mathf.InverseLerp(40f, 80f, humidity));
            EnemySpawnManager.Instance?.SetSpawnInterval(interval);

            if (humidity >= 65f && !_surgeCalloutFired)
            {
                _surgeCalloutFired = true;
                CalloutManager.Instance?.TryShowCallout("SPAWN SURGE", 2f);
            }
            else if (humidity < 50f && _surgeCalloutFired)
            {
                _surgeCalloutFired = false;
                CalloutManager.Instance?.TryShowCallout("SWARM THINNING", 2f);
            }
        }

        private void OnZoneChanged(int from, int to) { }

        private void ApplyZoneEffects(int zone)
        {
            GlobalGameSpeed.Value = zone switch
            {
                1 => 0.5f,
                2 => 0.75f,
                3 => 1.0f,
                4 => 1.5f,
                _ => 1.0f
            };
        }

        private void FireZoneCallout(int zone)
        {
            switch (zone)
            {
                case 1: CalloutManager.Instance?.TryShowCallout("GAME SPEED: VERY SLOW", 2f); break;
                case 2: CalloutManager.Instance?.TryShowCallout("GAME SPEED: SLOW", 2f); break;
                case 3: CalloutManager.Instance?.TryShowCallout("GAME SPEED: NORMAL", 1.5f); break;
                case 4: CalloutManager.Instance?.TryShowCallout("GAME SPEED: FAST", 2f); break;
            }
        }

        public void ResetEffects()
        {
            if (!IsServer) return;
            GlobalGameSpeed.Value = 1f;
            GlobalGravity.Value = 30f;
            _surgeCalloutFired = false;
        }

        public float GetCooldownRemaining() => 0f;
    }
}