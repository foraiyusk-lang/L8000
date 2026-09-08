using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// クレジット1行を実体化する。プール対象。
    ///   - 文字は「1文字 = 1個のソリッドな当たり判定」。下からも横からも抜けられない
    ///   - 単語と単語の間、役割と名前の間、姓と名の間などの空白 = 隙間。プレイヤーはそこを縫って登る
    ///   - 弾「・」が当たった文字は消える(グリフも当たり判定も)。等幅グリッドなので
    ///     消しても他の文字の位置はずれない
    /// TMP と BoxCollider2D は使い回す(Destroy しない)。
    /// </summary>
    [DisallowMultipleComponent]
    public class CreditLineBuilder : MonoBehaviour
    {
        const float RefSize = 32f;

        GameLayout _layout;
        TMP_FontAsset _font;

        class Seg
        {
            public string original = "";
            public bool[] destroyed;
            public TextMeshPro tmp;
            public float scale;
            public float k;         // ref空間 → world のスケール
            public float originX;   // line 空間での左端(全文基準・不変)
            public readonly List<BoxCollider2D> boxes = new List<BoxCollider2D>();
            public readonly List<int> boxChar = new List<int>();   // boxes[i] が対応する文字index
            public int usedBoxes;
            public bool built;
        }

        readonly List<Seg> _segs = new List<Seg>();
        int _usedSegs;

        Rigidbody2D _body;
        CompositeCollider2D _composite;

        public LineSpec Spec { get; private set; }
        public bool Active { get; private set; }

        public void Init(GameLayout layout, TMP_FontAsset font, int groundLayer)
        {
            _layout = layout;
            _font = font;
            gameObject.layer = groundLayer;

            _body = GetComponent<Rigidbody2D>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.simulated = true;

            // すべてソリッド。文字ごとの箱を1枚に融合して継ぎ目に引っかからないようにする。
            _composite = GetComponent<CompositeCollider2D>();
            if (_composite == null) _composite = gameObject.AddComponent<CompositeCollider2D>();
            _composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            _composite.generationType = CompositeCollider2D.GenerationType.Manual;
            _composite.offsetDistance = 0.01f;

            var eff = GetComponent<PlatformEffector2D>();
            if (eff != null) eff.enabled = false;
        }

        void RegenCollision()
        {
            if (_composite != null) _composite.GenerateGeometry();
        }

        public void Configure(LineSpec spec)
        {
            Spec = spec;
            Active = true;
            _usedSegs = 0;

            transform.position = new Vector3(0f, spec.WorldY, 0f);
            gameObject.name = $"Line[{spec.Kind}] y{spec.WorldY:0.0}";

            float scale = spec.Kind == LineKind.Title ? _layout.titleScale
                        : spec.Kind == LineKind.Header ? _layout.headerScale
                        : 1f;

            if (spec.Kind == LineKind.Rule)
            {
                BuildRule();
                HideUnused();
                RegenCollision();
                return;
            }

            if (spec.Kind == LineKind.RoleName)
            {
                BuildSeg(Sanitize(spec.Left), _layout.roleColor, scale, Align.RightTo, -_layout.centerGap);
                BuildSeg(Sanitize(spec.Right), _layout.nameColor, scale, Align.LeftFrom, _layout.centerGap);
            }
            else
            {
                Color color = spec.Kind switch
                {
                    LineKind.Header => _layout.headerColor,
                    LineKind.Title => _layout.titleColor,
                    LineKind.Free => _layout.dimColor,
                    _ => _layout.nameColor,
                };
                BuildSeg(Sanitize(spec.Left), color, scale, Align.Center, 0f);
            }

            HideUnused();
            RegenCollision();
        }

        public void Release()
        {
            Active = false;
            Spec = null;
            for (int i = 0; i < _segs.Count; i++)
            {
                var s = _segs[i];
                if (s.tmp != null) { s.tmp.text = string.Empty; s.tmp.gameObject.SetActive(false); }
                for (int b = 0; b < s.boxes.Count; b++) s.boxes[b].enabled = false;
                s.usedBoxes = 0;
                s.built = false;
            }
            _usedSegs = 0;
            gameObject.SetActive(false);
        }

        /// <summary>弾がこの行に当たった。world 位置に最も近い文字を1つ消す。</summary>
        public bool HitAt(Vector2 world)
        {
            Seg hitSeg = null;
            int hitChar = -1;
            float best = float.MaxValue;

            for (int i = 0; i < _usedSegs; i++)
            {
                var s = _segs[i];
                for (int b = 0; b < s.usedBoxes; b++)
                {
                    var col = s.boxes[b];
                    if (!col.enabled) continue;
                    int ci = s.boxChar[b];
                    if (ci < 0 || ci >= s.destroyed.Length || s.destroyed[ci]) continue; // 床(-1)等は壊さない

                    // 行GOは無回転・無スケールなので offset+size から world AABB を直接出す
                    Vector2 c = (Vector2)transform.position + col.offset;
                    Vector2 half = col.size * 0.5f;
                    Vector2 dv = new Vector2(Mathf.Abs(world.x - c.x) - half.x, Mathf.Abs(world.y - c.y) - half.y);
                    if (dv.x <= 0f && dv.y <= 0f)
                    {
                        hitSeg = s; hitChar = ci; best = 0f;
                        break;
                    }
                    float d = ((c - world)).sqrMagnitude;
                    if (d < best) { best = d; hitSeg = s; hitChar = ci; }
                }
                if (best == 0f) break;
            }

            if (hitSeg == null || hitChar < 0 || best > 1.0f) return false;
            hitSeg.destroyed[hitChar] = true;
            RenderSeg(hitSeg);
            RegenCollision();
            TotalCharsDestroyed++;
            return true;
        }

        /// <summary>検証用: これまでに弾で消された文字の総数。</summary>
        public static int TotalCharsDestroyed;

        // --- 内部 -------------------------------------------------------
        enum Align { Center, LeftFrom, RightTo }

        /// <summary>===== 行: 全幅のソリッドな床。文字ごとの当たり判定は作らず1本の広いコライダ。</summary>
        void BuildRule()
        {
            Seg seg = GetSeg();
            seg.original = "=";
            seg.destroyed = new bool[1];
            seg.scale = 1f;
            seg.k = 1f;
            seg.originX = 0f;

            float cell = _layout.cellHeight > 0f ? _layout.cellHeight : 1f;
            float chr = _layout.colliderHeightRatio > 0f ? _layout.colliderHeightRatio : 0.9f;

            var tmp = seg.tmp;
            tmp.gameObject.SetActive(true);
            tmp.font = _font;
            tmp.color = _layout.dimColor;
            tmp.richText = true;
            tmp.enableVertexGradient = false;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontSize = RefSize;
            float mspaceEm = _layout.CellWidth(false) / cell;
            tmp.text = $"<mspace={mspaceEm:0.####}em>{new string('=', 200)}";

            var rt = tmp.rectTransform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 40f);
            rt.localRotation = Quaternion.identity;
            rt.localPosition = Vector3.zero;
            tmp.ForceMeshUpdate();
            var ti = tmp.textInfo;
            float refH = (ti.lineCount > 0) ? (ti.lineInfo[0].ascender - ti.lineInfo[0].descender) : RefSize;
            if (refH <= 0.0001f) refH = RefSize;
            rt.localScale = Vector3.one * (cell / refH);

            var box = GetBox(seg);
            box.enabled = true;
            box.isTrigger = false;
            box.usedByComposite = true;
            box.offset = new Vector2(0f, _layout.colliderYOffset);
            box.size = new Vector2(240f, Mathf.Max(0.1f, cell * chr));
            seg.boxChar[seg.usedBoxes - 1] = -1;
        }

        void BuildSeg(string s, Color color, float scale, Align align, float anchorX)
        {
            if (string.IsNullOrEmpty(s)) return;

            Seg seg = GetSeg();
            seg.original = s;
            seg.destroyed = new bool[s.Length];
            seg.scale = scale;
            seg.tmp.gameObject.SetActive(true);
            seg.tmp.font = _font;
            seg.tmp.color = color;
            seg.tmp.richText = true;
            seg.tmp.enableVertexGradient = false;
            seg.tmp.alignment = TextAlignmentOptions.MidlineLeft;
            seg.tmp.textWrappingMode = TextWrappingModes.NoWrap;
            seg.tmp.overflowMode = TextOverflowModes.Overflow;
            seg.tmp.fontSize = RefSize;

            bool cjk = GameLayout.ContainsCJK(s);
            float targetH = _layout.cellHeight * scale;
            float mspaceEm = _layout.CellWidth(cjk) / _layout.cellHeight;
            seg.tmp.text = $"<mspace={mspaceEm:0.####}em>{s}";

            var rt = seg.tmp.rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(10000f, 200f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;

            seg.tmp.ForceMeshUpdate();
            EnsurePointFilter(_font);
            var ti = seg.tmp.textInfo;

            float refH = (ti.lineCount > 0) ? (ti.lineInfo[0].ascender - ti.lineInfo[0].descender) : RefSize;
            if (refH <= 0.0001f) refH = RefSize;
            seg.k = targetH / refH;

            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < ti.characterCount; i++)
            {
                var ch = ti.characterInfo[i];
                if (!ch.isVisible) continue;
                if (ch.origin < minX) minX = ch.origin;
                if (ch.xAdvance > maxX) maxX = ch.xAdvance;
            }
            if (minX == float.MaxValue) { minX = 0f; maxX = 0f; }

            seg.originX = align switch
            {
                Align.Center => -0.5f * (minX + maxX) * seg.k,
                Align.LeftFrom => anchorX - minX * seg.k,
                Align.RightTo => anchorX - maxX * seg.k,
                _ => 0f,
            };
            seg.built = true;

            RenderSeg(seg);
        }

        /// <summary>seg のテキストと当たり判定を(再)構築する。位置・スケールは不変。</summary>
        void RenderSeg(Seg seg)
        {
            // 表示文字列: 消された文字は空白に
            var chars = seg.original.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (seg.destroyed[i]) chars[i] = ' ';
            string disp = new string(chars);

            bool cjk = GameLayout.ContainsCJK(seg.original);
            float targetH = _layout.cellHeight * seg.scale;
            float mspaceEm = _layout.CellWidth(cjk) / _layout.cellHeight;
            seg.tmp.text = $"<mspace={mspaceEm:0.####}em>{disp}";

            var rt = seg.tmp.rectTransform;
            rt.localScale = Vector3.one * seg.k;
            rt.localPosition = new Vector3(seg.originX, 0f, 0f);

            seg.tmp.ForceMeshUpdate();
            var ti = seg.tmp.textInfo;

            seg.usedBoxes = 0;
            float chr = _layout.colliderHeightRatio > 0f ? _layout.colliderHeightRatio : 0.9f;
            float colliderH = (_layout.cellHeight > 0f ? _layout.cellHeight : 1f) * chr;
            float wRatio = _layout.charColliderWidthRatio > 0f ? _layout.charColliderWidthRatio : 1f;

            int n = Mathf.Min(ti.characterCount, seg.original.Length);
            for (int i = 0; i < n; i++)
            {
                if (seg.destroyed[i]) continue;
                var ch = ti.characterInfo[i];
                if (!ch.isVisible) continue; // 空白 = 隙間

                float x0 = seg.originX + ch.origin * seg.k;
                float x1 = seg.originX + ch.xAdvance * seg.k;
                float w = (x1 - x0) * wRatio;
                if (w < 0.03f) w = x1 - x0;

                BoxCollider2D box = GetBox(seg);
                box.enabled = true;
                box.isTrigger = false;
                box.usedByComposite = true;
                box.offset = new Vector2((x0 + x1) * 0.5f, _layout.colliderYOffset);
                box.size = new Vector2(Mathf.Max(0.03f, w), Mathf.Max(0.1f, colliderH));
                seg.boxChar[seg.usedBoxes - 1] = i;
            }

            for (int b = seg.usedBoxes; b < seg.boxes.Count; b++)
                seg.boxes[b].enabled = false;
        }

        Seg GetSeg()
        {
            if (_usedSegs < _segs.Count)
            {
                var s = _segs[_usedSegs++];
                s.usedBoxes = 0;
                return s;
            }
            var go = new GameObject("seg", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            var seg = new Seg { tmp = go.GetComponent<TextMeshPro>() };
            seg.tmp.sortingOrder = 1;
            _segs.Add(seg);
            _usedSegs++;
            return seg;
        }

        BoxCollider2D GetBox(Seg seg)
        {
            if (seg.usedBoxes < seg.boxes.Count)
            {
                seg.usedBoxes++;
                return seg.boxes[seg.usedBoxes - 1];
            }
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.usedByComposite = true;
            seg.boxes.Add(box);
            seg.boxChar.Add(0);
            seg.usedBoxes++;
            return box;
        }

        void HideUnused()
        {
            for (int k = _usedSegs; k < _segs.Count; k++)
            {
                var s = _segs[k];
                if (s.tmp != null) { s.tmp.text = string.Empty; s.tmp.gameObject.SetActive(false); }
                for (int b = 0; b < s.boxes.Count; b++) s.boxes[b].enabled = false;
                s.usedBoxes = 0;
            }
        }

        static int _lastAtlasCount = -1;
        static void EnsurePointFilter(TMP_FontAsset font)
        {
            if (font == null || font.atlasTextures == null) return;
            int c = font.atlasTextures.Length;
            if (c == _lastAtlasCount) return;
            _lastAtlasCount = c;
            foreach (var tex in font.atlasTextures)
                if (tex != null) tex.filterMode = FilterMode.Point;
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.IndexOf('<') < 0 && s.IndexOf('>') < 0) return s;
            return s.Replace("<", string.Empty).Replace(">", string.Empty);
        }
    }
}
