using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// クレジット1行を実体化する。プール対象。
    ///   - 半角/全角のモノスペース・グリッドに強制した TMP(ワールド空間) で描画
    ///   - 実際に生成されたメッシュを計測して cellHeight に合わせて transform をスケール
    ///     (TMP の fontSize は world 単位ではないので必須)
    ///   - 空白で区切られた連続文字ラン = 1本の足場(BoxCollider2D) に融合
    ///   - 行 GameObject に Rigidbody2D(Static) + PlatformEffector2D(one-way)
    /// TMP と BoxCollider2D は使い回す(Destroy しない)ので、行を出し入れしても GC が出にくい。
    /// </summary>
    [DisallowMultipleComponent]
    public class CreditLineBuilder : MonoBehaviour
    {
        const float RefSize = 32f;   // TMP fontSize の基準値(この後スケールで合わせる)

        GameLayout _layout;
        TMP_FontAsset _font;

        readonly List<TextMeshPro> _texts = new List<TextMeshPro>();
        readonly List<BoxCollider2D> _boxes = new List<BoxCollider2D>();
        int _usedTexts;
        int _usedBoxes;

        Rigidbody2D _body;
        PlatformEffector2D _effector;

        public LineSpec Spec { get; private set; }
        public bool Active { get; private set; }

        public void Init(GameLayout layout, TMP_FontAsset font, int groundLayer)
        {
            _layout = layout;
            _font = font;
            gameObject.layer = groundLayer;

            _body = GetComponent<Rigidbody2D>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody2D>();
            // Kinematic: プール再利用で位置を動かしても "Static Collider moved" 警告が出ない
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.simulated = true;

            _effector = GetComponent<PlatformEffector2D>();
            if (_effector == null) _effector = gameObject.AddComponent<PlatformEffector2D>();
            _effector.useOneWay = true;
            _effector.useOneWayGrouping = true;
            _effector.surfaceArc = 140f;
            _effector.rotationalOffset = 0f;
        }

        public void Configure(LineSpec spec)
        {
            Spec = spec;
            Active = true;
            _usedTexts = 0;
            _usedBoxes = 0;

            transform.position = new Vector3(0f, spec.WorldY, 0f);
            gameObject.name = $"Line[{spec.Kind}] y{spec.WorldY:0.0}";

            float scale = spec.Kind == LineKind.Title ? _layout.titleScale
                        : spec.Kind == LineKind.Header ? _layout.headerScale
                        : 1f;

            if (spec.Kind == LineKind.RoleName)
            {
                string role = Sanitize(spec.Left);
                string name = Sanitize(spec.Right);
                // 役割: 中央線の左に右揃え / 名前: 中央線の右に左揃え
                var rr = EmitSegment(role, _layout.roleColor, scale, Align.RightTo, -_layout.centerGap);
                var nr = EmitSegment(name, _layout.nameColor, scale, Align.LeftFrom, _layout.centerGap);
                // 役割と名前の間の隙間を埋めて「1行=1本の足場」にする
                float left = Mathf.Min(rr.x, nr.x);
                float right = Mathf.Max(rr.y, nr.y);
                if (right > left)
                    AddCollider((left + right) * 0.5f, right - left,
                                _layout.cellHeight * _layout.colliderHeightRatio);
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
                EmitSegment(Sanitize(spec.Left), color, scale, Align.Center, 0f);
            }

            HideUnused();
        }

        public void Release()
        {
            Active = false;
            Spec = null;
            for (int i = 0; i < _texts.Count; i++)
            {
                _texts[i].text = string.Empty;
                _texts[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < _boxes.Count; i++)
                _boxes[i].enabled = false;
            _usedTexts = 0;
            _usedBoxes = 0;
            gameObject.SetActive(false);
        }

        // --- 内部 ---------------------------------------------------------
        enum Align { Center, LeftFrom, RightTo }

        /// <returns>この文字列がカバーした line 空間の X 範囲 (x=左, y=右)。空なら (0,0)。</returns>
        Vector2 EmitSegment(string s, Color color, float scale, Align align, float anchorX)
        {
            if (string.IsNullOrEmpty(s)) return Vector2.zero;

            bool cjk = GameLayout.ContainsCJK(s);
            float targetH = _layout.cellHeight * scale;
            float mspaceEm = _layout.CellWidth(cjk) / _layout.cellHeight; // scale 非依存

            TextMeshPro tmp = GetText();
            tmp.font = _font;
            tmp.color = color;
            tmp.richText = true;
            tmp.enableVertexGradient = false;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontSize = RefSize;
            tmp.text = $"<mspace={mspaceEm:0.####}em>{s}";

            RectTransform rt = tmp.rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(10000f, 200f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;

            tmp.ForceMeshUpdate();
            EnsurePointFilter(_font);
            var ti = tmp.textInfo;

            // 基準スケール: 行ボックス高 → targetH
            float refH = (ti.lineCount > 0)
                ? (ti.lineInfo[0].ascender - ti.lineInfo[0].descender)
                : RefSize;
            if (refH <= 0.0001f) refH = RefSize;
            float k = targetH / refH;

            // 可視文字の左右端(ref 空間)
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < ti.characterCount; i++)
            {
                var ch = ti.characterInfo[i];
                if (!ch.isVisible) continue;
                if (ch.bottomLeft.x < minX) minX = ch.bottomLeft.x;
                if (ch.topRight.x > maxX) maxX = ch.topRight.x;
            }
            if (minX == float.MaxValue) { minX = 0f; maxX = 0f; }

            float originX = align switch
            {
                Align.Center => -0.5f * (minX + maxX) * k,
                Align.LeftFrom => anchorX - minX * k,
                Align.RightTo => anchorX - maxX * k,
                _ => 0f,
            };

            rt.localScale = Vector3.one * k;
            rt.localPosition = new Vector3(originX, 0f, 0f);

            // 1行=1本の連続した足場にする(中央の単語間スペースに穴が空くと登れなくなるため)。
            // コライダ高は見出し/タイトルでも通常行と同じ厚さ(登坂のクリアランス確保)。
            float colliderH = _layout.cellHeight * _layout.colliderHeightRatio;
            float leftX = originX + minX * k;
            float rightX = originX + maxX * k;
            if (rightX > leftX)
                AddCollider((leftX + rightX) * 0.5f, rightX - leftX, colliderH);

            return new Vector2(leftX, rightX);
        }

        void AddCollider(float centerX, float width, float colliderH)
        {
            BoxCollider2D box = GetBox();
            box.enabled = true;
            box.usedByEffector = true;
            box.offset = new Vector2(centerX, _layout.colliderYOffset);
            box.size = new Vector2(Mathf.Max(0.05f, width), Mathf.Max(0.1f, colliderH));
        }

        TextMeshPro GetText()
        {
            if (_usedTexts < _texts.Count)
            {
                var t = _texts[_usedTexts++];
                t.gameObject.SetActive(true);
                return t;
            }
            var go = new GameObject("t", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.sortingOrder = 1;
            _texts.Add(tmp);
            _usedTexts++;
            return tmp;
        }

        BoxCollider2D GetBox()
        {
            if (_usedBoxes < _boxes.Count)
                return _boxes[_usedBoxes++];
            var box = gameObject.AddComponent<BoxCollider2D>();
            _boxes.Add(box);
            _usedBoxes++;
            return box;
        }

        void HideUnused()
        {
            for (int k = _usedTexts; k < _texts.Count; k++)
            {
                _texts[k].text = string.Empty;
                _texts[k].gameObject.SetActive(false);
            }
            for (int k = _usedBoxes; k < _boxes.Count; k++)
                _boxes[k].enabled = false;
        }

        // 動的アトラスに新しいグリフが追加されるたび、そのテクスチャは Point フィルタでない
        // ことがある。滲み防止のため増えたら設定し直す。
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
