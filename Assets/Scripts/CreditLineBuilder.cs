using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// クレジット1行を実体化する。プール対象。
    ///   - 当たり判定は「行1本のソリッドなバー」(minX..maxX)。単語内・単語間の隙間はすべて埋まっていて
    ///     プレイヤーは文字の間をすり抜けられない(端まで歩いて隣の行へ跳ぶ簡単な登り)。
    ///   - 弾が当たると命中文字＋隣の1文字が消え、その X 範囲だけバーに穴が開く → そこを通って上へ抜けられる
    ///     (任意のショートカット)。等幅(mspace)なので消しても文字位置はずれない。
    ///   - 命中判定は文字の X 範囲から直接計算する(コライダに依存しない)。
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
            public int idx;               // 0 = Left/本文, 1 = Right(RoleName)
            public string original = "";
            public bool[] destroyed;
            public float[] baseX0, baseX1;   // 各文字の line 空間 X 範囲(破壊されても不変)
            public bool[] chVis;             // もともと可視の文字か
            public TextMeshPro tmp;
            public float k = 1f;
            public float originX;
            public float mspaceEm = 1f;
            public bool isRule;
        }

        readonly List<Seg> _segs = new List<Seg>();
        int _usedSegs;
        readonly List<BoxCollider2D> _bars = new List<BoxCollider2D>();
        int _usedBars;
        Rigidbody2D _body;
        PlatformEffector2D _effector;
        BoxCollider2D _gateBlock;   // ゲートが閉じている間だけ有効な完全ソリッドの壁
        bool _oneWay;
        bool _isGate;
        bool _gateOpened;
        float _lineScale = 1f;      // Title/Header は拡大。当たり判定もこれに追従させる。

        public LineSpec Spec { get; private set; }
        public bool Active { get; private set; }

        /// <summary>検証用: これまでに弾で消された文字の総数。</summary>
        public static int TotalCharsDestroyed;
        public static bool DebugHitLog;

        /// <summary>文字が破壊されたとき (行, 破壊された文字の連結). 機能無効化フックが購読する。</summary>
        public static System.Action<CreditLineBuilder, string> OnCharsDestroyed;

        /// <summary>必須ゲートに人が通れる幅の穴が初めて開いたとき。</summary>
        public static System.Action OnGateOpened;

        /// <summary>降り演出中: 一度でも文字を壊された行は当たり判定を全て消す(斜線＝素通り)。</summary>
        public static bool DescentDropDestroyed;

        /// <summary>下向き射撃(最下部の ===== 突破など)で行の当たり判定を全消し。</summary>
        /// <summary>最下部の ===== が下向き射撃で突破されたとき。</summary>
        public static System.Action OnBottomBroken;

        public void BreakOpen()
        {
            for (int b = 0; b < _bars.Count; b++) _bars[b].enabled = false;
            if (_gateBlock != null) _gateBlock.enabled = false;
            _broken = true;
            AudioManager.Play(Sfx.Gate);
            if (Spec != null && Spec.Kind == LineKind.Rule) OnBottomBroken?.Invoke();
        }
        bool _broken;

        public bool IsGate => _isGate;

        public void Init(GameLayout layout, TMP_FontAsset font, int groundLayer)
        {
            _layout = layout;
            _font = font;
            gameObject.layer = groundLayer;

            _body = GetComponent<Rigidbody2D>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.simulated = true;

            _effector = GetComponent<PlatformEffector2D>();
            if (_effector == null) _effector = gameObject.AddComponent<PlatformEffector2D>();
            _effector.useOneWay = true;
            _effector.useOneWayGrouping = true;
            _effector.surfaceArc = 150f;      // 上向き±75°から着地可、下から/横からは通り抜け
            _effector.useColliderMask = false;
            _effector.enabled = false;
        }

        public void Configure(LineSpec spec)
        {
            Spec = spec;
            Active = true;
            _usedSegs = 0;
            _usedBars = 0;

            transform.position = new Vector3(0f, spec.WorldY, 0f);
            gameObject.name = $"Line[{spec.Kind}] y{spec.WorldY:0.0}";

            // Rule(=====)は完全ソリッド。それ以外は一方通行。
            // 必須ゲートは「一方通行の足場 ＋ 撃つまで塞ぐ壁(_gateBlock)」。撃つと壁を外す。
            _isGate = spec.Solid && spec.Kind != LineKind.Rule;
            _gateOpened = false;
            _broken = false;
            // 足場は全部ソリッド(下からは抜けられない)。登りは端から。
            // Title(頂上)だけは下から抜けられる = 触れると巻き戻しが発動する目印。
            _oneWay = spec.Kind == LineKind.Title;
            _effector.enabled = _oneWay;

            float scale = spec.Kind == LineKind.Title ? _layout.titleScale
                        : spec.Kind == LineKind.Header ? _layout.headerScale
                        : 1f;
            _lineScale = scale;

            if (_gateBlock == null)
            {
                _gateBlock = gameObject.AddComponent<BoxCollider2D>();
                _gateBlock.usedByEffector = false;
            }
            _gateBlock.enabled = _isGate;
            if (_isGate)
            {
                _gateBlock.offset = new Vector2(0f, ColliderOffsetY());
                _gateBlock.size = new Vector2(400f, Mathf.Max(0.1f, ColliderH()));
            }

            // 必須ゲートは琥珀色で「ここは撃って開ける」と分かるように
            Color gate = new Color(1f, 0.72f, 0.28f);

            if (spec.Kind == LineKind.Rule)
            {
                BuildRule();
            }
            else if (spec.Kind == LineKind.RoleName)
            {
                // まず分割点 0 を挟んで組み、そのあと行全体を左右の列へ寄せる。
                BuildSeg(Sanitize(spec.Left), _isGate ? gate : _layout.roleColor, scale, Align.RightTo, -_layout.centerGap, 0);
                BuildSeg(Sanitize(spec.Right), _isGate ? gate : _layout.nameColor, scale, Align.LeftFrom, _layout.centerGap, 1);
                AnchorLine(spec.StaggerSide);
                RebuildBars();
            }
            else
            {
                Color color = _isGate ? gate : spec.Kind switch
                {
                    LineKind.Header => _layout.headerColor,
                    LineKind.Title => _layout.titleColor,
                    LineKind.Free => _layout.dimColor,
                    _ => _layout.nameColor,
                };
                BuildSeg(Sanitize(spec.Left), color, scale, Align.Center, 0f);
                AnchorLine(spec.StaggerSide);
                RebuildBars();
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
            }
            for (int b = 0; b < _bars.Count; b++) _bars[b].enabled = false;
            if (_gateBlock != null) _gateBlock.enabled = false;
            _usedSegs = 0;
            _usedBars = 0;
            gameObject.SetActive(false);
        }

        // --- 命中 ---------------------------------------------------------
        public bool HitAt(Vector2 world)
        {
            float lx = world.x - transform.position.x;
            float ly = world.y - transform.position.y;

            float colliderH = ColliderH();
            if (Mathf.Abs(ly - ColliderOffsetY()) > colliderH * 0.5f + 0.5f)
            {
                if (DebugHitLog) Debug.Log($"[Hit] ly reject: ly={ly:0.00}");
                return false;
            }

            Seg bestSeg = null; int bestChar = -1; float bestDist = float.MaxValue;

            for (int si = 0; si < _usedSegs; si++)
            {
                var s = _segs[si];
                if (s.isRule || s.baseX0 == null) continue;
                for (int i = 0; i < s.baseX0.Length; i++)
                {
                    if (!s.chVis[i] || s.destroyed[i]) continue;
                    if (lx >= s.baseX0[i] - 0.08f && lx <= s.baseX1[i] + 0.08f)
                        return DestroyAround(s, i);
                    float cx = (s.baseX0[i] + s.baseX1[i]) * 0.5f;
                    float d = Mathf.Abs(cx - lx);
                    if (d < bestDist) { bestDist = d; bestSeg = s; bestChar = i; }
                }
            }

            if (DebugHitLog)
                Debug.Log($"[Hit] miss: lx={lx:0.00} ly={ly:0.00} segs={_usedSegs} bestDist={bestDist:0.00}");

            if (bestSeg != null && bestDist < 0.8f) return DestroyAround(bestSeg, bestChar);
            return false;
        }

        /// <summary>命中文字 i と隣の1文字を消す(プレイヤーが通れる幅の穴)。</summary>
        bool DestroyAround(Seg s, int i)
        {
            var hit = new StringBuilder();
            MarkDestroyed(s, i, hit);

            // 隣を1つ: 可視で未破壊の側を優先(右→左)
            if (i + 1 < s.destroyed.Length && s.chVis[i + 1] && !s.destroyed[i + 1]) MarkDestroyed(s, i + 1, hit);
            else if (i - 1 >= 0 && s.chVis[i - 1] && !s.destroyed[i - 1]) MarkDestroyed(s, i - 1, hit);

            RenderSeg(s);
            RebuildBars();
            AudioManager.Play(Sfx.Hit, 0.6f);
            OnCharsDestroyed?.Invoke(this, hit.ToString());

            if (_isGate && !_gateOpened)
            {
                _gateOpened = true;
                if (_gateBlock != null) _gateBlock.enabled = false;   // 壁を外す → 以降ふつうの一方通行
                AudioManager.Play(Sfx.Gate);
                OnGateOpened?.Invoke();
            }
            return true;
        }

        void MarkDestroyed(Seg s, int i, StringBuilder acc)
        {
            if (s.destroyed[i]) return;
            s.destroyed[i] = true;
            Spec?.MarkDestroyed(s.idx, i, s.destroyed.Length);   // streaming で出入りしても保持
            TotalCharsDestroyed++;
            if (i < s.original.Length) acc.Append(s.original[i]);
        }

        // --- 内部 -------------------------------------------------------
        enum Align { Center, LeftFrom, RightTo }

        /// <summary>この行の見た目の文字セル高(拡大込み)。</summary>
        float LineCellH()
        {
            float cell = _layout.cellHeight > 0f ? _layout.cellHeight : 1f;
            return cell * Mathf.Max(0.01f, _lineScale);
        }

        /// <summary>足場コライダの厚み。文字セル高に追従。</summary>
        float ColliderH()
        {
            float chr = _layout.colliderHeightRatio > 0f ? _layout.colliderHeightRatio : 0.9f;
            return LineCellH() * chr;
        }

        /// <summary>足場コライダの縦オフセット。上端が文字の上端あたりに来る = 文字の上に立つ。</summary>
        float ColliderOffsetY()
        {
            const float topFrac = 0.5f;   // 文字上端の目安(em ボックス中心からの比率)
            return LineCellH() * topFrac - ColliderH() * 0.5f + _layout.colliderYOffset;
        }

        void BuildRule()
        {
            Seg seg = GetSeg();
            seg.original = "=";
            seg.destroyed = new bool[1];
            seg.baseX0 = null; seg.baseX1 = null; seg.chVis = null;
            seg.isRule = true;
            seg.k = 1f; seg.originX = 0f;

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

            var box = GetBar();
            box.enabled = true;
            box.isTrigger = false;
            box.usedByEffector = false;   // Rule は完全ソリッド
            box.offset = new Vector2(0f, ColliderOffsetY());
            box.size = new Vector2(400f, Mathf.Max(0.1f, ColliderH()));
            EnsurePointFilter(_font);
        }

        /// <summary>Localization 破壊 → 以降に組まれる行を文字化けさせる。</summary>
        public static bool Garble;
        const string GarbleChars = "▓▒░#※§¤‡†◆■◇□×";

        static string Mojibake(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var a = s.ToCharArray();
            for (int i = 0; i < a.Length; i++)
                if (a[i] != ' ') a[i] = GarbleChars[Random.Range(0, GarbleChars.Length)];
            return new string(a);
        }

        void BuildSeg(string s, Color color, float scale, Align align, float anchorX, int segIdx = 0)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (Garble) s = Mojibake(s);

            Seg seg = GetSeg();
            seg.idx = segIdx;
            seg.original = s;
            seg.destroyed = new bool[s.Length];
            seg.baseX0 = new float[s.Length];
            seg.baseX1 = new float[s.Length];
            seg.chVis = new bool[s.Length];
            seg.isRule = false;

            // streaming で出入りしても破壊済みなら復元
            var saved = Spec != null ? (segIdx == 0 ? Spec.Dest0 : Spec.Dest1) : null;
            if (saved != null)
                for (int i = 0; i < s.Length && i < saved.Length; i++)
                    seg.destroyed[i] = saved[i];

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

            float targetH = _layout.cellHeight * scale;
            tmp.text = ComposeMspace(s.ToCharArray());

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
                Align.Center => anchorX - 0.5f * (minX + maxX) * seg.k,
                Align.LeftFrom => anchorX - minX * seg.k,
                Align.RightTo => anchorX - maxX * seg.k,
                _ => 0f,
            };

            // 文字ごとの X 範囲を確定(以後不変)。可視文字だけがバーの範囲に効く。
            for (int i = 0; i < s.Length; i++)
            {
                if (i < n && ti.characterInfo[i].isVisible)
                {
                    var ch = ti.characterInfo[i];
                    seg.baseX0[i] = seg.originX + ch.origin * seg.k;
                    seg.baseX1[i] = seg.originX + ch.xAdvance * seg.k;
                    seg.chVis[i] = true;
                }
                else
                {
                    seg.chVis[i] = false;
                    seg.baseX0[i] = seg.baseX1[i] = 0f;
                }
            }

            RenderSeg(seg);
        }

        /// <summary>
        /// 行全体を左右の「列」へ寄せる。
        ///   side +1 … いちばん左の可視文字を +centerGap に(＝右の列。内側の端が中央寄り)
        ///   side -1 … いちばん右の可視文字を -centerGap に(＝左の列)
        ///   side  0 … 中央そろえ(Title / @行 など)
        /// 当たり判定は「可視文字の範囲そのまま」なので、寄せた分だけ baseX0/baseX1 もずらす。
        /// これでプレイヤーは必ず「中央付近の内側の端」からジャンプでき、行の長さに依らず登れる。
        /// </summary>
        void AnchorLine(int side)
        {
            float mn = float.MaxValue, mx = float.MinValue;
            for (int si = 0; si < _usedSegs; si++)
            {
                var s = _segs[si];
                if (s.baseX0 == null) continue;
                for (int i = 0; i < s.baseX0.Length; i++)
                {
                    if (!s.chVis[i]) continue;
                    if (s.baseX0[i] < mn) mn = s.baseX0[i];
                    if (s.baseX1[i] > mx) mx = s.baseX1[i];
                }
            }
            if (mn == float.MaxValue) return;

            float g = _layout.centerGap;
            float shift = side > 0 ? (g - mn)
                        : side < 0 ? (-g - mx)
                        : (-(mn + mx) * 0.5f);

            for (int si = 0; si < _usedSegs; si++)
            {
                var s = _segs[si];
                s.originX += shift;
                if (s.baseX0 != null)
                    for (int i = 0; i < s.baseX0.Length; i++) { s.baseX0[i] += shift; s.baseX1[i] += shift; }
                RenderSeg(s);
            }
        }

        /// <summary>破壊された文字を空白に置換して描画。位置・スケールは不変。コライダは触らない。</summary>
        void RenderSeg(Seg seg)
        {
            if (seg.baseX0 == null) return;

            // 降り演出中: 破壊済みの行は「取り消し線」で全文字を見せる(消えた名前が読める)
            if (DescentDropDestroyed && Spec != null && Spec.AnyDestroyed)
            {
                var rtd = seg.tmp.rectTransform;
                rtd.localScale = Vector3.one * seg.k;
                rtd.localPosition = new Vector3(seg.originX, 0f, 0f);
                seg.tmp.text = $"<s>{ComposeMspace(seg.original.ToCharArray())}</s>";
                seg.tmp.color = new Color(seg.tmp.color.r, seg.tmp.color.g, seg.tmp.color.b, 0.4f);
                seg.tmp.ForceMeshUpdate();
                return;
            }

            var chars = seg.original.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (seg.destroyed[i]) chars[i] = ' ';

            seg.tmp.text = ComposeMspace(chars);
            var rt = seg.tmp.rectTransform;
            rt.localScale = Vector3.one * seg.k;
            rt.localPosition = new Vector3(seg.originX, 0f, 0f);
            seg.tmp.ForceMeshUpdate();
        }

        /// <summary>
        /// 文字ごとに送り幅を切り替えた rich text を作る。
        /// 欧文(半角)は halfCellWidth、CJK/全角は fullCellWidth。
        /// → 「残り8000回」の数字が間延びしない。
        /// </summary>
        string ComposeMspace(char[] chars)
        {
            float halfEm = _layout.CellWidth(false) / _layout.cellHeight;
            float fullEm = _layout.CellWidth(true) / _layout.cellHeight;
            var sb = new StringBuilder(chars.Length + 24);
            int lastWide = -1;
            foreach (char c in chars)
            {
                int wide = GameLayout.IsWide(c) ? 1 : 0;
                if (wide != lastWide)
                {
                    sb.Append("<mspace=").Append((wide == 1 ? fullEm : halfEm).ToString("0.####")).Append("em>");
                    lastWide = wide;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>行1本のソリッドバーを組む。破壊された文字の X 範囲だけ穴。</summary>
        void RebuildBars()
        {
            _usedBars = 0;

            float lineMin = float.MaxValue, lineMax = float.MinValue;
            for (int si = 0; si < _usedSegs; si++)
            {
                var s = _segs[si];
                if (s.baseX0 == null) continue;
                for (int i = 0; i < s.baseX0.Length; i++)
                {
                    if (!s.chVis[i]) continue;
                    if (s.baseX0[i] < lineMin) lineMin = s.baseX0[i];
                    if (s.baseX1[i] > lineMax) lineMax = s.baseX1[i];
                }
            }
            if (lineMin == float.MaxValue || lineMax <= lineMin)
            {
                for (int b = 0; b < _bars.Count; b++) _bars[b].enabled = false;
                return;
            }
            // 当たり判定 = 可視文字の範囲そのまま(AnchorLine で列へ寄せ済み)。
            // 描画されている文字にはすべて足場がある。見えない縁も、はみ出す文字も無い。

            // 穴(破壊された可視文字の X 範囲、少し広げる)。
            // ゲートは撃つと一方通行の「完全な」足場になる(穴はあくまで見た目) → 穴を掘らない。
            var holes = new List<Vector2>();
            const float pad = 0.06f;
            if (!_isGate)
                for (int si = 0; si < _usedSegs; si++)
                {
                    var s = _segs[si];
                    if (s.baseX0 == null) continue;
                    for (int i = 0; i < s.baseX0.Length; i++)
                        if (s.destroyed[i] && s.chVis[i])
                            holes.Add(new Vector2(s.baseX0[i] - pad, s.baseX1[i] + pad));
                }
            holes.Sort((a, b) => a.x.CompareTo(b.x));

            float colliderH = ColliderH();
            float cursor = lineMin;
            foreach (var h in holes)
            {
                float hl = Mathf.Clamp(h.x, lineMin, lineMax);
                float hr = Mathf.Clamp(h.y, lineMin, lineMax);
                if (hr <= cursor) continue;
                if (hl > cursor) EmitBar(cursor, hl, colliderH);
                cursor = Mathf.Max(cursor, hr);
            }
            if (cursor < lineMax) EmitBar(cursor, lineMax, colliderH);

            for (int b = _usedBars; b < _bars.Count; b++) _bars[b].enabled = false;

            // 降り演出中: 壊された行は素通り(斜線表現)
            if (_broken || (DescentDropDestroyed && Spec != null && Spec.AnyDestroyed))
                for (int b = 0; b < _bars.Count; b++) _bars[b].enabled = false;
        }

        void EmitBar(float left, float right, float h)
        {
            // プレイヤー幅(0.26)より狭い細片は足場にしない。
            // 弾で開けた穴を細片が分断して通れなくなるのを防ぐ。
            if (right - left < 0.4f) return;
            var box = GetBar();
            box.enabled = true;
            box.isTrigger = false;
            box.usedByEffector = _oneWay;
            box.offset = new Vector2((left + right) * 0.5f, ColliderOffsetY());
            box.size = new Vector2(right - left, Mathf.Max(0.1f, h));
        }

        Seg GetSeg()
        {
            if (_usedSegs < _segs.Count)
            {
                var s = _segs[_usedSegs++];
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

        BoxCollider2D GetBar()
        {
            if (_usedBars < _bars.Count)
            {
                _usedBars++;
                return _bars[_usedBars - 1];
            }
            var box = gameObject.AddComponent<BoxCollider2D>();
            _bars.Add(box);
            _usedBars++;
            return box;
        }

        void HideUnused()
        {
            for (int k = _usedSegs; k < _segs.Count; k++)
            {
                var s = _segs[k];
                if (s.tmp != null) { s.tmp.text = string.Empty; s.tmp.gameObject.SetActive(false); }
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
