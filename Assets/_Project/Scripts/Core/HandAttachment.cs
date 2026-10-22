using UnityEngine;

namespace OCoveiro.Core
{
    /// <summary>
    /// Prende um objeto (a pá, a lanterna, uma arma) num osso do modelo ativo, como "handslot.r".
    /// É feito em tempo de jogo porque o jogador tem dois modelos (Coveiro e Aprendiz) e só um fica ativo.
    /// Posição e rotação podem ser ajustadas no Inspector com o jogo rodando.
    /// </summary>
    public class HandAttachment : MonoBehaviour
    {
        [SerializeField] private Transform item;
        [SerializeField] private string boneName = "handslot.r";
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEuler;

        private Transform currentBone;
        private Animator currentAnimator;

        public Transform Item => item;

        private void LateUpdate()
        {
            if (item == null) return;

            var animator = FindActiveAnimator();
            if (animator == null) return;

            if (animator != currentAnimator || currentBone == null)
            {
                currentAnimator = animator;
                currentBone = FindDeep(animator.transform, boneName);
                if (currentBone == null) return;
                item.SetParent(currentBone, false);
            }

            // Reaplicado sempre, para o ajuste no Inspector aparecer na hora.
            item.localPosition = localPosition;
            item.localRotation = Quaternion.Euler(localEuler);
        }

        private Animator FindActiveAnimator()
        {
            if (currentAnimator != null && currentAnimator.isActiveAndEnabled) return currentAnimator;
            foreach (var animator in GetComponentsInChildren<Animator>(false))
                if (!animator.transform.IsChildOf(item)) return animator;
            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
