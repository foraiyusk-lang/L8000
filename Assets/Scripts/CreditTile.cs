using UnityEngine;
using TMPro;

namespace L8000
{
    /// <summary>
    /// クレジットの「単語」または「名前」1つ。シーンビューで縦横に自由に動かせる足場。
    /// このオブジェクトの Transform が位置。子に TextMeshPro(描画)を持ち、
    /// 同じ GameObject の BoxCollider2D を「描画されている文字の範囲」にぴったり合わせる。
    ///
    /// エディタでは [ExecuteAlways] で動かすたびにコライダが追従する。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D))]
    public class CreditTile : MonoBehaviour
    {
        [TextArea] public string text = "";
        public bool isName;
        public bool isRule;
        public bool isHeader;

        [Tooltip("元になったクレジット1行(例: \"Physics Programmer :: D. Bianchi\")。破壊副作用の判定に使う。")]
        public string sourceLine = "";

        [Tooltip("フォント倍率(見出しは 1.35、タイトルは 1.7 など)。")]
        public float sizeScale = 1f;

        [Tooltip("当たり判定を文字の上端側に薄く(足場向き)。false で文字ボックス全体。")]
        public bool thinTop = true;

        [Range(0.1f, 1f)] public float thickness = 0.7f;

        public bool Destroyed { get; private set; }

        public const float RefSize = 32f;
        public const float CellHeight = 0.68f;

        [SerializeField, HideInInspector] TextMeshPro _tmp;
        BoxCollider2D _box;
        [SerializeField, HideInInspector] TMP_FontAsset _font;

        BoxCollider2D Box => _box != null ? _box : (_box = GetComponent<BoxCollider2D>());

        string _built = "\0";
        float _builtScale = -1f;

        public void SetFont(TMP_FontAsset f) { _font = f; if (_tmp != null) _tmp.font = f; }

        void EnsureText()
        {
            if (_tmp != null) return;
            var t = transform.Find("Text");
            if (t == null)
            {
                var go = new GameObject("Text");
                go.transform.SetParent(transform, false);
                t = go.transform;
            }
            _tmp = t.GetComponent<TextMeshPro>();
            if (_tmp == null) _tmp = t.gameObject.AddComponent<TextMeshPro>();
            if (_font != null) _tmp.font = _font;
        }

        void OnEnable() { EnsureText(); Rebuild(force: true); }
        void Start() { Rebuild(force: true); }

        void Update()
        {
            if (_built != text || !Mathf.Approximately(_builtScale, sizeScale))
                Rebuild(force: true);
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                EnsureText();
                Rebuild(force: true);
            };
#endif
        }

        public void Rebuild(bool force = false)
        {
            EnsureText();
            var tmp = _tmp;
            var box = Box;
            if (tmp == null || box == null) return;

            bool strike = Destroyed && TileField.DescentDropDestroyed;
            bool gone   = Destroyed && !TileField.DescentDropDestroyed;
            string bodyText = text ?? "";
            if (Application.isPlaying && TileField.Garble && !isRule) bodyText = Mojibake(bodyText);
            string shown = gone ? "" : (strike ? $"<s>{bodyText}</s>" : bodyText);

            tmp.text = shown;
            tmp.fontSize = RefSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.richText = true;
            tmp.sortingOrder = 1;

            var rt = tmp.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(4000f, 400f);
            rt.localPosition = Vector3.zero;      // ← 子(Text)のローカル。タイル本体の位置は動かさない。
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            tmp.ForceMeshUpdate();
            var ti = tmp.textInfo;

            float refH = ti.lineCount > 0 ? (ti.lineInfo[0].ascender - ti.lineInfo[0].descender) : RefSize;
            if (refH <= 0.0001f) refH = RefSize;
            float k = (CellHeight * Mathf.Max(0.05f, sizeScale)) / refH;
            rt.localScale = Vector3.one * k;

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            bool any = false;
            for (int i = 0; i < ti.characterCount; i++)
            {
                var ch = ti.characterInfo[i];
                if (!ch.isVisible) continue;
                any = true;
                if (ch.bottomLeft.x < minX) minX = ch.bottomLeft.x;
                if (ch.topRight.x > maxX) maxX = ch.topRight.x;
                if (ch.bottomLeft.y < minY) minY = ch.bottomLeft.y;
                if (ch.topRight.y > maxY) maxY = ch.topRight.y;
            }

            if (!any || gone || strike)
            {
                box.enabled = false;
            }
            else
            {
                box.enabled = true;
                box.isTrigger = false;
                float w = (maxX - minX) * k;
                float cx = (minX + maxX) * 0.5f * k;
                float fullH = (maxY - minY) * k;
                float cy = (minY + maxY) * 0.5f * k;

                if (thinTop)
                {
                    float h = CellHeight * Mathf.Max(0.05f, sizeScale) * thickness;
                    float top = cy + fullH * 0.5f;
                    box.size = new Vector2(Mathf.Max(0.05f, w), Mathf.Max(0.05f, h));
                    box.offset = new Vector2(cx, top - h * 0.5f);
                }
                else
                {
                    box.size = new Vector2(Mathf.Max(0.05f, w), Mathf.Max(0.05f, fullH));
                    box.offset = new Vector2(cx, cy);
                }
            }

            Color col = isRule ? new Color(0.5f, 0.5f, 0.5f)
                      : isHeader ? new Color(0.55f, 0.75f, 1f)
                      : isName ? Color.white
                      : new Color(0.72f, 0.78f, 0.9f);
            if (strike) col.a = 0.4f;
            tmp.color = col;

            _built = text;
            _builtScale = sizeScale;
        }

        static readonly string GarbleChars = "▓▒░#※§¤‡†◆■◇□×";
        static string Mojibake(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var a = s.ToCharArray();
            for (int i = 0; i < a.Length; i++)
                if (a[i] != ' ' && a[i] != '　') a[i] = GarbleChars[Random.Range(0, GarbleChars.Length)];
            return new string(a);
        }

        public bool HitTest(Vector2 world)
        {
            var box = Box;
            if (box == null || !box.enabled || Destroyed) return false;
            var c = (Vector2)transform.position + box.offset;
            var half = box.size * 0.5f + new Vector2(0.12f, 0.35f);
            if (Mathf.Abs(world.x - c.x) > half.x || Mathf.Abs(world.y - c.y) > half.y) return false;
            Destroy_();
            return true;
        }

        void Destroy_()
        {
            if (Destroyed) return;
            Destroyed = true;
            TileField.TotalCharsDestroyed += Mathf.Max(1, (text ?? "").Length);
            Rebuild(force: true);
            AudioManager.Play(Sfx.Hit, 0.6f);
            TileField.FireTileDestroyed(this, text ?? "");
            if (isRule) TileField.FireBottomBroken();
        }

        public void BreakOpen() => Destroy_();

        /// <summary>可視文字の world 範囲(min/max)。レイアウト計算用。</summary>
        public Bounds VisibleBounds
        {
            get
            {
                var box = Box;
                if (box == null) return new Bounds(transform.position, Vector3.zero);
                var c = (Vector2)transform.position + box.offset;
                return new Bounds(c, box.size);
            }
        }
    }
}
