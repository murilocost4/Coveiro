using System.Collections.Generic;
using UnityEngine;

namespace OCoveiro.Core
{
    /// <summary>
    /// Ponte entre a lógica e o Animator do modelo (jogador ou inimigo).
    ///
    /// Ideia central: NADA de animação é enviado pela rede. Cada PC anima sozinho a partir do que
    /// já está sincronizado: a velocidade vem do quanto o personagem andou neste quadro, e as ações
    /// (golpe, esquiva, morte...) vêm dos estados e RPCs que já existiam.
    ///
    /// "Fitted" = toca a animação acelerada ou desacelerada para caber no tempo do jogo
    /// (ex.: o golpe do inimigo acerta exatamente no fim do aviso, seja ele de 0,9 s ou de 0,5 s).
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int ActionSpeedId = Animator.StringToHash("ActionSpeed");

        [Tooltip("Nome do estado de andar/correr no Animator Controller.")]
        [SerializeField] private string locomotionState = "Locomotion";
        [Tooltip("Suaviza a troca entre parado, andar e correr.")]
        [SerializeField] private float speedSmoothing = 12f;

        private Animator animator;
        private readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();
        private RuntimeAnimatorController cachedController;
        private Vector3 lastPosition;
        private float smoothedSpeed;
        private float freezeUntil;
        private bool frozen;

        /// <summary>Animator do modelo ATIVO (o Coveiro e a Aprendiz estão no mesmo prefab; só um fica ligado).</summary>
        public Animator Animator
        {
            get
            {
                if (animator != null && animator.isActiveAndEnabled) return animator;
                animator = null;
                foreach (var candidate in GetComponentsInChildren<Animator>(false))
                {
                    if (candidate.runtimeAnimatorController == null) continue;
                    animator = candidate;
                    animator.applyRootMotion = false;
                    break;
                }
                return animator;
            }
        }

        public bool HasAnimator => Animator != null;

        private void OnEnable() => lastPosition = transform.position;

        private void Update()
        {
            var anim = Animator;
            if (anim == null) return;

            // Hit-stop: congela a animação por um instante quando o golpe acerta.
            if (frozen && Time.time >= freezeUntil)
            {
                frozen = false;
                anim.speed = 1f;
            }

            // Velocidade real no chão (funciona igual para o dono, o parceiro e os inimigos).
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            float speed = delta.magnitude / dt;
            if (speed > 20f) speed = 0f; // teleporte, não corrida

            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-speedSmoothing * dt));
            anim.SetFloat(SpeedId, smoothedSpeed);
        }

        /// <summary>Toca uma ação na velocidade normal.</summary>
        public void Play(string state, float fade = 0.08f, float speed = 1f, float startAt = 0f)
        {
            var anim = Animator;
            if (anim == null || !HasState(anim, state)) return;
            anim.SetFloat(ActionSpeedId, speed);
            anim.CrossFadeInFixedTime(state, fade, 0, startAt * ClipLength(state));
        }

        /// <summary>Toca uma ação inteira em exatamente "seconds" segundos.</summary>
        public void PlayFitted(string state, float seconds, float fade = 0.08f)
        {
            float length = ClipLength(state);
            if (length <= 0f || seconds <= 0f) { Play(state, fade); return; }
            Play(state, fade, Mathf.Clamp(length / seconds, 0.2f, 4f));
        }

        /// <summary>
        /// Toca uma ação de forma que um momento dela (ex.: o impacto, a 42% do clipe)
        /// aconteça daqui a "seconds" segundos.
        /// </summary>
        public void PlayToMoment(string state, float moment, float seconds, float startAt = 0f, float fade = 0.08f)
        {
            float length = ClipLength(state);
            if (length <= 0f || seconds <= 0f) { Play(state, fade, 1f, startAt); return; }
            float speed = Mathf.Clamp((moment - startAt) * length / seconds, 0.2f, 4f);
            Play(state, fade, speed, startAt);
        }

        /// <summary>Muda a velocidade da ação que está tocando (0 = segura a pose).</summary>
        public void SetActionSpeed(float speed)
        {
            var anim = Animator;
            if (anim != null) anim.SetFloat(ActionSpeedId, speed);
        }

        /// <summary>Em que ponto (0 a 1) está a ação atual, se for este estado.</summary>
        public float NormalizedTime(string state)
        {
            var anim = Animator;
            if (anim == null) return 0f;
            var info = anim.GetCurrentAnimatorStateInfo(0);
            return info.IsName(state) ? info.normalizedTime : 0f;
        }

        public void ReturnToLocomotion(float fade = 0.15f)
        {
            var anim = Animator;
            if (anim == null) return;
            anim.SetFloat(ActionSpeedId, 1f);
            anim.CrossFadeInFixedTime(locomotionState, fade, 0);
        }

        public void Freeze(float seconds)
        {
            var anim = Animator;
            if (anim == null) return;
            anim.speed = 0f;
            frozen = true;
            freezeUntil = Time.time + seconds;
        }

        public float ClipLength(string clipName)
        {
            var anim = Animator;
            if (anim == null || anim.runtimeAnimatorController == null) return 0f;

            if (cachedController != anim.runtimeAnimatorController)
            {
                cachedController = anim.runtimeAnimatorController;
                clipLengths.Clear();
                foreach (var clip in cachedController.animationClips)
                    if (clip != null) clipLengths[clip.name] = clip.length;
            }

            return clipLengths.TryGetValue(clipName, out float length) ? length : 0f;
        }

        private static bool HasState(Animator anim, string state)
        {
            if (anim.HasState(0, Animator.StringToHash(state)) ||
                anim.HasState(0, Animator.StringToHash("Base Layer." + state))) return true;
            Debug.LogWarning($"[Animação] Estado '{state}' não existe no Animator de {anim.name}.");
            return false;
        }
    }
}
