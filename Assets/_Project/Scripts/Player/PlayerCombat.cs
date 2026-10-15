using System.Collections.Generic;
using OCoveiro.Combat;
using OCoveiro.Core;
using OCoveiro.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OCoveiro.Player
{
    public enum PlayerState
    {
        Free,       // andando, pode fazer qualquer ação
        Attacking,  // preso no golpe
        Charging,   // segurando o ataque pesado
        Dodging,    // rolando (com i-frames no início)
        Healing,    // bebendo frasco, lento e vulnerável
        Staggered,  // atordoado depois de levar um golpe
        Downed,     // caído
    }

    /// <summary>
    /// O "cérebro" do jogador: máquina de estados do combate (State pattern simplificado).
    ///
    /// Divisão de autoridade (GDD, seção "Rede: quem manda em quê"):
    /// - O DONO decide as próprias ações: golpe, esquiva, cura. Nada espera a rede.
    /// - O HOST decide o que o golpe ACERTOU e aplica o dano (o cliente só diz "golpeei agora").
    /// - O HOST controla os frascos e a vida.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerInputReader), typeof(PlayerStamina))]
    [RequireComponent(typeof(Health), typeof(PlayerLife))]
    public class PlayerCombat : NetworkBehaviour, IDamageFilter
    {
        private const float InputBuffer = 0.2f;      // aperto antecipado ainda conta por 0,2 s
        private const float MaxOriginError = 2.5f;   // tolerância entre a posição do cliente e a do host

        [SerializeField] private WeaponData weapon;
        [SerializeField] private PlayerStatsData stats;
        [SerializeField] private ShovelVisual shovel;

        private readonly NetworkVariable<bool> invulnerable = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> flasks = new NetworkVariable<int>(3);

        private PlayerController mover;
        private PlayerInputReader input;
        private PlayerStamina stamina;
        private Lantern lantern;
        private Health health;
        private PlayerLife life;

        private float stateTime;
        private float lightBufferedAt = -10f;
        private float dodgeBufferedAt = -10f;

        private AttackKind currentKind;
        private int comboStep;
        private bool hitDone;
        private bool queuedLight;
        private bool chargeReached;
        private bool healApplied;
        private float staggerDuration;

        // Usados só no host para achar alvos.
        private readonly Collider[] hitBuffer = new Collider[24];
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

        public PlayerState State { get; private set; } = PlayerState.Free;
        public int Flasks => flasks.Value;
        public int MaxFlasks => stats != null ? stats.flaskCount : 3;

        private void Awake()
        {
            mover = GetComponent<PlayerController>();
            input = GetComponent<PlayerInputReader>();
            stamina = GetComponent<PlayerStamina>();
            lantern = GetComponent<Lantern>();
            health = GetComponent<Health>();
            life = GetComponent<PlayerLife>();
            if (shovel == null) shovel = GetComponent<ShovelVisual>();
        }

        public override void OnNetworkSpawn()
        {
            health.Damaged += OnDamaged;

            if (IsServer && stats != null)
            {
                health.SetMax(stats.maxHealth);
                flasks.Value = stats.flaskCount;
            }
        }

        public override void OnNetworkDespawn()
        {
            health.Damaged -= OnDamaged;
        }

        // ------------------------------------------------------------------ loop do dono

        private void Update()
        {
            if (!IsSpawned || !IsOwner || weapon == null || stats == null) return;

            if (life.IsDowned)
            {
                if (State != PlayerState.Downed) Enter(PlayerState.Downed);
                return;
            }
            if (State == PlayerState.Downed) Enter(PlayerState.Free);

            BufferInputs();
            stateTime += Time.deltaTime;

            switch (State)
            {
                case PlayerState.Free: UpdateFree(); break;
                case PlayerState.Attacking: UpdateAttacking(); break;
                case PlayerState.Charging: UpdateCharging(); break;
                case PlayerState.Dodging: UpdateDodging(); break;
                case PlayerState.Healing: UpdateHealing(); break;
                case PlayerState.Staggered: UpdateStaggered(); break;
            }

            UpdateDebugKeys();
        }

        private void BufferInputs()
        {
            if (input.LightPressed) lightBufferedAt = Time.time;
            if (input.DodgePressed) dodgeBufferedAt = Time.time;
        }

        private static bool Consume(ref float bufferedAt)
        {
            if (Time.time - bufferedAt > InputBuffer) return false;
            bufferedAt = -10f;
            return true;
        }

        private void UpdateFree()
        {
            if (stamina.HasAny && Consume(ref dodgeBufferedAt)) { StartDodge(); return; }
            if (stamina.HasAny && Consume(ref lightBufferedAt)) { StartAttack(AttackKind.Light, 0); return; }
            if (stamina.HasAny && input.HeavyPressed) { StartCharging(); return; }
            if (input.HealPressed) TryStartHeal();
        }

        private void UpdateAttacking()
        {
            var attack = weapon.Get(currentKind);
            float activeEnd = attack.windup + attack.active;

            // Momento do acerto: avisa o host, que calcula quem foi atingido.
            if (!hitDone && stateTime >= attack.windup)
            {
                hitDone = true;
                PerformHitRpc((byte)currentKind, comboStep, transform.position, transform.forward);
            }

            bool canChain = currentKind == AttackKind.Light && comboStep < weapon.comboLength - 1;
            if (canChain && Consume(ref lightBufferedAt)) queuedLight = true;

            if (queuedLight && stateTime >= activeEnd + attack.chainDelay)
            {
                if (stamina.HasAny) StartAttack(AttackKind.Light, comboStep + 1);
                else Enter(PlayerState.Free);
                return;
            }

            // A esquiva cancela a recuperação do golpe (nunca a preparação).
            if (stateTime >= activeEnd && stamina.HasAny && Consume(ref dodgeBufferedAt))
            {
                StartDodge();
                return;
            }

            if (stateTime >= activeEnd + attack.recovery) Enter(PlayerState.Free);
        }

        private void UpdateCharging()
        {
            mover.Face(AimDirection(), false);

            if (!chargeReached && stateTime >= weapon.heavyChargeTime)
            {
                chargeReached = true;
                if (shovel != null) shovel.PulseCharged();
            }

            if (stamina.HasAny && Consume(ref dodgeBufferedAt)) { StartDodge(); return; }

            if (!input.HeavyHeld)
            {
                // Soltou antes de carregar: cancela sem gastar stamina.
                if (chargeReached) StartAttack(AttackKind.Heavy, 0);
                else Enter(PlayerState.Free);
            }
        }

        private void UpdateDodging()
        {
            if (invulnerable.Value && stateTime >= stats.iFrameDuration) invulnerable.Value = false;
            if (stateTime >= stats.dodgeDuration + stats.dodgeRecovery) Enter(PlayerState.Free);
        }

        private void UpdateHealing()
        {
            if (!healApplied && stateTime >= stats.flaskUseTime)
            {
                healApplied = true;
                DrinkFlaskRpc();
            }

            if (stateTime >= stats.flaskUseTime + 0.15f) Enter(PlayerState.Free);
        }

        private void UpdateStaggered()
        {
            if (stateTime >= staggerDuration) Enter(PlayerState.Free);
        }

        // ------------------------------------------------------------------ ações

        private void StartAttack(AttackKind kind, int step)
        {
            var attack = weapon.Get(kind);
            if (!stamina.TrySpend(attack.staminaCost))
            {
                Enter(PlayerState.Free);
                return;
            }

            currentKind = kind;
            comboStep = step;
            hitDone = false;
            queuedLight = false;

            Enter(PlayerState.Attacking);
            mover.Face(AimDirection(), true);
            mover.AddImpulse(transform.forward * attack.lunge);

            if (shovel != null) shovel.PlayAttack(kind, step, attack);
            AttackVisualRpc((byte)kind, step);
        }

        private void StartCharging()
        {
            chargeReached = false;
            Enter(PlayerState.Charging);
            if (shovel != null) shovel.SetCharging(true);
            ChargeVisualRpc(true);
        }

        private void StartDodge()
        {
            if (!stamina.TrySpend(stats.dodgeCost)) return;

            // Sem direção apertada, rola para onde está olhando.
            Vector3 direction = mover.MoveDirection.sqrMagnitude > 0.01f ? mover.MoveDirection : transform.forward;

            Enter(PlayerState.Dodging);
            mover.StartDash(direction, stats.dodgeDistance, stats.dodgeDuration);
            invulnerable.Value = true;
            if (shovel != null) shovel.ResetPose();
        }

        private void TryStartHeal()
        {
            if (flasks.Value <= 0 || health.Current >= health.Max) return;
            healApplied = false;
            Enter(PlayerState.Healing);
            if (shovel != null) shovel.ResetPose();
        }

        /// <summary>Troca de estado: desfaz o que o estado anterior travou e aplica as regras do novo.</summary>
        private void Enter(PlayerState next)
        {
            if (State == PlayerState.Dodging)
            {
                mover.StopDash();
                if (invulnerable.Value) invulnerable.Value = false;
            }

            if (State == PlayerState.Charging && next != PlayerState.Attacking)
            {
                if (shovel != null) shovel.SetCharging(false);
                ChargeVisualRpc(false);
            }

            mover.MovementLocked = false;
            mover.RotationLocked = false;
            mover.SpeedMultiplier = 1f;

            State = next;
            stateTime = 0f;

            switch (next)
            {
                case PlayerState.Attacking:
                case PlayerState.Staggered:
                case PlayerState.Downed:
                    mover.MovementLocked = true;
                    mover.RotationLocked = true;
                    break;
                case PlayerState.Charging:
                    mover.RotationLocked = true;
                    mover.SpeedMultiplier = weapon.chargeMoveMultiplier;
                    break;
                case PlayerState.Dodging:
                    mover.RotationLocked = true;
                    break;
                case PlayerState.Healing:
                    mover.SpeedMultiplier = stats.healMoveMultiplier;
                    break;
            }
        }

        /// <summary>Para onde o golpe vai: analógico direito, senão o mouse, senão para onde anda.</summary>
        private Vector3 AimDirection()
        {
            Vector2 stick = input.AimStick;
            if (stick.sqrMagnitude > 0.2f) return mover.ToWorldDirection(stick);

            var cam = Camera.main;
            if (!input.LastDeviceWasGamepad && cam != null && Mouse.current != null)
            {
                Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                var ground = new Plane(Vector3.up, transform.position);
                if (ground.Raycast(ray, out float distance))
                {
                    Vector3 toCursor = ray.GetPoint(distance) - transform.position;
                    toCursor.y = 0f;
                    if (toCursor.sqrMagnitude > 0.04f) return toCursor.normalized;
                }
            }

            return mover.MoveDirection.sqrMagnitude > 0.01f ? mover.MoveDirection : transform.forward;
        }

        // ------------------------------------------------------------------ levar dano

        /// <summary>Host: i-frames da esquiva e caído recusam o dano.</summary>
        public bool CanReceive(in DamageInfo info) => !invulnerable.Value && !life.IsDowned;

        private void OnDamaged(float amount, Vector3 direction, bool heavy)
        {
            if (!IsOwner) return;

            IsoCamera.Shake(heavy ? 0.35f : 0.2f, 0.2f);
            if (State == PlayerState.Downed || stats == null) return;

            staggerDuration = heavy ? stats.heavyHitStun : stats.hitStun;
            Enter(PlayerState.Staggered);
            mover.AddImpulse(direction.normalized * (stats.hitKnockback * (heavy ? 1.6f : 1f)));
            if (shovel != null) shovel.ResetPose();
        }

        // ------------------------------------------------------------------ RPCs para o host

        /// <summary>
        /// O dono diz "golpeei agora, daqui, nesta direção". O host confere e decide quem foi atingido.
        /// O dano vem do WeaponData do host, não do cliente.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void PerformHitRpc(byte kind, int step, Vector3 origin, Vector3 forward, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || health.IsDead || weapon == null) return;

            var attackKind = (AttackKind)kind;
            var attack = weapon.Get(attackKind);

            // O cliente está um pouco à frente do host; se a diferença for absurda, usa a posição do host.
            if ((origin - transform.position).sqrMagnitude > MaxOriginError * MaxOriginError) origin = transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
            forward.Normalize();

            bool finisher = attackKind == AttackKind.Light && step >= weapon.comboLength - 1;
            float knockback = attack.knockback * (finisher ? weapon.finisherKnockbackMultiplier : 1f);

            hitTargets.Clear();
            bool anyHit = false;
            int count = Physics.OverlapSphereNonAlloc(origin + Vector3.up, attack.reach, hitBuffer,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var target = hitBuffer[i].GetComponentInParent<IDamageable>();
                if (target == null || ReferenceEquals(target, health) || target.Team == health.Team) continue;
                if (!hitTargets.Add(target)) continue; // um alvo com vários colliders só leva um golpe

                Vector3 toTarget = target.transform.position - origin;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(forward, toTarget) > attack.arcAngle * 0.5f) continue;

                float damage = attack.damage;
                if (Lantern.IsLit(target.transform.position)) damage *= weapon.litDamageMultiplier;

                var info = new DamageInfo
                {
                    Amount = damage,
                    Direction = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : forward,
                    Knockback = knockback,
                    Heavy = attackKind == AttackKind.Heavy,
                    Stagger = attack.stagger,
                    SourceTeam = health.Team,
                    SourceObjectId = NetworkObjectId,
                };

                if (target.ApplyDamage(info)) anyHit = true;
            }

            if (anyHit) HitConfirmedRpc(attackKind == AttackKind.Heavy);
        }

        [Rpc(SendTo.Server)]
        private void DrinkFlaskRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (flasks.Value <= 0 || health.IsDead || stats == null) return;

            flasks.Value--;
            health.Heal(stats.flaskHeal);
        }

        /// <summary>Host: devolve os frascos (altar de vigília, semana 4).</summary>
        public void RefillFlasks()
        {
            if (IsServer && stats != null) flasks.Value = stats.flaskCount;
        }

        // ------------------------------------------------------------------ feedback

        /// <summary>O parceiro vê o golpe (a pá provisória) no PC dele.</summary>
        [Rpc(SendTo.NotOwner)]
        private void AttackVisualRpc(byte kind, int step)
        {
            if (shovel != null && weapon != null)
                shovel.PlayAttack((AttackKind)kind, step, weapon.Get((AttackKind)kind));
        }

        [Rpc(SendTo.NotOwner)]
        private void ChargeVisualRpc(bool charging)
        {
            if (shovel != null) shovel.SetCharging(charging);
        }

        /// <summary>O host confirma o acerto: tremor de câmera e hit-stop na pá de quem bateu.</summary>
        [Rpc(SendTo.Owner)]
        private void HitConfirmedRpc(bool heavy)
        {
            IsoCamera.Shake(heavy ? 0.25f : 0.1f, heavy ? 0.15f : 0.1f);
            if (shovel != null) shovel.Freeze(heavy ? 0.1f : 0.06f);
        }

        // ------------------------------------------------------------------ teclas de teste

        private void UpdateDebugKeys()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (input.DebugDamagePressed) DebugDamageSelfRpc();
            if (input.DebugRefillPressed)
            {
                if (lantern != null) lantern.Refill(float.MaxValue);
                stamina.RefillFull();
                DebugRefillRpc();
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Rpc(SendTo.Server)]
        private void DebugDamageSelfRpc()
        {
            health.ApplyDamage(new DamageInfo
            {
                Amount = 25f,
                Direction = -transform.forward,
                SourceTeam = Team.Enemies,
            });
        }

        [Rpc(SendTo.Server)]
        private void DebugRefillRpc()
        {
            RefillFlasks();
            health.RestoreFull();
        }
#endif
    }
}
