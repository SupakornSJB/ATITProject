using Shared;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class EnvironmentController : NetworkSingleton<EnvironmentController>
    {
        public NetworkVariable<float> Temperature = new(
            22f,
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

        private float _lastShieldTrigger = float.MinValue;
        private float _lastTimeSlow = float.MinValue;
        private float _lastFrenzy = float.MinValue;
        private int _previousZone = 3;
        private float _zone4DrainAccum;

        private void Update()
        {
            if (!IsServer) return;
            DrainEnvironmentQueue();
            if (GameRoundManager.Instance?.CurrentPhase.Value == RoundPhase.Active)
            {
                ApplyZoneEffects();
                ApplyTemperatureEffects();
            }
        }

        private void DrainEnvironmentQueue()
        {
#if UNITY_SERVER
            while (Server.IoTGameServiceImpl.EnvironmentQueue.TryDequeue(out var evt))
                OnEnvironmentUpdate(evt.EnvKey, evt.RawValue);
#endif
        }

        public void OnEnvironmentUpdate(string envKey, float value)
        {
            switch (envKey)
            {
                case "temperature":
                    Temperature.Value = value;
                    break;
                case "zone":
                    int zone = Mathf.Clamp(Mathf.RoundToInt(value), 1, 4);
                    if (zone != UltrasonicZone.Value)
                        OnZoneChanged(zone);
                    UltrasonicZone.Value = zone;
                    ActiveZoneEffect.Value = zone;
                    break;
            }
        }

        private void OnZoneChanged(int newZone)
        {
            if (_previousZone == 2 && newZone != 2)
            {
                GlobalGameSpeed.Value = 1f;
                CalloutManager.Instance?.ShowCalloutClientRpc("NORMAL", 1.5f);
            }
            _previousZone = newZone;
        }

        private void ApplyZoneEffects()
        {
            int zone = UltrasonicZone.Value;
            float now = Time.time;

            _zone4DrainAccum = zone == 4 ? _zone4DrainAccum : 0f;

            switch (zone)
            {
                case 1:
                    if (now - _lastShieldTrigger >= 15f)
                    {
                        _lastShieldTrigger = now;
                        ActivateShields();
                        CalloutManager.Instance?.ShowCalloutClientRpc("SHIELD ACTIVATED", 2f);
                    }
                    break;

                case 2:
                    if (now - _lastTimeSlow >= 20f)
                    {
                        _lastTimeSlow = now;
                        GlobalGameSpeed.Value = 0.6f;
                        CalloutManager.Instance?.ShowCalloutClientRpc("TIME SLOW", 2f);
                    }
                    break;

                case 3:
                    if (!Mathf.Approximately(GlobalGameSpeed.Value, 1f))
                        GlobalGameSpeed.Value = 1f;
                    break;

                case 4:
                    if (now - _lastFrenzy >= 10f)
                    {
                        _lastFrenzy = now;
                        EnemySpawnManager.Instance?.TriggerFrenzy();
                        CalloutManager.Instance?.ShowCalloutClientRpc("FRENZY", 2f);
                    }
                    ApplyBossDrainZone4();
                    break;
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

        private void ApplyTemperatureEffects()
        {
            float temp = Temperature.Value;
            float interval;
            if (temp < 20f) interval = 8f;
            else if (temp < 28f) interval = 5f;
            else if (temp < 34f) interval = 3f;
            else interval = 1.5f;

            EnemySpawnManager.Instance?.SetSpawnInterval(interval);
        }

        public void ResetEffects()
        {
            if (!IsServer) return;
            GlobalGameSpeed.Value = 1f;
            _lastShieldTrigger = float.MinValue;
            _lastTimeSlow = float.MinValue;
            _lastFrenzy = float.MinValue;
            _zone4DrainAccum = 0f;
        }

        // Zone 1 grants boss birds +30% flap impulse
        public float GetBossFlapBonus() => UltrasonicZone.Value == 1 ? 1.3f : 1f;
    }
}