using UnityEngine;

namespace L8000
{
    /// <summary>
    /// 残り8000回 のレイアウト共有設定。
    /// 「文字調節はこっちで指示する」との事なので、寸法・色・ストリーミングの値は
    /// すべてここ（ScriptableObject アセット）に集約して Inspector から即調整できるようにしている。
    ///
    /// 座標系の約束:
    ///   - 1 world unit = 1 セル高 = マルモニカの全角高(16px) 相当。PPU = 16。
    ///   - 欧文(ASCII)は半角セル、CJK/全角は全角セル。1行内は「CJKを含むか」で列幅を切り替える。
    ///   - Y は上が正。ロール下端(©カード)が worldY≈0、上端(タイトル)が最大。登る = Y 増加。
    /// </summary>
    [CreateAssetMenu(menuName = "L8000/Game Layout", fileName = "GameLayout")]
    public class GameLayout : ScriptableObject
    {
        [Header("スケール")]
        [Tooltip("1セル高 = TMP fontSize(world)。テキストを大きくしたいときはここを上げて lineStepY も合わせる。")]
        public float cellHeight = 1.0f;

        [Tooltip("欧文(半角)1文字の列幅。cellHeight を基準にした比率で持つとズレにくい。")]
        public float halfCellWidth = 0.5f;

        [Tooltip("CJK/全角1文字の列幅。通常は halfCellWidth の2倍。")]
        public float fullCellWidth = 1.0f;

        [Header("行の縦間隔")]
        [Tooltip("クレジット1エントリごとの基本の縦送り(world units)。")]
        public float lineStepY = 1.5f;

        [Tooltip("空行 1つあたりの追加の縦送り(world units)。")]
        public float blankLineGap = 0.5f;

        [Tooltip("見出し(#)の前後に足す余白(world units)。")]
        public float headerPad = 0.4f;

        [Tooltip("[gap N] 1単位あたりの縦送り(world units)。")]
        public float gapUnit = 0.6f;

        [Tooltip("連続する足場の最大の縦間隔(world units)。ジャンプ到達高より必ず小さく。" +
                 "credits.txt に大きな [gap] があってもここで頭打ちにして『必ず登れる』を保証する。")]
        public float maxLineGap = 2.6f;

        [Tooltip("連続する足場の最小の縦間隔(world units)。足場が重ならないように。")]
        public float minLineGap = 1.3f;

        [Header("役割 :: 名前 の2カラム")]
        [Tooltip("中央線から役割/名前をどれだけ離すか(world units)。")]
        public float centerGap = 0.4f;

        [Header("色 / スケール")]
        public Color nameColor = Color.white;
        public Color roleColor = new Color(0.72f, 0.78f, 0.9f);
        public Color headerColor = new Color(0.55f, 0.75f, 1f);
        public Color dimColor = new Color(0.5f, 0.5f, 0.5f);
        public Color titleColor = Color.white;

        [Tooltip("見出しのフォント倍率。")]
        public float headerScale = 1.35f;

        [Tooltip("タイトル(=)のフォント倍率。")]
        public float titleScale = 1.8f;

        [Header("ストリーミング(画面内だけ実体化)")]
        [Tooltip("カメラ表示範囲の上下に足すマージン(world units)。大きいほど早めに生成・遅く破棄。")]
        public float streamMarginY = 10f;

        [Tooltip("1フレームに新規実体化する行数の上限(生成スパイク回避)。")]
        public int maxSpawnPerFrame = 3;

        [Header("足場(当たり判定)")]
        [Tooltip("文字セル高に対するコライダ高の比率。厚めにするとすり抜け(トンネリング)しにくい。")]
        [Range(0.2f, 2f)] public float colliderHeightRatio = 0.9f;

        [Tooltip("コライダの縦位置オフセット(world units)。+で上へ。")]
        public float colliderYOffset = 0f;

        [Tooltip("1文字あたりのコライダ幅の比率(送り幅に対して)。1で隙間なく密着、<1で文字間に細い隙間。")]
        [Range(0.5f, 1f)] public float charColliderWidthRatio = 1f;

        // --- 便利プロパティ -------------------------------------------------
        public float CellWidth(bool wide) => wide ? fullCellWidth : halfCellWidth;

        /// <summary>
        /// 全角(2セル)扱いにする文字か。CJK 統合漢字・かな・全角記号など U+2E80 以降。
        /// ©(U+00A9) や — などのラテン系記号は半角のまま。
        /// </summary>
        public static bool IsWide(char c)
        {
            int u = c;
            return u >= 0x2E80 && u <= 0xFFEF;   // CJK部首〜半角/全角形。©(0x00A9)等は対象外
        }

        public static bool IsHalfWidth(char c) => !IsWide(c);

        /// <summary>行内に全角文字が含まれるか(列幅の切り替え判定)。</summary>
        public static bool ContainsCJK(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if (IsWide(c)) return true;
            return false;
        }
    }
}
