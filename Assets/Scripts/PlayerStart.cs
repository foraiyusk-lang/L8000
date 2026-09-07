using UnityEngine;

namespace L8000
{
    /// <summary>
    /// ロール解析後(=RollStreamer.Awake の後)に、プレイヤーを最下部(©カード)の上へ置く。
    /// 足場が実体化する前に落ちないよう、少しだけ上に出し、安全床も用意する。
    /// </summary>
    public class PlayerStart : MonoBehaviour
    {
        public RollStreamer roll;
        public Transform player;
        public float heightAboveFloor = 0.9f;
        public bool spawnSafetyFloor = true;

        void Start()
        {
            if (roll == null || player == null) return;

            float floorY = roll.BottomY;
            player.position = new Vector3(0f, floorY + heightAboveFloor, player.position.z);

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;

            if (spawnSafetyFloor)
            {
                var go = new GameObject("SafetyFloor");
                int g = LayerMask.NameToLayer("Ground");
                go.layer = g >= 0 ? g : 0;
                go.transform.position = new Vector3(0f, floorY - 1.2f, 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(400f, 1f);
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
            }
        }
    }
}
