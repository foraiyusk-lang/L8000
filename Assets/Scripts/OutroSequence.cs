using System.Collections;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// 頂上のタイトルに触れると発動。操作を奪い、視点ごと上から下へ降りていく。
    ///   ・降りながら「残り」を一気に使い切る(戻るのにも回数がかかる)
    ///   ・下からは「無」が速くせり上がってきて途中で合流 → 飲み込み → EndSequence
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
        public float titleTouchRadius = 1.5f;

        [Tooltip("降りる速さ(world units / 秒)。")]
        public float descendSpeed = 7f;

        [Tooltip("発動してから降り始めるまでの静止(頂上を見せる間)。")]
        public float pauseAtTop = 1.4f;

        public bool Active { get; private set; }

        Rigidbody2D _rb;
        bool _descending;
        int _drainFrom;

        void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (player != null) _rb = player.GetComponent<Rigidbody2D>();
        }

        void LateUpdate()
        {
            if (Active) { if (_descending) Descend(); return; }
            if (player == null || streamer == null || streamer.TopY <= 1f) return;

            if (player.position.y >= streamer.TopY) { Begin(); return; }
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
            _drainFrom = GameState.Instance != null ? GameState.Instance.Remaining : 0;

            ScreenFx.Flash(new Color(1f, 1f, 1f, 0.85f), 0.6f);
            ScreenFx.Toast("― 戻る ―", Color.white, 2.2f);

            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(pauseAtTop);
            _descending = true;

            if (voidRiser != null)
            {
                float bottom = streamer != null ? streamer.BottomY : 0f;
                voidRiser.player = player;
                voidRiser.startGapBelowPlayer = Mathf.Max(4f, (player != null ? player.position.y : 0f) - bottom);
                voidRiser.Trigger(descendSpeed * 1.15f);
            }
        }

        void Descend()
        {
            float dt = Time.deltaTime;
            float bottom = streamer != null ? streamer.BottomY : 0f;
            float top = streamer != null ? streamer.TopY : 1f;

            if (player != null)
            {
                var p = player.position;
                p.y = Mathf.Max(bottom, p.y - descendSpeed * dt);
                p.x = Mathf.Lerp(p.x, 0f, 3f * dt);
                player.position = p;
                if (_rb != null) _rb.position = p;

                // 降りた進捗ぶん「残り」を削る(降りるのにも回数を使う)。
                // 降下の 55% 地点で 0 になるよう前倒し(無に合流する頃には使い切っている)。
                if (GameState.Instance != null && !GameState.Instance.Depleted)
                {
                    float progress = Mathf.Clamp01(Mathf.InverseLerp(top, bottom, p.y) / 0.55f);
                    int target = Mathf.RoundToInt(_drainFrom * (1f - progress));
                    int cut = GameState.Instance.Remaining - Mathf.Max(0, target);
                    if (cut > 0) GameState.Instance.Spend(cut);
                }
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
