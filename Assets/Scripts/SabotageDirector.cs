using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace L8000
{
    /// <summary>
    /// 弾でクレジット(担当者)を壊すと、その担当機能が不可逆に壊れる。
    /// CreditLineBuilder.OnCharsDestroyed を購読し、行の役割名で効果を振り分ける。
    /// </summary>
    public class SabotageDirector : MonoBehaviour
    {
        public Hud hud;
        public RawImage graphicsNoise;   // Graphics 破壊で表示する砂嵐オーバーレイ

        readonly HashSet<string> _fired = new HashSet<string>();

        void Awake()
        {
            if (graphicsNoise != null && graphicsNoise.texture == null)
            {
                var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                var px = new Color32[96 * 96];
                var rng = new System.Random(7);
                for (int i = 0; i < px.Length; i++)
                {
                    byte v = (byte)rng.Next(0, 256);
                    px[i] = new Color32(v, v, v, 255);
                }
                tex.SetPixels32(px);
                tex.Apply();
                graphicsNoise.texture = tex;
                graphicsNoise.color = new Color(1f, 1f, 1f, 0.13f);
            }
        }

        void OnEnable() => CreditLineBuilder.OnCharsDestroyed += OnDestroyed;
        void OnDisable() => CreditLineBuilder.OnCharsDestroyed -= OnDestroyed;

        void Update()
        {
            if (graphicsNoise != null && graphicsNoise.enabled)
            {
                var r = graphicsNoise.uvRect;
                r.x = Random.value; r.y = Random.value;
                graphicsNoise.uvRect = r;
            }
        }

        void OnDestroyed(CreditLineBuilder line, string chars)
        {
            if (line == null || line.Spec == null) return;
            string role = (line.Spec.Left ?? "").Trim();
            string r = role.ToLowerInvariant();

            if (r.Contains("music") || r.Contains("composer")) Fire("bgm", () => AudioManager.Instance?.StopBgm());
            if (r.Contains("sound director")) Fire("sfx", () => AudioManager.Instance?.KillSfx());
            if (r.Contains("physics programmer")) Fire("physics", () => PlayerController.PhysicsAnomaly = true);
            if (r.Contains("graphics programmer") || r.Contains("rendering")) Fire("noise", ShowNoise);
            if (r.Contains("localization")) Fire("garble", () => CreditLineBuilder.Garble = true);
            if (r.StartsWith("ui ") || r.Contains("ui /") || r.Contains("ux")) Fire("ui", () => hud?.Kill());
            if (role == "Producer") Fire("producer", () =>
            {
                if (GameState.Instance != null) GameState.Instance.spendMultiplier = 1.5f;
            });
        }

        void Fire(string key, System.Action effect)
        {
            if (!_fired.Add(key)) return;
            effect?.Invoke();
            Debug.Log($"[Sabotage] {key} 破壊");
        }

        void ShowNoise()
        {
            if (graphicsNoise != null) graphicsNoise.enabled = true;
        }
    }
}
