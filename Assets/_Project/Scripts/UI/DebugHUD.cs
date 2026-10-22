using OCoveiro.AI;
using OCoveiro.Combat;
using OCoveiro.Player;
using UnityEngine;

namespace OCoveiro.UI
{
    /// <summary>
    /// HUD PROVISÓRIO (OnGUI) para testar o combate na semana 2.
    /// Na semana 4 é substituído pelo HUD de verdade (uGUI), que escuta os GameEvents.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        private static readonly Color HealthColor = new Color(0.75f, 0.16f, 0.14f);
        private static readonly Color StaminaColor = new Color(0.36f, 0.62f, 0.28f);
        private static readonly Color OilColor = new Color(0.95f, 0.62f, 0.2f);
        private static readonly Color BackColor = new Color(0f, 0f, 0f, 0.55f);

        [SerializeField] private bool showControls = true;

        private void OnGUI()
        {
            PlayerAppearance local = null;
            PlayerAppearance partner = null;
            foreach (var player in PlayerAppearance.All)
            {
                if (player == null) continue;
                if (player.IsOwner) local = player;
                else partner = player;
            }

            if (showControls) DrawControls();
            DrawEnemyBars();
            if (local == null) return;

            var health = local.GetComponent<Health>();
            var stamina = local.GetComponent<PlayerStamina>();
            var lantern = local.GetComponent<Lantern>();
            var combat = local.GetComponent<PlayerCombat>();
            var life = local.GetComponent<PlayerLife>();

            float x = 16f;
            float y = Screen.height - 128f;
            const float width = 240f;

            if (health != null) DrawBar(x, y, width, health.Normalized, HealthColor, $"Vida {health.Current:0}/{health.Max:0}");
            if (stamina != null) DrawBar(x, y + 26f, width, stamina.Normalized, StaminaColor, $"Stamina {stamina.Current:0}");
            if (lantern != null) DrawBar(x, y + 52f, width, lantern.NormalizedOil, OilColor, $"Óleo {lantern.Oil:0}{(lantern.IsOn ? "  (acesa)" : "")}");

            string status = combat != null ? $"Frascos {combat.Flasks}/{combat.MaxFlasks}    Estado: {combat.State}" : string.Empty;
            if (life != null && life.IsDowned) status = "CAÍDO — levantando...";
            GUI.Label(new Rect(x, y + 78f, 400f, 22f), $"{local.DisplayName}   {status}");

            if (partner != null)
            {
                var partnerHealth = partner.GetComponent<Health>();
                var partnerLife = partner.GetComponent<PlayerLife>();
                if (partnerHealth != null)
                {
                    string label = $"{partner.DisplayName} {partnerHealth.Current:0}/{partnerHealth.Max:0}";
                    if (partnerLife != null && partnerLife.IsDowned) label += " (caído)";
                    DrawBar(x + width + 24f, y, 180f, partnerHealth.Normalized, HealthColor, label);
                }
            }
        }

        /// <summary>Barrinha sobre a cabeça dos inimigos feridos, e "!" quando o Vigia grita.</summary>
        private static void DrawEnemyBars()
        {
            var cam = Camera.main;
            if (cam == null) return;

            foreach (var enemy in EnemyBrain.All)
            {
                if (enemy == null || enemy.Data == null || enemy.StateId == EnemyStateId.Dead) continue;
                if (enemy.Data.spectral && !Lantern.IsLit(enemy.transform.position)) continue;

                Vector3 screen = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.6f);
                if (screen.z <= 0f) continue;
                float x = screen.x - 30f;
                float y = Screen.height - screen.y;

                if (enemy.StateId == EnemyStateId.Alert)
                    GUI.Label(new Rect(screen.x - 4f, y - 22f, 20f, 20f), "!");

                var health = enemy.Health;
                if (health == null || health.Current >= health.Max) continue;

                var previous = GUI.color;
                GUI.color = BackColor;
                GUI.DrawTexture(new Rect(x, y, 60f, 7f), Texture2D.whiteTexture);
                GUI.color = HealthColor;
                GUI.DrawTexture(new Rect(x + 1f, y + 1f, 58f * health.Normalized, 5f), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }

        private static void DrawBar(float x, float y, float width, float fill, Color color, string label)
        {
            var previous = GUI.color;
            GUI.color = BackColor;
            GUI.DrawTexture(new Rect(x, y, width, 20f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x + 2f, y + 2f, (width - 4f) * Mathf.Clamp01(fill), 16f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(x + 6f, y + 1f, width, 20f), label);
        }

        private static void DrawControls()
        {
            const string help =
                "WASD andar · Mouse mirar\n" +
                "Clique: golpe (combo de 3)\n" +
                "Botão direito: segurar = pesado\n" +
                "Espaço: esquiva · F: lanterna · Q: frasco\n" +
                "Teste: K = levar 25 de dano · O = recarregar tudo";
            var rect = new Rect(Screen.width - 316f, 16f, 300f, 92f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, rect.height), help);
        }
    }
}
