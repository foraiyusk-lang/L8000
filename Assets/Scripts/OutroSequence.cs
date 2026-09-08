using UnityEngine;

namespace L8000
{
    /// <summary>
    /// 頂上のタイトルに触れると発動。プレイヤーの操作を奪い、視点ごと上から下へ降りていく。
    /// 下からは「無」が速くせり上がってきて、途中で合流 → VoidRiser が飲み込み → EndSequence。
    /// </summary>
    public class OutroSequence : MonoBehaviour
    {
        public Transform player;
        public PlayerController playerController;
        public CameraFollow cameraFollow;
        public Camera viewCamera;
        public RollStreamer streamer;
        public VoidRiser voidRiser;

        [Tooltip("タイトル行にこの距離まで近づいたら接触とみなす。")]
        public float titleTouchRadius = 0.9f;

        [Tooltip("降りる速さ(world units / 秒)。")]
        public float descendSpeed = 6f;

        public bool Active { get; private set; }

        Rigidbody2D _rb;

        void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (player != null) _rb = player.GetComponent<Rigidbody2D>();
        }

        void LateUpdate()
        {
            if (Active) { Descend(); return; }
            if (player == null || streamer == null || streamer.TopY <= 1f) return;

            // タイトル行付近まで来たら発動(乗る・頭で触れる・かすめる)
            if (player.position.y < streamer.TopY - 4f) return;
            var hits = Physics2D.OverlapCircleAll(player.position, titleTouchRadius);
            foreach (var h in hits)
            {
                var b = h.GetComponentInParent<CreditLineBuilder>();
                if (b != null && b.Spec != null && b.Spec.Kind == LineKind.Title) { Begin(); return; }
            }
        }

        public void Begin()
        {
            if (Active) return;
            Active = true;

            if (cameraFollow != null) cameraFollow.enabled = false;
            if (playerController != null) playerController.enabled = false;
            if (_rb != null) { _rb.bodyType = RigidbodyType2D.Kinematic; _rb.linearVelocity = Vector2.zero; }

            EndSequence.ReachedTop = true;

            // 「無」を下から速くせり上がらせる(降りてくるプレイヤーと合流させる)
            if (voidRiser != null)
            {
                float bottom = streamer != null ? streamer.BottomY : 0f;
                voidRiser.player = player;
                voidRiser.startGapBelowPlayer = Mathf.Max(4f, player.position.y - bottom);
                voidRiser.Trigger(descendSpeed * 1.9f);
            }
        }

        void Descend()
        {
            float dt = Time.deltaTime;
            float bottom = streamer != null ? streamer.BottomY : 0f;

            if (player != null)
            {
                var p = player.position;
                p.y = Mathf.Max(bottom, p.y - descendSpeed * dt);
                p.x = Mathf.Lerp(p.x, 0f, 3f * dt);
                player.position = p;
                if (_rb != null) _rb.position = p;
            }

            if (viewCamera != null)
            {
                var c = viewCamera.transform.position;
                c.y = player != null ? player.position.y : c.y - descendSpeed * dt;
                viewCamera.transform.position = c;
            }
        }
    }
}
