using System.Collections.Generic;
using OCoveiro.Data;
using Unity.Netcode;
using UnityEngine;

namespace OCoveiro.Player
{
    /// <summary>
    /// A lanterna: liga/desliga, gasta óleo e ilumina. Autoridade: o DONO.
    /// "Ligada" é sincronizada para os dois verem a luz; o óleo vai para a rede de ponto em ponto.
    /// Lantern.IsLit(ponto) responde se algum lugar está iluminado (bônus de dano agora,
    /// revelar Espectros e caminhos na semana 3).
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class Lantern : NetworkBehaviour
    {
        private static readonly List<Lantern> Active = new List<Lantern>();

        [SerializeField] private PlayerStatsData stats;
        [SerializeField] private Light lanternLight;

        [Header("Chama")]
        [SerializeField] private float flickerAmount = 0.12f;
        [SerializeField] private float flickerSpeed = 7f;

        private readonly NetworkVariable<bool> isOn = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> netOil = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private PlayerInputReader input;
        private float oil;
        private float flickerSeed;

        public bool IsOn => isOn.Value;
        public float MaxOil => stats != null ? stats.maxOil : 100f;
        public float Oil => IsOwner ? oil : netOil.Value;
        public float NormalizedOil => MaxOil > 0f ? Oil / MaxOil : 0f;
        public float Radius => stats != null ? stats.lanternRadius : 7f;
        public Vector3 LightPosition => lanternLight != null ? lanternLight.transform.position : transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        /// <summary>Algum ponto está dentro da luz de uma lanterna acesa? (distância no chão)</summary>
        public static bool IsLit(Vector3 point)
        {
            foreach (var lantern in Active)
            {
                if (!lantern.IsOn) continue;
                Vector3 offset = point - lantern.LightPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude <= lantern.Radius * lantern.Radius) return true;
            }
            return false;
        }

        /// <summary>Alguma parte de uma área está dentro da luz? (para objetos grandes, como passarelas)</summary>
        public static bool IsLit(Bounds area)
        {
            foreach (var lantern in Active)
            {
                if (!lantern.IsOn) continue;
                Vector3 source = lantern.LightPosition;
                Vector3 offset = area.ClosestPoint(new Vector3(source.x, area.center.y, source.z)) - source;
                offset.y = 0f;
                if (offset.sqrMagnitude <= lantern.Radius * lantern.Radius) return true;
            }
            return false;
        }

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            flickerSeed = Random.value * 100f;
        }

        public override void OnNetworkSpawn()
        {
            Active.Add(this);
            isOn.OnValueChanged += OnLitChanged;

            if (IsOwner)
            {
                oil = MaxOil;
                netOil.Value = oil;
            }

            ApplyLight();
        }

        public override void OnNetworkDespawn()
        {
            Active.Remove(this);
            isOn.OnValueChanged -= OnLitChanged;
        }

        private void OnLitChanged(bool previous, bool current) => ApplyLight();

        public void Toggle()
        {
            if (!IsOwner) return;
            if (!isOn.Value && oil <= 0f) return; // sem óleo não acende
            isOn.Value = !isOn.Value;
            ApplyLight();
        }

        public void Refill(float amount)
        {
            if (!IsOwner) return;
            oil = Mathf.Min(MaxOil, oil + amount);
            netOil.Value = oil;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                if (input.LanternPressed) Toggle();
                if (isOn.Value) BurnOil(Time.deltaTime);
            }

            Flicker();
        }

        private void BurnOil(float dt)
        {
            float drain = stats != null ? stats.oilDrainPerSecond : 4f;
            oil = Mathf.Max(0f, oil - drain * dt);

            if (oil <= 0f)
            {
                isOn.Value = false;
                ApplyLight();
            }

            if (Mathf.Abs(netOil.Value - oil) >= 1f || oil <= 0f) netOil.Value = oil;
        }

        private void ApplyLight()
        {
            if (lanternLight == null) return;
            lanternLight.enabled = isOn.Value;
            lanternLight.range = Radius;
        }

        private void Flicker()
        {
            if (lanternLight == null || !lanternLight.enabled) return;
            float baseIntensity = stats != null ? stats.lanternIntensity : 3f;
            float noise = Mathf.PerlinNoise(flickerSeed, Time.time * flickerSpeed) - 0.5f;
            lanternLight.intensity = baseIntensity * (1f + noise * 2f * flickerAmount);
        }
    }
}
