using UnityEngine;

namespace L8000
{
    /// <summary>
    /// プレイヤーを最下部(===== の少し上)へ置き、下に安全床を作る。
    /// タイル方式では全タイルがシーンに存在するので streaming 待ちは不要。
    /// </summary>
    public class PlayerStart : MonoBehaviour
    {
        public TileField roll;
        public Transform player;
        public float heightAboveFloor = 0.8f;
        public bool spawnSafetyFloor = true;
        public bool spawnSideWalls = true;   // 世界の外へ落ちないよう左右に見えない壁
        public float wallX = 8.0f;            // 中心からの距離。play 域(±7.2)の少し外・端ジャンプの邪魔にならない位置

        void Start()
        {
            if (roll == null || player == null) return;

            float floorY = roll.BottomY;
            var ruleTile = roll.RuleTile;
            if (ruleTile != null)
            {
                var b = ruleTile.VisibleBounds;
                floorY = b.max.y;
            }

            player.position = new Vector3(0f, floorY + heightAboveFloor, player.position.z);
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = player.position; rb.linearVelocity = Vector2.zero; }
            Physics2D.SyncTransforms();

            int groundLayer = LayerMask.NameToLayer("Ground");

            if (spawnSafetyFloor && ruleTile != null)
            {
                var go = new GameObject("SafetyFloor");
                go.layer = groundLayer >= 0 ? groundLayer : 0;
                var rb2 = ruleTile.VisibleBounds;
                go.transform.position = new Vector3(0f, rb2.center.y, 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(400f, Mathf.Max(0.2f, rb2.size.y));
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
            }

            if (spawnSideWalls)
            {
                float lo = roll.BottomY - 4f;
                float hi = roll.TopY + 4f;
                float h = Mathf.Max(10f, hi - lo);
                float cy = (lo + hi) * 0.5f;
                foreach (int s in new[] { -1, 1 })
                {
                    var w = new GameObject(s < 0 ? "WallLeft" : "WallRight");
                    w.layer = groundLayer >= 0 ? groundLayer : 0;
                    w.transform.position = new Vector3(s * wallX, cy, 0f);
                    var box = w.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(0.6f, h);
                    var body = w.AddComponent<Rigidbody2D>();
                    body.bodyType = RigidbodyType2D.Static;
                }
            }
        }
    }
}
