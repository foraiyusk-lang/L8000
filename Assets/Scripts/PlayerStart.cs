using UnityEngine;

namespace L8000
{
    /// <summary>
    /// ロール解析後(=RollStreamer.Awake の後)に、下端の行を即実体化してからプレイヤーを
    /// その上へ置く。足場が streaming 到着前だとプレイヤーが下へ抜けて挟まるため。
    /// </summary>
    public class PlayerStart : MonoBehaviour
    {
        public RollStreamer roll;
        public Transform player;
        public float heightAboveFloor = 0.6f;
        public bool spawnSafetyFloor = true;

        void Start()
        {
            if (roll == null || player == null) return;

            float floorY = roll.BottomY;

            // 下端まわりの行を先に実体化(落下抜け防止)
            roll.SpawnImmediate(floorY - 3f, floorY + 14f);
            Physics2D.SyncTransforms();

            // 下端行のコライダ上面へ乗せる
            float layoutTop = 0.35f;
            var layout = roll.layout;
            if (layout != null)
                layoutTop = layout.cellHeight * Mathf.Max(0.2f, layout.colliderHeightRatio) * 0.5f + layout.colliderYOffset;
            player.position = new Vector3(0f, floorY + layoutTop + heightAboveFloor, player.position.z);

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = player.position; rb.linearVelocity = Vector2.zero; }

            if (spawnSafetyFloor)
            {
                // 下端行のコライダと同じ高さ・厚みで、隙間に挟まれないよう完全に重ねる
                var go = new GameObject("SafetyFloor");
                int g = LayerMask.NameToLayer("Ground");
                go.layer = g >= 0 ? g : 0;
                go.transform.position = new Vector3(0f, floorY + (layout != null ? layout.colliderYOffset : 0f), 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(400f, layout != null ? layout.cellHeight * Mathf.Max(0.2f, layout.colliderHeightRatio) : 0.61f);
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
            }
        }
    }
}
