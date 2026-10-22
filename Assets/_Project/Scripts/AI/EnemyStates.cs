using UnityEngine;

namespace OCoveiro.AI
{
    /// <summary>Parado no posto. Procura jogadores 4 vezes por segundo.</summary>
    public class IdleState : EnemyState
    {
        private float scanTimer;

        public IdleState(EnemyBrain brain) : base(brain) { }

        protected override void OnEnter()
        {
            Brain.Motor.Stop();
            scanTimer = 0f;
        }

        protected override void OnTick(float deltaTime)
        {
            scanTimer -= deltaTime;
            if (scanTimer > 0f) return;
            scanTimer = 0.25f;

            var target = Brain.FindTarget(Brain.Data.detectionRadius);
            if (target == null) return;

            Brain.Target = target;
            Brain.ChangeState(Brain.Data.alertsOthers ? EnemyStateId.Alert : EnemyStateId.Chase);
        }
    }

    /// <summary>Errante Vigia: encara o jogador, grita e chama quem estiver perto.</summary>
    public class AlertState : EnemyState
    {
        public AlertState(EnemyBrain brain) : base(brain) { }

        protected override void OnEnter() => Brain.Motor.Stop();

        protected override void OnTick(float deltaTime)
        {
            if (Brain.Target != null) Brain.Motor.FaceTowards(Brain.Target.position, Brain.Data.turnSpeed);
            if (Elapsed < Brain.Data.alertDuration) return;

            Brain.AlertNearby();
            Brain.ChangeState(EnemyStateId.Chase);
        }
    }

    /// <summary>Persegue o alvo, troca para o jogador mais próximo e ataca quando chega perto.</summary>
    public class ChaseState : EnemyState
    {
        private float repathTimer;
        private float retargetTimer;

        public ChaseState(EnemyBrain brain) : base(brain) { }

        protected override void OnEnter()
        {
            repathTimer = 0f;
            retargetTimer = 1f;
        }

        protected override void OnTick(float deltaTime)
        {
            var data = Brain.Data;

            // Alvo caiu ou fugiu longe demais: procura outro ou desiste.
            if (!Brain.IsValidTarget(Brain.Target) || Brain.PlanarDistanceTo(Brain.Target.position) > data.loseRadius)
            {
                Brain.Target = Brain.FindTarget(data.detectionRadius);
                if (Brain.Target == null)
                {
                    Brain.ChangeState(EnemyStateId.Return);
                    return;
                }
            }

            // De vez em quando troca para o jogador mais próximo (GDD: "ataca o alvo mais próximo").
            retargetTimer -= deltaTime;
            if (retargetTimer <= 0f)
            {
                retargetTimer = 1f;
                var nearest = Brain.FindTarget(data.detectionRadius);
                if (nearest != null && nearest != Brain.Target &&
                    Brain.PlanarDistanceTo(nearest.position) + 2f < Brain.PlanarDistanceTo(Brain.Target.position))
                    Brain.Target = nearest;
            }

            var target = Brain.Target;
            float distance = Brain.PlanarDistanceTo(target.position);

            if (distance <= data.attackRange)
            {
                Brain.Motor.Stop();
                Brain.Motor.FaceTowards(target.position, data.turnSpeed);
                if (Time.time >= Brain.NextAttackTime) Brain.ChangeState(EnemyStateId.Attack);
                return;
            }

            repathTimer -= deltaTime;
            if (repathTimer > 0f) return;
            repathTimer = 0.2f;

            // O Espectro tenta chegar pelas costas antes de encostar.
            Vector3 destination = target.position;
            if (data.flankFromBehind && distance > 2.5f) destination -= target.forward * 1.8f;
            Brain.Motor.MoveTo(destination, data.chaseSpeed);
        }
    }

    /// <summary>
    /// Aviso (windup), golpe e recuperação. Durante o começo do aviso ainda vira para o alvo;
    /// no fim do aviso fica travado: é a janela para o jogador esquivar.
    /// </summary>
    public class AttackState : EnemyState
    {
        private bool hitDone;

        public AttackState(EnemyBrain brain) : base(brain) { }

        protected override void OnEnter()
        {
            Brain.Motor.Stop();
            hitDone = false;
        }

        protected override void OnTick(float deltaTime)
        {
            var data = Brain.Data;

            if (Elapsed < data.attackWindup * 0.7f && Brain.Target != null)
                Brain.Motor.FaceTowards(Brain.Target.position, data.turnSpeed);

            if (!hitDone && Elapsed >= data.attackWindup)
            {
                hitDone = true;
                Brain.Motor.AddImpulse(Brain.transform.forward * data.attackLunge);
                Brain.PerformAttack();
            }

            if (Elapsed >= data.attackWindup + data.attackActive + data.attackRecovery)
            {
                Brain.NextAttackTime = Time.time + data.attackCooldown;
                Brain.ChangeState(EnemyStateId.Chase);
            }
        }
    }

    /// <summary>Cambaleia (cancela o golpe que estava preparando).</summary>
    public class StaggerState : EnemyState
    {
        public StaggerState(EnemyBrain brain) : base(brain) { }

        protected override void OnEnter() => Brain.Motor.Stop();

        protected override void OnTick(float deltaTime)
        {
            if (Elapsed < Brain.Data.staggerDuration) return;
            Brain.ChangeState(Brain.IsValidTarget(Brain.Target) ? EnemyStateId.Chase : EnemyStateId.Return);
        }
    }

    /// <summary>Volta para o posto. Se vir alguém no caminho, persegue de novo. Chegando, recupera a vida.</summary>
    public class ReturnState : EnemyState
    {
        private float scanTimer;

        public ReturnState(EnemyBrain brain) : base(brain) { }

        protected override void OnEnter()
        {
            Brain.Target = null;
            scanTimer = 0.5f;
            Brain.Motor.MoveTo(Brain.SpawnPosition, Brain.Data.chaseSpeed * 0.8f);
        }

        protected override void OnTick(float deltaTime)
        {
            scanTimer -= deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.25f;
                var target = Brain.FindTarget(Brain.Data.detectionRadius);
                if (target != null)
                {
                    Brain.Target = target;
                    Brain.ChangeState(EnemyStateId.Chase);
                    return;
                }
            }

            if (!Brain.Motor.HasArrived(0.4f)) return;

            Brain.Motor.Warp(Brain.transform.position, Brain.SpawnRotation);
            Brain.Health.RestoreFull();
            Brain.ChangeState(EnemyStateId.Idle);
        }
    }

    /// <summary>Morto. Fica assim até o altar fazer os inimigos voltarem (semana 4).</summary>
    public class DeadState : EnemyState
    {
        public DeadState(EnemyBrain brain) : base(brain) { }

        protected override void OnEnter()
        {
            Brain.Motor.Stop();
            Brain.Target = null;
        }

        protected override void OnTick(float deltaTime) { }
    }
}
