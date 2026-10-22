namespace OCoveiro.AI
{
    public enum EnemyStateId : byte
    {
        Idle,     // parado no posto, procurando jogadores
        Alert,    // gritando para chamar os outros (Errante Vigia)
        Chase,    // indo atrás do alvo
        Attack,   // aviso, golpe e recuperação
        Stagger,  // cambaleando depois de apanhar
        Return,   // perdeu o alvo, voltando para o posto
        Dead,
    }

    /// <summary>
    /// Base dos estados da IA (padrão State). Cada estado é uma classe pequena com
    /// Enter / Tick / Exit; o EnemyBrain só sabe trocar de um para outro.
    /// Roda apenas no host.
    /// </summary>
    public abstract class EnemyState
    {
        protected readonly EnemyBrain Brain;
        protected float Elapsed; // segundos desde que o estado começou

        protected EnemyState(EnemyBrain brain)
        {
            Brain = brain;
        }

        public void Enter()
        {
            Elapsed = 0f;
            OnEnter();
        }

        public void Tick(float deltaTime)
        {
            Elapsed += deltaTime;
            OnTick(deltaTime);
        }

        public virtual void Exit() { }

        protected virtual void OnEnter() { }
        protected abstract void OnTick(float deltaTime);
    }
}
