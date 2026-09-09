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

        void OnEnable() => TileField.OnTileDestroyed += OnTileDestroyed;
        void OnDisable() => TileField.OnTileDestroyed -= OnTileDestroyed;

        void Update()
        {
            if (graphicsNoise != null && graphicsNoise.enabled)
            {
                var r = graphicsNoise.uvRect;
                r.x = Random.value; r.y = Random.value;
                graphicsNoise.uvRect = r;
            }
        }

        void OnTileDestroyed(CreditTile tile, string chars)
        {
            if (tile == null) return;
            // 歪みが出るのは「担当ロール名」を撃ったときだけ。人の名前(:: の後)や
            // 封鎖ゲートの足場に流用された名前タイルでは発動しない。
            if (tile.isName) return;
            // タイルの元になった行(役割名)で判定。役割部分だけ見る(":: 名前" の前)。
            string src = tile.sourceLine ?? "";
            int sep = src.IndexOf("::");
            string role = (sep >= 0 ? src.Substring(0, sep) : src).Trim();
            string r = role.ToLowerInvariant();
            if (string.IsNullOrEmpty(r)) return;

            if (r.Contains("music") || r.Contains("composer")) Fire("bgm", "BGM", () => AudioManager.Instance?.StopBgm());
            if (r.Contains("sound director")) Fire("sfx", "効果音", () => AudioManager.Instance?.KillSfx());
            if (r.Contains("physics programmer")) Fire("physics", "物理", () => PlayerController.PhysicsAnomaly = true);
            if (r.Contains("graphics programmer") || r.Contains("rendering")) Fire("noise", "描画", ShowNoise);
            if (r.Contains("localization")) Fire("garble", "翻訳", () => { TileField.Garble = true; TileField.Instance?.RefreshAll(); });
            if (r.StartsWith("ui ") || r.Contains("ui /") || r.Contains("ux")) Fire("ui", "UI", () => hud?.Kill());
            if (r.Contains("lighting")) Fire("light", "照明", () => ScreenFx.Vignette(0.4f, 2f));
            if (r.Contains("cinematics") || r.Contains("camera")) Fire("frame", "画面の水平", () => CameraFollow.FrameBroken = true);
            if (r.Contains("writer") || r.Contains("script editor")) Fire("writing", "言葉", () => Finale.WritingBroken = true);
            if (role == "Producer") Fire("producer", "進行管理", () =>
            {
                if (GameState.Instance != null) GameState.Instance.spendMultiplier = 1.5f;
            });
        }

        void Fire(string key, string label, System.Action effect)
        {
            if (!_fired.Add(key)) return;
            effect?.Invoke();
            AudioManager.Play(Sfx.Break);
            ScreenFx.Toast($"― {label} 消失 ―", new Color(0.95f, 0.45f, 0.4f), 2.2f);
            ScreenFx.Flash(new Color(0.6f, 0.05f, 0.05f, 0.32f), 0.5f);
            Debug.Log($"[Sabotage] {key}");
        }

        void ShowNoise()
        {
            if (graphicsNoise != null) graphicsNoise.enabled = true;
        }
    }
}
