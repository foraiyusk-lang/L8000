using System.Collections.Generic;
using UnityEngine;

namespace L8000
{
    public enum LineKind { Header, RoleName, Name, Free, Title, Rule }

    /// <summary>1行ぶんのクレジット。WorldY はレイアウト計算で確定する。</summary>
    public class LineSpec
    {
        public LineKind Kind;
        public string Left;   // Name/Header/Free/Title は本文。RoleName は役割。
        public string Right;  // RoleName の名前。それ以外は null。
        public float WorldY;
        public bool HasCJK;   // 列幅(半角/全角)の切り替え用。行内に CJK があれば true。
        public bool Solid;    // 必須ゲート: 一方通行にせず、撃って穴を開けないと通れない。
    }

    public class CreditsRoll
    {
        public List<LineSpec> Lines = new List<LineSpec>();
        public float TopY;      // いちばん上の行の WorldY
        public float BottomY;   // いちばん下の行の WorldY(≈0)
    }

    /// <summary>
    /// credits.txt の軽量マークアップを解析する。
    ///   # 見出し
    ///   = タイトル(大きい)
    ///   ~ 自由行 ~        (装飾なし・dim)
    ///   [gap N]           縦の空白を N セルぶん
    ///   役割 :: 名前       中央線を挟んだ2カラム
    ///   名前              名前のみ(中央寄せ)
    ///   (空行)            小さな空白
    /// ファイルは「表示順(上=タイトル, 下=©カード)」で書く。
    /// </summary>
    public static class CreditsMarkup
    {
        static bool IsAllEquals(string s)
        {
            foreach (char c in s) if (c != '=') return false;
            return true;
        }

        enum RawKind { Line, Blank, Gap }

        struct Raw
        {
            public RawKind RawKind;
            public LineSpec Spec;
            public float GapCells;
        }

        public static CreditsRoll Parse(string text, GameLayout layout)
        {
            var raws = new List<Raw>();
            if (text == null) text = string.Empty;
            // 改行コードを吸収
            string[] rawLines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            foreach (string rawLine in rawLines)
            {
                string line = rawLine.TrimEnd();
                string trimmed = line.Trim();

                if (trimmed.Length == 0)
                {
                    raws.Add(new Raw { RawKind = RawKind.Blank });
                    continue;
                }
                if (trimmed.StartsWith("//"))
                    continue; // コメント行

                if (trimmed.StartsWith("[gap") && trimmed.EndsWith("]"))
                {
                    string inner = trimmed.Length > 5 ? trimmed.Substring(4, trimmed.Length - 5).Trim() : "1";
                    if (!float.TryParse(inner, out float n)) n = 1f;
                    raws.Add(new Raw { RawKind = RawKind.Gap, GapCells = Mathf.Max(0f, n) });
                    continue;
                }

                var spec = new LineSpec();
                if (trimmed.Length >= 3 && IsAllEquals(trimmed))
                {
                    spec.Kind = LineKind.Rule;    // ===== 全幅の床/仕切り(ソリッド)
                    spec.Left = trimmed;
                }
                else if (trimmed.StartsWith("@"))
                {
                    spec.Kind = LineKind.Name;    // @TEXT = 空白を詰めたい名前行など(装飾なし)
                    spec.Left = trimmed.Substring(1).Trim();
                }
                else if (trimmed.StartsWith("#"))
                {
                    spec.Kind = LineKind.Header;
                    spec.Left = trimmed.Substring(1).Trim();
                }
                else if (trimmed.StartsWith("="))
                {
                    spec.Kind = LineKind.Title;
                    spec.Left = trimmed.Substring(1).Trim();
                }
                else if (trimmed.StartsWith("~"))
                {
                    spec.Kind = LineKind.Free;
                    string s = trimmed.Substring(1).Trim();
                    if (s.EndsWith("~")) s = s.Substring(0, s.Length - 1).Trim();
                    spec.Left = s;
                }
                else
                {
                    int sep = line.IndexOf("::");
                    if (sep >= 0)
                    {
                        spec.Kind = LineKind.RoleName;
                        spec.Left = line.Substring(0, sep).Trim();
                        spec.Right = line.Substring(sep + 2).Trim();
                    }
                    else
                    {
                        spec.Kind = LineKind.Name;
                        spec.Left = trimmed;
                    }
                }

                spec.HasCJK = GameLayout.ContainsCJK(spec.Left) || GameLayout.ContainsCJK(spec.Right);
                raws.Add(new Raw { RawKind = RawKind.Line, Spec = spec });
            }

            // --- レイアウト: 下から上へ Y を積む -----------------------------
            // 連続する足場の間隔は [minLineGap, maxLineGap] に必ず収める。
            // これにより credits.txt にどんな [gap] があっても「必ず登れる」ことを保証する。
            var roll = new CreditsRoll();
            float y = 0f;
            float pending = 0f;       // 未反映の空白/gap の合計
            bool firstLinePlaced = false;

            for (int i = raws.Count - 1; i >= 0; i--)
            {
                Raw r = raws[i];
                if (r.RawKind == RawKind.Blank) { pending += layout.blankLineGap; continue; }
                if (r.RawKind == RawKind.Gap) { pending += r.GapCells * layout.gapUnit; continue; }

                LineSpec spec = r.Spec;

                if (firstLinePlaced)
                {
                    float advance = layout.lineStepY + pending;
                    if (spec.Kind == LineKind.Header) advance += layout.headerPad;
                    if (spec.Kind == LineKind.Title) advance += layout.headerPad;
                    // 区切り(空行/[gap])があるところだけ少し大きく許す。無ければ通常の行間。
                    float ceiling = pending > 0.001f
                        ? Mathf.Max(layout.maxLineGap, layout.maxSectionGap)
                        : layout.maxLineGap;
                    advance = Mathf.Clamp(advance, layout.minLineGap, Mathf.Max(layout.minLineGap, ceiling));
                    y += advance;
                }
                pending = 0f;

                spec.WorldY = y;
                if (!firstLinePlaced) { roll.BottomY = y; firstLinePlaced = true; }

                roll.Lines.Add(spec);
                roll.TopY = spec.WorldY;
            }

            roll.Lines.Reverse(); // Lines は WorldY 降順(先頭=タイトル、末尾=最下部)

            // 必須ゲート: 下端(登り出し)から数えて最初の数本(Rule 以外)をソリッドに。
            // 撃って穴を開けないと通れない = 射撃を教えるチュートリアル。
            int gates = Mathf.Max(0, layout.gateLineCount);
            for (int i = roll.Lines.Count - 1; i >= 0 && gates > 0; i--)
            {
                if (roll.Lines[i].Kind == LineKind.Rule) continue;
                roll.Lines[i].Solid = true;
                gates--;
            }
            return roll;
        }
    }
}
