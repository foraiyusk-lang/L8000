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

        void OnEnable()
        {
            TileField.OnTileDestroyed += OnTileDestroyed;
            TileField.OnTileRestored += OnTileRestored;
        }
        void OnDisable()
        {
            TileField.OnTileDestroyed -= OnTileDestroyed;
            TileField.OnTileRestored -= OnTileRestored;
        }

        static string RoleOf(CreditTile tile)
        {
            string src = tile != null ? (tile.sourceLine ?? "") : "";
            int sep = src.IndexOf("::");
            return (sep >= 0 ? src.Substring(0, sep) : src).Trim();
        }

        int _noiseFrame;

        void Update()
        {
            // 砂嵐は毎フレームではなく数フレームおきに動かす(専用 Canvas でも更新は軽く)
            if (graphicsNoise != null && graphicsNoise.enabled && (++_noiseFrame & 1) == 0)
            {
                var r = graphicsNoise.uvRect;
                r.x = Random.value; r.y = Random.value;
                graphicsNoise.uvRect = r;
            }
        }

        void OnTileDestroyed(CreditTile tile, string chars)
        {
            if (tile == null) return;
            // その行のどこか(役職語でも名前でも)を壊せば、その担当の歪みが発動する。
            string role = RoleOf(tile);
            string r = role.ToLowerInvariant();
            if (string.IsNullOrEmpty(r)) return;

            if (r.Contains("music") || r.Contains("composer")) Fire("bgm", "音が 歪む", () => AudioManager.Instance?.DistortBgm(), "");
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

        void Fire(string key, string label, System.Action effect, string verb = "消失")
        {
            if (!_fired.Add(key)) return;
            effect?.Invoke();
            AudioManager.Play(Sfx.Break);
            string msg = string.IsNullOrEmpty(verb) ? $"― {label} ―" : $"― {label} {verb} ―";
            ScreenFx.Toast(msg, new Color(0.95f, 0.45f, 0.4f), 2.2f);
            ScreenFx.Flash(new Color(0.6f, 0.05f, 0.05f, 0.32f), 0.5f);
            Debug.Log($"[Sabotage] {key}");
        }

        // --- 降りで通過した所の機能を戻す(FB11)---------------------------
        void OnTileRestored(CreditTile tile, string chars)
        {
            if (tile == null) return;
            string r = RoleOf(tile).ToLowerInvariant();
            string role = RoleOf(tile);
            if (string.IsNullOrEmpty(r)) return;

            if (r.Contains("music") || r.Contains("composer")) Unfire("bgm", "音", () => AudioManager.Instance?.RestoreBgm());
            if (r.Contains("sound director")) Unfire("sfx", "効果音", () => AudioManager.Instance?.ReviveSfx());
            if (r.Contains("physics programmer")) Unfire("physics", "物理", () => PlayerController.PhysicsAnomaly = false);
            if (r.Contains("graphics programmer") || r.Contains("rendering")) Unfire("noise", "描画", () => { if (graphicsNoise != null) graphicsNoise.enabled = false; });
            if (r.Contains("localization")) Unfire("garble", "翻訳", () => { TileField.Garble = false; TileField.Instance?.RefreshAll(); });
            if (r.StartsWith("ui ") || r.Contains("ui /") || r.Contains("ux")) Unfire("ui", "UI", () => hud?.Revive());
            if (r.Contains("lighting")) Unfire("light", "照明", () => ScreenFx.Vignette(0f, 1.5f));
            if (r.Contains("cinematics") || r.Contains("camera")) Unfire("frame", "画面の水平", () => CameraFollow.FrameBroken = false);
            if (r.Contains("writer") || r.Contains("script editor")) Unfire("writing", "言葉", () => Finale.WritingBroken = false);
            if (role == "Producer") Unfire("producer", "進行管理", () => { if (GameState.Instance != null) GameState.Instance.spendMultiplier = 1f; });
        }

        void Unfire(string key, string label, System.Action revert)
        {
            if (!_fired.Remove(key)) return;
            revert?.Invoke();
            ScreenFx.Toast($"― {label} もどる ―", new Color(0.68f, 0.82f, 0.96f), 1.8f);
            AudioManager.Play(Sfx.Shoot, 0.35f);
            Debug.Log($"[Sabotage] {key} restored");
        }

        void ShowNoise()
        {
            if (graphicsNoise != null) graphicsNoise.enabled = true;
        }
    }
}
