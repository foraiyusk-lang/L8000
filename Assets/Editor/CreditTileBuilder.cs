using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace L8000.EditorTools
{
    /// <summary>
    /// credits.txt から「単語 / 名前」タイル(CreditTile)を生成してシーンに置く。
    /// 生成後はシーンビューで各タイルを縦横に自由に動かして良い(コライダは追従する)。
    /// 再生成すると手で動かした位置は失われるので、レイアウトを詰める前に一度だけ回す想定。
    /// </summary>
    public static class CreditTileBuilder
    {
        const string CreditsPath   = "Assets/Credits/credits.txt";
        const string FontAssetPath = "Assets/Fonts/MaruMonica.asset";
        const string ScenePath     = "Assets/Scenes/Main.unity";
        const string RootName      = "CreditTiles";

        [MenuItem("L8000/Build Credit Tiles from credits.txt")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var txt  = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsPath);
            if (font == null || txt == null)
            {
                Debug.LogError($"[CreditTiles] font or credits.txt not found");
                return;
            }
            var layout = AssetDatabase.LoadAssetAtPath<GameLayout>("Assets/Settings/GameLayout.asset");

            var rootGo = GameObject.Find(RootName) ?? new GameObject(RootName);
            int made = Populate(rootGo.transform, font, txt.text, layout);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[CreditTiles] built {made} tiles under '{RootName}'. Move them freely in Scene View.");
        }

        /// <summary>ルート配下の CreditTile を作り直す。ProjectBootstrap からも呼ぶ。</summary>
        public static int Populate(Transform root, TMP_FontAsset font, string creditsText, GameLayout layout)
        {
            root.position = Vector3.zero;
            var roll = CreditsMarkup.Parse(creditsText, layout);

            var old = root.GetComponentsInChildren<CreditTile>(true);
            foreach (var t in old) Object.DestroyImmediate(t.gameObject);

            var rootGo = root.gameObject;
            int made = 0;

            // roll.Lines は WorldY 降順(先頭=タイトル、末尾=最下部)
            foreach (var spec in roll.Lines)
            {
                float y = spec.WorldY;

                string sourceLine = spec.Right != null ? $"{spec.Left} :: {spec.Right}" : (spec.Left ?? "");

                if (spec.Kind == LineKind.Rule)
                {
                    var t = MakeTile(rootGo.transform, font, new string('=', 54), y, isName: false, isHeader: false, isRule: true, scale: 1f, sourceLine, wide: true);
                    t.name = "Tile[Rule]";
                    t.transform.position = new Vector3(0f, y, 0f);
                    made++;
                    continue;
                }

                bool header = spec.Kind == LineKind.Header;
                float scale = spec.Kind == LineKind.Title ? (layout != null ? layout.titleScale : 1.7f)
                            : header ? (layout != null ? layout.headerScale : 1.35f)
                            : 1f;

                var units = new List<(string s, bool name)>();
                if (spec.Kind == LineKind.RoleName)
                {
                    foreach (var w in SplitWords(spec.Left)) units.Add((w, false));       // 役割は単語ごと
                    if (!string.IsNullOrWhiteSpace(spec.Right)) units.Add((spec.Right.Trim(), true));  // 名前(苗字＋名前)はセットで1つ
                }
                else   // Name / Header / Free / Title — すべて単語ごとに分ける
                {
                    foreach (var w in SplitWords(spec.Left)) units.Add((w, false));
                }

                // 既定レイアウト: その行を中央そろえで左→右に並べる(あとで手で動かす)
                float gap = 0.55f * scale;
                var widths = units.Select(u => EstWidth(u.s) * scale).ToArray();
                float total = widths.Sum() + gap * Mathf.Max(0, units.Count - 1);
                float cx = -total * 0.5f;
                for (int i = 0; i < units.Count; i++)
                {
                    float w = widths[i];
                    var t = MakeTile(rootGo.transform, font, units[i].s, y, units[i].name, header && !units[i].name, false, scale, sourceLine, wide: false);
                    t.transform.position = new Vector3(cx + w * 0.5f, y, 0f);
                    t.name = $"Tile[{(units[i].name ? "Name" : spec.Kind.ToString())}] {Trim(units[i].s)}";
                    cx += w + gap;
                    made++;
                }
            }
            return made;
        }

        static CreditTile MakeTile(Transform parent, TMP_FontAsset font, string text, float y, bool isName, bool isHeader, bool isRule, float scale, string sourceLine, bool wide)
        {
            var go = new GameObject("Tile", typeof(BoxCollider2D), typeof(CreditTile));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, y, 0f);
            int gl = LayerMask.NameToLayer("Ground");
            if (gl >= 0) go.layer = gl;

            var tile = go.GetComponent<CreditTile>();
            tile.SetFont(font);
            tile.text = text;
            tile.isName = isName;
            tile.isHeader = isHeader;
            tile.isRule = isRule;
            tile.sizeScale = scale;
            tile.sourceLine = sourceLine;
            tile.thinTop = !wide;      // ===== は厚い床
            tile.Rebuild(force: true);
            return tile;
        }

        static IEnumerable<string> SplitWords(string s)
            => (s ?? "").Split(new[] { ' ', '　', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

        // 大まかな幅(セル)。ASCII 0.36 / 全角 0.72。CreditTile.Rebuild が実測で上書きするので目安で良い。
        static float EstWidth(string s)
        {
            float w = 0f;
            foreach (char c in s) w += GameLayout.IsWide(c) ? 0.72f : 0.36f;
            return Mathf.Max(0.3f, w);
        }

        static string Trim(string s) => s.Length > 16 ? s.Substring(0, 16) : s;
    }
}
