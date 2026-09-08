using System.Collections.Generic;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// シーンに手で置かれた CreditTile 群を束ねる。ロールの上端/下端、最下部の ===== 、
    /// 降り演出の当たり判定ルールなどを提供する。RollStreamer の後継。
    /// </summary>
    public class TileField : MonoBehaviour
    {
        public float TopY { get; private set; }
        public float BottomY { get; private set; }

        /// <summary>最下部の ===== タイル(下向き射撃で突破する床)。</summary>
        public CreditTile RuleTile { get; private set; }

        readonly List<CreditTile> _tiles = new List<CreditTile>();
        public IReadOnlyList<CreditTile> Tiles => _tiles;

        /// <summary>降り演出中: 撃たれた(破壊された)タイルは当たり判定を消す(取り消し線で素通り)。</summary>
        public static bool DescentDropDestroyed;

        /// <summary>最下部の ===== が下向き射撃で突破されたとき。</summary>
        public static System.Action OnBottomBroken;

        /// <summary>タイルが破壊されたとき (タイル, 文字列)。副作用フックが購読する。</summary>
        public static System.Action<CreditTile, string> OnTileDestroyed;

        /// <summary>破壊された文字の総数(検証用)。</summary>
        public static int TotalCharsDestroyed;

        /// <summary>文字化け(Localization 破壊)。</summary>
        public static bool Garble;

        public static TileField Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            DescentDropDestroyed = false;
            TotalCharsDestroyed = 0;
            Garble = false;

            _tiles.Clear();
            GetComponentsInChildren(true, _tiles);

            float top = float.MinValue, bot = float.MaxValue;
            foreach (var t in _tiles)
            {
                var b = t.VisibleBounds;
                if (b.max.y > top) top = b.max.y;
                if (b.min.y < bot) bot = b.min.y;
                if (t.isRule) RuleTile = t;
            }
            TopY = top == float.MinValue ? 0f : top;
            BottomY = bot == float.MaxValue ? 0f : bot;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>全タイルのコライダを組み直す(降りの取り消し線ルール反映など)。</summary>
        public void RefreshAll()
        {
            foreach (var t in _tiles) t.Rebuild(force: true);
        }

        public static void FireBottomBroken() => OnBottomBroken?.Invoke();
        public static void FireTileDestroyed(CreditTile t, string s) => OnTileDestroyed?.Invoke(t, s);
    }
}
