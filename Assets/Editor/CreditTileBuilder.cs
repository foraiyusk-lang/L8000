using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace L8000.EditorTools
{
    /// <summary>
    /// credits.txt から「単語 / 名前」タイル(CreditTile)を生成し、セクションごとに
    /// パターン(階段 / ジグザグ / 島 / 封鎖ゲート)を割り当ててレベルを組む。
    ///
    /// 封鎖ゲート = 全幅の天井 + 中央の狭い足場。上はすべて足場なので、担当ロールの
    /// タイルをビームで撃ち抜いて穴を開けないと上へ行けない(= 壊すと歪みが発生する)。
    ///
    /// 生成後はシーンビューで各タイルを微調整して良い(コライダは追従)。
    /// </summary>
    public static class CreditTileBuilder
    {
        const string CreditsPath   = "Assets/Credits/credits.txt";
        const string FontAssetPath = "Assets/Fonts/MaruMonica.asset";
        const string ScenePath     = "Assets/Scenes/Main.unity";
        const string RootName      = "CreditTiles";

        const float HalfW      = 7.2f;
        const float StepUp     = 1.25f;   // 1行の高さ送り
        const float StepAcross = 2.6f;    // 階段1段の横送り
        const float SectionGap = 1.8f;
        const float WordGap    = 0.16f;   // 行内の単語どうしの隙間

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
            if (font == null || txt == null) { Debug.LogError("[CreditTiles] font/credits.txt not found"); return; }
            var layout = AssetDatabase.LoadAssetAtPath<GameLayout>("Assets/Settings/GameLayout.asset");

            var rootGo = GameObject.Find(RootName) ?? new GameObject(RootName);
            int made = Populate(rootGo.transform, font, txt.text, layout);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[CreditTiles] built {made} tiles. 封鎖ゲートで sabotage タイルを撃たせる設計。");
        }

        // ---- 破壊副作用の判定(SabotageDirector と対応)----
        enum Sabo { None, Bgm, Sfx, Physics, Noise, Garble, Ui, Light, Frame, Writing, Producer }
        static Sabo Detect(string sourceLine)
        {
            if (string.IsNullOrEmpty(sourceLine)) return Sabo.None;
            int sep = sourceLine.IndexOf("::");
            string role = (sep >= 0 ? sourceLine.Substring(0, sep) : sourceLine).Trim();
            string r = role.ToLowerInvariant();
            if (r.Contains("music") || r.Contains("composer")) return Sabo.Bgm;
            if (r.Contains("sound director")) return Sabo.Sfx;
            if (r.Contains("physics programmer")) return Sabo.Physics;
            if (r.Contains("graphics programmer") || r.Contains("rendering")) return Sabo.Noise;
            if (r.Contains("localization")) return Sabo.Garble;
            if (r.StartsWith("ui ") || r.Contains("ui /") || r.Contains("ux")) return Sabo.Ui;
            if (r.Contains("lighting")) return Sabo.Light;
            if (r.Contains("cinematics") || r.Contains("camera")) return Sabo.Frame;
            if (r.Contains("writer") || r.Contains("script editor")) return Sabo.Writing;
            if (role == "Producer") return Sabo.Producer;
            return Sabo.None;
        }

        class Tile { public CreditTile c; public float w; }
        class Line
        {
            public List<Tile> tiles = new List<Tile>();
            public bool isHeader, isTitle, isRule;
            public Sabo sabo;
            public Tile saboTile;   // 撃つと歪みが出るタイル(役割の単語を優先)
            public float Width => tiles.Sum(t => t.w) + WordGap * Mathf.Max(0, tiles.Count - 1);
        }

        // ---- 生成 ----
        public static int Populate(Transform root, TMP_FontAsset font, string creditsText, GameLayout layout)
        {
            root.position = Vector3.zero;
            var roll = CreditsMarkup.Parse(creditsText, layout);
            foreach (var t in root.GetComponentsInChildren<CreditTile>(true)) Object.DestroyImmediate(t.gameObject);
            var parent = root;

            var specs = roll.Lines.ToList();
            specs.Reverse();   // 登り順(下=最下部)

            // セクション分割: 中身が先、その上に見出し。見出しに当たったらセクション確定。
            var sections = new List<List<LineSpec>>();
            var cur = new List<LineSpec>();
            foreach (var spec in specs)
            {
                cur.Add(spec);
                if (spec.Kind == LineKind.Header)
                {
                    var hdr = cur[cur.Count - 1];
                    cur.RemoveAt(cur.Count - 1);
                    cur.Insert(0, hdr);           // 見出しを入口へ
                    sections.Add(cur);
                    cur = new List<LineSpec>();
                }
            }
            if (cur.Count > 0) sections.Add(cur);

            int made = 0;
            float cy = 0f;
            int patternIdx = 0;

            for (int si = 0; si < sections.Count; si++)
            {
                var secLines = new List<Line>();
                foreach (var spec in sections[si])
                {
                    string src = spec.Right != null ? $"{spec.Left} :: {spec.Right}" : (spec.Left ?? "");
                    var sabo = Detect(src);
                    var line = new Line { sabo = sabo, isHeader = spec.Kind == LineKind.Header, isTitle = spec.Kind == LineKind.Title, isRule = spec.Kind == LineKind.Rule };

                    if (spec.Kind == LineKind.Rule)
                    {
                        var rc = MakeTile(parent, font, new string('=', 54), false, false, true, 1f, src, wide: true);
                        line.tiles.Add(new Tile { c = rc, w = 14f });
                        made++;
                        secLines.Add(line);
                        continue;
                    }

                    float scale = spec.Kind == LineKind.Title ? (layout != null ? layout.titleScale : 1.7f)
                                : spec.Kind == LineKind.Header ? (layout != null ? layout.headerScale : 1.35f) : 1f;

                    var units = new List<(string s, bool name)>();
                    if (spec.Kind == LineKind.RoleName)
                    {
                        foreach (var w in SplitWords(spec.Left)) units.Add((w, false));
                        if (!string.IsNullOrWhiteSpace(spec.Right)) units.Add((spec.Right.Trim(), true));
                    }
                    else foreach (var w in SplitWords(spec.Left)) units.Add((w, false));

                    foreach (var u in units)
                    {
                        bool isHdr = spec.Kind == LineKind.Header && !u.name;
                        var c = MakeTile(parent, font, u.s, u.name, isHdr, false, scale, src, wide: false);
                        c.name = $"Tile[{(u.name ? "Name" : spec.Kind.ToString())}] {Trim(u.s)}";
                        var tile = new Tile { c = c, w = EstWidth(u.s) * scale };
                        line.tiles.Add(tile);
                        if (sabo != Sabo.None && line.saboTile == null && !u.name) line.saboTile = tile;
                        made++;
                    }
                    if (sabo != Sabo.None && line.saboTile == null && line.tiles.Count > 0) line.saboTile = line.tiles[0];
                    if (line.tiles.Count > 0) secLines.Add(line);
                }
                if (secLines.Count == 0) continue;

                var gate = secLines.FirstOrDefault(l => l.saboTile != null && !l.isHeader && GateSection(l.sabo));

                if (si == 0) cy = LayOnRamp(secLines, cy);
                else if (gate != null) cy = LaySealedGate(secLines, gate, cy);
                else
                {
                    // 階段中心 + 時々 島。ジグザグは封鎖ゲート内でだけ使う(広い行を扱えないため)。
                    switch (patternIdx % 5)
                    {
                        case 0: cy = LayStaircase(secLines, cy, +1); break;
                        case 1: cy = LayStaircase(secLines, cy, -1); break;
                        case 2: cy = LayIslands(secLines, cy); break;
                        case 3: cy = LayStaircase(secLines, cy, +1); break;
                        default: cy = LayStaircase(secLines, cy, -1); break;
                    }
                    patternIdx++;
                }
                cy += SectionGap;
            }
            return made;
        }

        static bool GateSection(Sabo s)
        {
            switch (s)
            {
                case Sabo.Producer: case Sabo.Frame: case Sabo.Garble: case Sabo.Bgm:
                case Sabo.Sfx: case Sabo.Light: case Sabo.Physics: case Sabo.Noise:
                    return true;
                default: return false;   // Ui / Writing は必須にしない
            }
        }

        // ---- パターン(1行 = 1つの横並びプラットフォーム)----

        static void PlaceLine(Line line, float centerX, float y)
        {
            float total = line.Width;
            // 行全体が play 域 [-HalfW, HalfW] に収まるよう中心を寄せる
            float lim = Mathf.Max(0f, HalfW - total * 0.5f);
            centerX = Mathf.Clamp(centerX, -lim, lim);
            float x = centerX - total * 0.5f;
            foreach (var t in line.tiles)
            {
                t.c.transform.position = new Vector3(x + t.w * 0.5f, y, 0f);
                x += t.w + WordGap;
            }
        }

        static float LayOnRamp(List<Line> lines, float cy)
        {
            float y = cy;
            for (int i = 0; i < lines.Count; i++)
            {
                float x = lines[i].isRule ? 0f : ((i % 3) - 1) * 2.6f;
                PlaceLine(lines[i], x, y);
                y += lines[i].isRule ? 1.5f : 1.7f;
            }
            return y;
        }

        static float LayStaircase(List<Line> lines, float cy, int dir)
        {
            float x = dir > 0 ? -HalfW + 1.5f : HalfW - 1.5f;
            float y = cy;
            int d = dir;
            foreach (var l in lines)
            {
                if (l.isHeader) { PlaceLine(l, 0f, y); y += StepUp + 0.3f; continue; }
                PlaceLine(l, x, y);
                x += d * StepAcross;
                y += StepUp;
                if (x > HalfW - 1f) { d = -1; x = HalfW - 1f; y += 0.3f; }
                else if (x < -HalfW + 1f) { d = 1; x = -HalfW + 1f; y += 0.3f; }
            }
            return y;
        }

        static float LayIslands(List<Line> lines, float cy)
        {
            var rng = new System.Random(lines.Count * 7 + 31);
            float y = cy, prevX = 0f;
            foreach (var l in lines)
            {
                if (l.isHeader) { PlaceLine(l, 0f, y); y += StepUp + 0.3f; continue; }
                float dx = (float)(rng.NextDouble() * 6.0 - 3.0);
                float x = Mathf.Clamp(prevX + dx, -HalfW + 1.5f, HalfW - 1.5f);
                PlaceLine(l, x, y);
                prevX = x;
                y += 1.45f + (float)rng.NextDouble() * 0.5f;
            }
            return y;
        }

        static float LaySealedGate(List<Line> lines, Line gate, float cy)
        {
            int gi = lines.IndexOf(gate);
            // 天井を組むタイルを集める: ゲート行 + その前後の行から全単語を借りる
            var ceilTiles = new List<Tile>();
            var used = new HashSet<Line>();
            void Borrow(Line l) { if (l != null && !used.Contains(l)) { used.Add(l); ceilTiles.AddRange(l.tiles.Where(t => t != gate.saboTile)); } }
            Borrow(gate);
            for (int k = 1; k <= 8 && ceilTiles.Sum(t => t.w) < HalfW * 3f; k++)
            {
                if (gi + k < lines.Count) Borrow(lines[gi + k]);
                if (gi - k >= 1) Borrow(lines[gi - k]);   // 見出し(index 0)は使わない
            }

            float y = cy;
            // 1) 下: 見出し + 借りていない前の行を階段で
            int side = 1;
            for (int i = 0; i < gi; i++)
            {
                var l = lines[i];
                if (used.Contains(l)) continue;
                if (l.isHeader) { PlaceLine(l, 0f, y); y += StepUp + 0.2f; continue; }
                float x = side * (1.1f + l.Width * 0.5f);
                PlaceLine(l, x, y);
                side = -side;
                y += StepUp;
            }
            // 2) 中央の「撃ち上げ台」(x=0 に必ず1枚。連続で塞ぐため隣にもう1枚)
            y += 0.6f;
            float standY = y;
            if (ceilTiles.Count > 3)
            {
                var s0 = ceilTiles[0]; ceilTiles.RemoveAt(0);
                s0.c.transform.position = new Vector3(0f, standY, 0f);   // ど真ん中
                if (ceilTiles.Count > 3)
                {
                    var s1 = ceilTiles[0]; ceilTiles.RemoveAt(0);
                    s1.c.transform.position = new Vector3(s0.w * 0.5f + s1.w * 0.5f, standY, 0f);
                }
            }
            // 3) 天井: saboTile を x=0、borrowed タイルで左右対称に全幅(±(HalfW+1))を埋める
            float ceilY = standY + 2.35f;
            gate.saboTile.c.transform.position = new Vector3(0f, ceilY, 0f);
            float lx = -gate.saboTile.w * 0.5f - WordGap;
            float rx = gate.saboTile.w * 0.5f + WordGap;
            int ti = 0;
            while (ti < ceilTiles.Count && (lx > -(HalfW + 1f) || rx < HalfW + 1f))
            {
                var t = ceilTiles[ti++];
                if (lx > -(HalfW + 1f) && (lx * -1f <= rx || rx >= HalfW + 1f))
                { t.c.transform.position = new Vector3(lx - t.w * 0.5f, ceilY, 0f); lx -= t.w + WordGap; }
                else
                { t.c.transform.position = new Vector3(rx + t.w * 0.5f, ceilY, 0f); rx += t.w + WordGap; }
            }
            // 余ったタイルは天井の上の階段へ回す(以降で使う)
            var leftover = ceilTiles.Skip(ti).ToList();

            // 4) 天井の上: 着地足場(余りタイル)+ 残りの行を階段で
            y = ceilY + 1.6f;
            if (leftover.Count > 0)
            {
                float lw = leftover.Sum(t => t.w) + WordGap * Mathf.Max(0, leftover.Count - 1);
                float ix = Mathf.Max(-2.5f, -lw * 0.5f);
                foreach (var t in leftover) { t.c.transform.position = new Vector3(ix + t.w * 0.5f, y, 0f); ix += t.w + WordGap; }
                y += StepUp;
            }
            side = -1;
            for (int i = gi + 1; i < lines.Count; i++)
            {
                var l = lines[i];
                if (used.Contains(l)) continue;
                if (l.isHeader) { PlaceLine(l, 0f, y); y += StepUp + 0.2f; continue; }
                float x = side * (1.1f + l.Width * 0.5f);
                PlaceLine(l, x, y);
                side = -side;
                y += StepUp;
            }
            return y + 0.4f;
        }

        // ---- 生成ヘルパ ----
        static CreditTile MakeTile(Transform parent, TMP_FontAsset font, string text,
            bool isName, bool isHeader, bool isRule, float scale, string sourceLine, bool wide)
        {
            var go = new GameObject("Tile", typeof(BoxCollider2D), typeof(CreditTile));
            go.transform.SetParent(parent, false);
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
            tile.thinTop = !wide;
            tile.Rebuild(force: true);
            return tile;
        }

        static IEnumerable<string> SplitWords(string s)
            => (s ?? "").Split(new[] { ' ', '　', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

        static float EstWidth(string s)
        {
            float w = 0f;
            foreach (char c in s) w += GameLayout.IsWide(c) ? 0.72f : 0.36f;
            return Mathf.Max(0.35f, w);
        }

        static string Trim(string s) => s.Length > 16 ? s.Substring(0, 16) : s;
    }
}
