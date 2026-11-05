using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace MobThreat
{
    [DefaultExecutionOrder(10000)]
    [BepInPlugin("local.mobthreat", "MobThreat", "1.0.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private sealed class Label
        {
            internal Character Mob;
            internal int Hits = -1;
            internal TextMeshProUGUI Text;
            internal string Caption;
            internal Vector2 Screen;
            internal bool Visible;

            internal void Set(Character mob, int hits)
            {
                Mob = mob;
                if (Hits == hits) return;
                Hits = hits;
                Caption = hits == 1 ? "~1 hit" : "~" + hits + " hits";
                if (Text) Text.text = Caption;
            }
        }

        private readonly List<Character> nearby = new List<Character>();
        private readonly List<Label> labels = new List<Label>();
        private readonly ThreatEstimator estimator = new ThreatEstimator();
        private ConfigEntry<bool> displayEnabled;
        private ConfigEntry<float> distance;
        private GameObject overlay;
        private TMP_FontAsset font;
        private float nextSample;
        private int active;
        private int wallMask;

        private void Awake()
        {
            displayEnabled = Config.Bind("Display", "Enabled", true, "Show estimated unblocked hits to death above hostile mobs.");
            distance = Config.Bind("Display", "MaxDistance", 35f,
                new ConfigDescription("Maximum label distance in metres.", new AcceptableValueRange<float>(5f, 100f)));
            wallMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
        }

        private bool CanDisplay()
        {
            var player = Player.m_localPlayer;
            return displayEnabled != null && displayEnabled.Value && player && !player.IsDead() &&
                !player.IsTeleporting() && !player.InCutscene() && Game.instance && !Hud.IsUserHidden();
        }

        private void LateUpdate()
        {
            if (!CanDisplay())
            {
                Clear();
                nextSample = 0f;
                return;
            }
            var player = Player.m_localPlayer;
            if (Time.unscaledTime >= nextSample)
            {
                nextSample = Time.unscaledTime + .25f;
                Sample(player);
            }
            var camera = Utils.GetMainCamera();
            for (int i = 0; i < active; ++i)
            {
                var label = labels[i];
                label.Visible = false;
                if (!camera || !label.Mob || label.Mob.IsDead() || label.Mob.m_hideHud) continue;
                Vector3 point = label.Mob.GetTopPoint() + Vector3.up * .45f;
                Vector3 projected = camera.WorldToScreenPointScaled(point);
                if (projected.z <= 0f || projected.x < 0f || projected.x > Screen.width ||
                    projected.y < 0f || projected.y > Screen.height) continue;
                if (Physics.Linecast(camera.transform.position, point, wallMask, QueryTriggerInteraction.Ignore)) continue;
                label.Screen = new Vector2(projected.x, projected.y);
                label.Visible = true;
            }
            DrawLabels();
        }

        private void Sample(Player player)
        {
            nearby.Clear();
            Character.GetCharactersInRange(player.transform.position, distance.Value, nearby);
            var resistance = player.GetDamageModifiers();
            float armor = player.GetBodyArmor();
            float difficulty = Game.instance.GetDifficultyDamageScalePlayer(player.transform.position);
            int count = 0;
            for (int i = 0; i < nearby.Count; ++i)
            {
                var mob = nearby[i];
                if (!mob || mob.IsPlayer() || mob.IsDead() || mob.m_hideHud ||
                    !mob.GetBaseAI() || !BaseAI.IsEnemy(player, mob)) continue;
                int hits = mob is Humanoid humanoid
                    ? ThreatMath.HitsToDie(player.GetHealth(), estimator.Estimate(humanoid, resistance, armor, difficulty))
                    : 0;
                if (hits <= 0) continue;
                if (count == labels.Count) labels.Add(new Label());
                labels[count++].Set(mob, hits);
            }
            for (int i = count; i < active; ++i)
            {
                labels[i].Mob = null;
                labels[i].Visible = false;
                if (labels[i].Text) labels[i].Text.gameObject.SetActive(false);
            }
            active = count;
            nearby.Clear();
        }

        private void DrawLabels()
        {
            if (active == 0)
            {
                if (overlay) overlay.SetActive(false);
                return;
            }
            if (!overlay)
            {
                // Unity 6's IMGUI LegacyRuntime font is absent in native Valheim.
                // Reuse the game's SDF font instead, as other local overlays do.
                var template = EnemyHud.instance ? EnemyHud.instance.m_baseHud : null;
                var name = template ? template.transform.Find("Name") : null;
                var source = name ? name.GetComponent<TextMeshProUGUI>() : null;
                if (!source || !source.font) return;
                font = source.font;
                overlay = new GameObject("MobThreatOverlay", typeof(RectTransform), typeof(Canvas));
                overlay.transform.SetParent(transform, false);
                var canvas = overlay.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
            }
            overlay.SetActive(true);
            for (int i = 0; i < active; ++i)
            {
                var label = labels[i];
                if (!label.Text)
                {
                    var obj = new GameObject("Threat", typeof(RectTransform), typeof(TextMeshProUGUI));
                    obj.transform.SetParent(overlay.transform, false);
                    label.Text = obj.GetComponent<TextMeshProUGUI>();
                    label.Text.font = font;
                    label.Text.fontSharedMaterial = font.material;
                    label.Text.fontSize = 20f;
                    label.Text.fontStyle = FontStyles.Bold;
                    label.Text.alignment = TextAlignmentOptions.Center;
                    label.Text.raycastTarget = false;
                    label.Text.richText = false;
                    label.Text.text = label.Caption;
                    var rect = label.Text.rectTransform;
                    rect.anchorMin = rect.anchorMax = Vector2.zero;
                    rect.pivot = new Vector2(.5f, 0f);
                    rect.sizeDelta = new Vector2(180f, 32f);
                }
                bool visible = label.Visible && label.Mob && !label.Mob.IsDead();
                label.Text.gameObject.SetActive(visible);
                if (!visible) continue;
                label.Text.rectTransform.anchoredPosition = label.Screen;
                label.Text.color = label.Hits > 0 && label.Hits <= 2 ? Color.red : Color.white;
            }
        }

        private void Clear()
        {
            for (int i = 0; i < active; ++i)
            {
                labels[i].Mob = null;
                labels[i].Visible = false;
                if (labels[i].Text) labels[i].Text.gameObject.SetActive(false);
            }
            active = 0;
            nearby.Clear();
            if (overlay) overlay.SetActive(false);
        }

        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            Clear();
            if (overlay) Destroy(overlay);
        }
    }
}
