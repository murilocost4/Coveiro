using System.Collections.Generic;
using OCoveiro.Combat;
using OCoveiro.Core;
using OCoveiro.Data;
using OCoveiro.Player;
using Unity.Netcode;
using UnityEngine;

namespace OCoveiro.AI
{
    /// <summary>
    /// O cérebro do inimigo: máquina de estados que roda SÓ NO HOST.
    /// Os clientes recebem a posição (NetworkTransform) e o estado atual (NetworkVariable),
    /// e o EnemyVisual usa o estado para mostrar o aviso do golpe, o grito, a queda...
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyBrain : NetworkBehaviour, IDamageFilter
    {
        private static readonly List<EnemyBrain> all = new List<EnemyBrain>();

        [SerializeField] private EnemyData data;

        private readonly NetworkVariable<byte> netState = new NetworkVariable<byte>((byte)EnemyStateId.Idle);

        private readonly Dictionary<EnemyStateId, EnemyState> states = new Dictionary<EnemyStateId, EnemyState>();
        private EnemyState current;
        private Collider[] colliders;

        private readonly Collider[] hitBuffer = new Collider[16];
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private readonly RaycastHit[] rayHits = new RaycastHit[16];

        private float poiseDamage;
        private float lastPoiseHit = -100f;

        public static IReadOnlyList<EnemyBrain> All => all;

        public EnemyData Data => data;
        public Health Health { get; private set; }
        public IEnemyMotor Motor { get; private set; }
        public EnemyStateId StateId => (EnemyStateId)netState.Value;
        /// <summary>Quando o estado atual começou (em todos os PCs; usado pelos visuais).</summary>
        public float StateChangedAt { get; private set; }

        // Só usados no host.
        public Transform Target { get; set; }
        public Vector3 SpawnPosition { get; private set; }
        public Quaternion SpawnRotation { get; private set; }
        public float NextAttackTime { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void Awake()
        {
            Health = GetComponent<Health>();
            Motor = GetComponent<IEnemyMotor>();
            colliders = GetComponentsInChildren<Collider>(true);
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            netState.OnValueChanged += OnStateChanged;
            StateChangedAt = Time.time;
            ApplyColliders();

            if (Motor == null || data == null)
            {
                Debug.LogError($"[IA] {name} está sem EnemyData ou sem motor (NavMeshMotor/GhostMotor).");
                return;
            }

            Motor.Initialize(IsServer);
            if (!IsServer) return;

            SpawnPosition = transform.position;
            SpawnRotation = transform.rotation;

            Health.DamagedServer += OnDamagedServer;
            Health.DiedServer += OnDiedServer;
            GameEvents.OnPlayerSpawned += OnPartyChanged;
            GameEvents.OnPlayerDespawned += OnPartyChanged;
            ApplyPartyScaling(true);

            states[EnemyStateId.Idle] = new IdleState(this);
            states[EnemyStateId.Alert] = new AlertState(this);
            states[EnemyStateId.Chase] = new ChaseState(this);
            states[EnemyStateId.Attack] = new AttackState(this);
            states[EnemyStateId.Stagger] = new StaggerState(this);
            states[EnemyStateId.Return] = new ReturnState(this);
            states[EnemyStateId.Dead] = new DeadState(this);
            ChangeState(EnemyStateId.Idle);
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            netState.OnValueChanged -= OnStateChanged;

            if (!IsServer) return;
            Health.DamagedServer -= OnDamagedServer;
            Health.DiedServer -= OnDiedServer;
            GameEvents.OnPlayerSpawned -= OnPartyChanged;
            GameEvents.OnPlayerDespawned -= OnPartyChanged;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || current == null) return;
            current.Tick(Time.deltaTime);
        }

        public void ChangeState(EnemyStateId next)
        {
            if (!IsServer) return;
            current?.Exit();
            current = states[next];
            netState.Value = (byte)next;
            StateChangedAt = Time.time;
            current.Enter();
        }

        private void OnStateChanged(byte previous, byte next)
        {
            StateChangedAt = Time.time;
            ApplyColliders();
        }

        /// <summary>Morto não bloqueia caminho nem leva golpe.</summary>
        private void ApplyColliders()
        {
            bool alive = StateId != EnemyStateId.Dead;
            foreach (var c in colliders)
                if (c != null) c.enabled = alive;
        }

        // ------------------------------------------------------------------ percepção (host)

        /// <summary>Jogador válido mais próximo dentro do raio (e à vista, se o inimigo precisar enxergar).</summary>
        public Transform FindTarget(float radius)
        {
            Transform best = null;
            float bestSq = radius * radius;

            foreach (var player in PlayerAppearance.All)
            {
                if (player == null || !IsValidTarget(player.transform)) continue;

                Vector3 offset = player.transform.position - transform.position;
                offset.y = 0f;
                float sq = offset.sqrMagnitude;
                if (sq > bestSq) continue;
                if (data.needsLineOfSight && !HasLineOfSight(player.transform)) continue;

                best = player.transform;
                bestSq = sq;
            }

            return best;
        }

        /// <summary>Jogador vivo e de pé.</summary>
        public bool IsValidTarget(Transform target)
        {
            if (target == null) return false;
            var life = target.GetComponent<PlayerLife>();
            var health = target.GetComponent<Health>();
            return (life == null || !life.IsDowned) && (health == null || !health.IsDead);
        }

        public float PlanarDistanceTo(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>Raio dos olhos até o peito do alvo; personagens não bloqueiam, paredes sim.</summary>
        public bool HasLineOfSight(Transform target)
        {
            Vector3 eye = transform.position + Vector3.up * 1.5f;
            Vector3 toTarget = target.position + Vector3.up - eye;
            float distance = toTarget.magnitude;
            if (distance < 0.01f) return true;

            int count = Physics.RaycastNonAlloc(eye, toTarget / distance, rayHits, distance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (rayHits[i].collider.GetComponentInParent<Health>() == null) return false;
            return true;
        }

        /// <summary>Grito do Vigia: acorda os inimigos parados por perto.</summary>
        public void AlertNearby()
        {
            if (Target == null) return;
            foreach (var other in all)
            {
                if (other == this || other == null || !other.IsSpawned) continue;
                if (other.PlanarDistanceTo(transform.position) <= data.alertRadius) other.Alert(Target);
            }
        }

        public void Alert(Transform target)
        {
            if (!IsServer || target == null) return;
            if (StateId != EnemyStateId.Idle && StateId != EnemyStateId.Return) return;
            Target = target;
            ChangeState(EnemyStateId.Chase);
        }

        // ------------------------------------------------------------------ ataque (host)

        public void PerformAttack()
        {
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;

            hitTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(origin + Vector3.up, data.attackReach, hitBuffer,
                Physics.AllLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var target = hitBuffer[i].GetComponentInParent<IDamageable>();
                if (target == null || ReferenceEquals(target, Health) || target.Team == Health.Team) continue;
                if (!hitTargets.Add(target)) continue;

                Vector3 toTarget = target.transform.position - origin;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(forward, toTarget) > data.attackArc * 0.5f) continue;

                target.ApplyDamage(new DamageInfo
                {
                    Amount = data.attackDamage,
                    Direction = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : forward,
                    Knockback = data.attackKnockback,
                    Heavy = data.heavyAttack,
                    SourceTeam = Health.Team,
                    SourceObjectId = NetworkObjectId,
                });
            }
        }

        // ------------------------------------------------------------------ levar dano (host)

        /// <summary>Espectro: no escuro o golpe atravessa sem causar dano.</summary>
        public bool CanReceive(in DamageInfo info) => data == null || !data.spectral || Lantern.IsLit(transform.position);

        private void OnDamagedServer(DamageInfo info)
        {
            if (Health.IsDead) return;

            Motor.AddImpulse(info.Direction * (info.Knockback * (1f - data.knockbackResistance)));

            // Apanhou de quem não estava vendo: vai atrás de quem bateu.
            if (!IsValidTarget(Target) &&
                NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(info.SourceObjectId, out var attacker) &&
                IsValidTarget(attacker.transform))
                Target = attacker.transform;

            if (Time.time - lastPoiseHit > data.poiseResetTime) poiseDamage = 0f;
            poiseDamage += info.Amount;
            lastPoiseHit = Time.time;

            if (info.Stagger || poiseDamage >= data.poise)
            {
                poiseDamage = 0f;
                ChangeState(EnemyStateId.Stagger);
                return;
            }

            if ((StateId == EnemyStateId.Idle || StateId == EnemyStateId.Return) && Target != null)
                ChangeState(data.alertsOthers && StateId == EnemyStateId.Idle ? EnemyStateId.Alert : EnemyStateId.Chase);
        }

        private void OnDiedServer() => ChangeState(EnemyStateId.Dead);

        /// <summary>Host: volta ao posto com vida cheia (usado pelo altar na semana 4).</summary>
        public void ResetToSpawn()
        {
            if (!IsServer) return;
            Health.RestoreFull();
            Target = null;
            poiseDamage = 0f;
            Motor.Warp(SpawnPosition, SpawnRotation);
            ChangeState(EnemyStateId.Idle);
        }

        // ------------------------------------------------------------------ co-op (host)

        private void OnPartyChanged(PlayerAppearance player) => ApplyPartyScaling(false);

        /// <summary>Com 2 jogadores os inimigos têm mais vida (GDD: 1,5×).</summary>
        private void ApplyPartyScaling(bool refill)
        {
            int players = 0;
            foreach (var p in PlayerAppearance.All)
                if (p != null) players++;

            float multiplier = players >= 2 ? data.coopHealthMultiplier : 1f;
            Health.SetMax(data.maxHealth * multiplier, refill);
        }
    }
}
