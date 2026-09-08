using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// クレジット1行を実体化する。プール対象。
    ///   - 足場は「空白で区切られた連続文字ラン = 1個のソリッドな BoxCollider2D」。継ぎ目なし。
    ///     下からも横からも抜けられない。単語間/役割と名前の間/姓名の間の空白 = 隙間。
    ///   - 弾が当たった文字は消える。等幅なので消しても他の文字位置はずれない。
    ///     当たり判定は文字の X 範囲から直接計算する(コライダに依存しない)。
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
            public float[] chX0, chX1;   // 各文字の line 空間 X 範囲(不可視は x0==x1)
            public bool[] chVis;
            public TextMeshPro tmp;
            public float scale;
            public float k;
            public float originX;
            public bool isRule;
            public readonly List<BoxCollider2D> boxes = new List<BoxCollider2D>();
            public int usedBoxes;
        }

        readonly List<Seg> _segs = new List<Seg>();
        int _usedSegs;
        Rigidbody2D _body;

        public LineSpec Spec { get; private set; }
        public bool Active { get; private set; }

        /// <summary>検証用: これまでに弾で消された文字の総数。</summary>
        public static int TotalCharsDestroyed;

        public void Init(GameLayout layout, TMP_FontAsset font, int groundLayer)
        {
            _layout = layout;
            _font = font;
            gameObject.layer = groundLayer;

            _body = GetComponent<Rigidbody2D>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.simulated = true;

            var eff = GetComponent<PlatformEffector2D>();
            if (eff != null) eff.enabled = false;
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
            }
            else if (spec.Kind == LineKind.RoleName)
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
            }
            _usedSegs = 0;
            gameObject.SetActive(false);
        }

        /// <summary>弾がこの行に当たった。world 位置の文字を1つ消す。</summary>
        public static bool DebugHitLog;

        public bool HitAt(Vector2 world)
        {
            float lx = world.x - transform.position.x;
            float ly = world.y - transform.position.y;

            float colliderH = ColliderH();
            if (Mathf.Abs(ly - _layout.colliderYOffset) > colliderH * 0.5f + 0.5f)
            {
                if (DebugHitLog) Debug.Log($"[Hit] ly reject: ly={ly:0.00} limit={colliderH * 0.5f + 0.5f:0.00}");
                return false;
            }

            Seg bestSeg = null; int bestChar = -1; float bestDist = float.MaxValue;

            for (int si = 0; si < _usedSegs; si++)
            {
                var s = _segs[si];
                if (s.isRule || s.chX0 == null) continue;
                for (int i = 0; i < s.chX0.Length; i++)
                {
                    if (!s.chVis[i] || s.destroyed[i]) continue;
                    if (lx >= s.chX0[i] - 0.08f && lx <= s.chX1[i] + 0.08f)
                        return Destroy(s, i);
                    float cx = (s.chX0[i] + s.chX1[i]) * 0.5f;
                    float d = Mathf.Abs(cx - lx);
                    if (d < bestDist) { bestDist = d; bestSeg = s; bestChar = i; }
                }
            }

            if (DebugHitLog)
                Debug.Log($"[Hit] miss: lx={lx:0.00} ly={ly:0.00} segs={_usedSegs} bestDist={bestDist:0.00} " +
                    (bestSeg != null ? $"nearestSeg[0..]=({bestSeg.chX0[0]:0.0}..{bestSeg.chX1[bestSeg.chX1.Length - 1]:0.0})" : "noSeg"));

            if (bestSeg != null && bestDist < 0.8f) return Destroy(bestSeg, bestChar);
            return false;
        }

        bool Destroy(Seg s, int i)
        {
            s.destroyed[i] = true;
            RenderSeg(s);
            TotalCharsDestroyed++;
            return true;
        }

        // --- 内部 -------------------------------------------------------
        enum Align { Center, LeftFrom, RightTo }

        float ColliderH()
        {
            float cell = _layout.cellHeight > 0f ? _layout.cellHeight : 1f;
            float chr = _layout.colliderHeightRatio > 0f ? _layout.colliderHeightRatio : 0.9f;
            return cell * chr;
        }

        void BuildRule()
        {
            Seg seg = GetSeg();
            seg.original = "=";
            seg.destroyed = new bool[1];
            seg.chX0 = null; seg.chX1 = null; seg.chVis = null;
            seg.isRule = true;
            seg.scale = 1f; seg.k = 1f; seg.originX = 0f;

            float cell = _layout.cellHeight > 0f ? _layout.cellHeight : 1f;
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
            tmp.text = $"<mspace={_layout.CellWidth(false) / cell:0.####}em>{new string('=', 220)}";

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
            box.offset = new Vector2(0f, _layout.colliderYOffset);
            box.size = new Vector2(400f, Mathf.Max(0.1f, ColliderH()));
            EnsurePointFilter(_font);
        }

        void BuildSeg(string s, Color color, float scale, Align align, float anchorX)
        {
            if (string.IsNullOrEmpty(s)) return;

            Seg seg = GetSeg();
            seg.original = s;
            seg.destroyed = new bool[s.Length];
            seg.chX0 = new float[s.Length];
            seg.chX1 = new float[s.Length];
            seg.chVis = new bool[s.Length];
            seg.isRule = false;
            seg.scale = scale;

            var tmp = seg.tmp;
            tmp.gameObject.SetActive(true);
            tmp.font = _font;
            tmp.color = color;
            tmp.richText = true;
            tmp.enableVertexGradient = false;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontSize = RefSize;

            bool cjk = GameLayout.ContainsCJK(s);
            float targetH = _layout.cellHeight * scale;
            float mspaceEm = _layout.CellWidth(cjk) / _layout.cellHeight;
            tmp.text = $"<mspace={mspaceEm:0.####}em>{s}";

            var rt = tmp.rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(10000f, 200f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;

            tmp.ForceMeshUpdate();
            EnsurePointFilter(_font);
            var ti = tmp.textInfo;

            float refH = (ti.lineCount > 0) ? (ti.lineInfo[0].ascender - ti.lineInfo[0].descender) : RefSize;
            if (refH <= 0.0001f) refH = RefSize;
            seg.k = targetH / refH;

            float minX = float.MaxValue, maxX = float.MinValue;
            int n = Mathf.Min(ti.characterCount, s.Length);
            for (int i = 0; i < n; i++)
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

            RenderSeg(seg);
        }

        /// <summary>テキストと当たり判定を(再)構築する。位置・スケールは不変。</summary>
        void RenderSeg(Seg seg)
        {
            var chars = seg.original.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (seg.destroyed[i]) chars[i] = ' ';

            bool cjk = GameLayout.ContainsCJK(seg.original);
            float mspaceEm = _layout.CellWidth(cjk) / _layout.cellHeight;
            seg.tmp.text = $"<mspace={mspaceEm:0.####}em>{new string(chars)}";

            var rt = seg.tmp.rectTransform;
            rt.localScale = Vector3.one * seg.k;
            rt.localPosition = new Vector3(seg.originX, 0f, 0f);

            seg.tmp.ForceMeshUpdate();
            var ti = seg.tmp.textInfo;

            int n = Mathf.Min(ti.characterCount, seg.original.Length);
            for (int i = 0; i < seg.chX0.Length; i++)
            {
                if (i < n && ti.characterInfo[i].isVisible && !seg.destroyed[i])
                {
                    var ch = ti.characterInfo[i];
                    seg.chX0[i] = seg.originX + ch.origin * seg.k;
                    seg.chX1[i] = seg.originX + ch.xAdvance * seg.k;
                    seg.chVis[i] = true;
                }
                else
                {
                    seg.chVis[i] = false;
                    seg.chX0[i] = seg.chX1[i] = 0f;
                }
            }

            // 連続する可視文字 = 1個の足場コライダ(ラン)。中央の『縦の道』はくり抜く。
            seg.usedBoxes = 0;
            float colliderH = ColliderH();
            float chute = Mathf.Max(0f, _layout.centerChuteHalfWidth);
            int k = 0;
            while (k < seg.chX0.Length)
            {
                if (!seg.chVis[k]) { k++; continue; }
                float left = seg.chX0[k], right = seg.chX1[k];
                while (k < seg.chX0.Length && seg.chVis[k]) { right = seg.chX1[k]; k++; }
                AddRunCollider(seg, left, right, colliderH, chute);
            }

            for (int b = seg.usedBoxes; b < seg.boxes.Count; b++)
                seg.boxes[b].enabled = false;
        }

        /// <summary>ラン[left,right]を足場化。中央 [-chute,+chute] に掛かる場合は左右に分割してくり抜く。</summary>
        void AddRunCollider(Seg seg, float left, float right, float colliderH, float chute)
        {
            if (chute <= 0f || right <= -chute || left >= chute)
            {
                EmitBox(seg, left, right, colliderH);
                return;
            }
            if (left < -chute) EmitBox(seg, left, -chute, colliderH);
            if (right > chute) EmitBox(seg, chute, right, colliderH);
        }

        void EmitBox(Seg seg, float left, float right, float colliderH)
        {
            if (right - left < 0.03f) return;
            var box = GetBox(seg);
            box.enabled = true;
            box.isTrigger = false;
            box.offset = new Vector2((left + right) * 0.5f, _layout.colliderYOffset);
            box.size = new Vector2(right - left, Mathf.Max(0.1f, colliderH));
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
            seg.boxes.Add(box);
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
