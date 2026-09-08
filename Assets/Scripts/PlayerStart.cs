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

            if (spawnSafetyFloor && ruleTile != null)
            {
                var go = new GameObject("SafetyFloor");
                int g = LayerMask.NameToLayer("Ground");
                go.layer = g >= 0 ? g : 0;
                var rb2 = ruleTile.VisibleBounds;
                go.transform.position = new Vector3(0f, rb2.center.y, 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(400f, Mathf.Max(0.2f, rb2.size.y));
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
            }
        }
    }
}
