using UnityEngine;

namespace L8000
{
    public enum VoidMode { RiseFromBelow, FallFromAbove }

    /// <summary>
    /// 「無」。登り中は下からせり上がり、降り中は上から落ちてくる。
    /// プレイヤーに追いつくと 残り回数を catchCost(=100)消費し、無を一旦止めて
    /// プレイヤーを安全な足場(中央寄り)へ戻す。プレイヤーが1段ぶん進むと再び迫る。
    /// 残り回数が 0 になったまま追いつかれたら OnFinalSwallow(→ 終幕)。
    /// </summary>
    public class VoidRiser : MonoBehaviour
    {
        public Transform player;
        public Camera viewCamera;
        public RollStreamer streamer;

        [Tooltip("発動時、プレイヤーからどれだけ離れて現れるか(world)。")]
        public float startGap = 10f;

        [Tooltip("迫る速さ(world units/秒)。")]
        public float speed = 3.2f;

        [Tooltip("この距離まで来たら『捕まった』。")]
        public float catchMargin = 0.35f;

        [Tooltip("捕まったときの消費。")]
        public int catchCost = 100;

        [Tooltip("捕まった後、この距離ぶん進めば無が再開する(=1段)。")]
        public float advanceToResume = 1.6f;

        public bool Active { get; private set; }
        public VoidMode Mode { get; private set; }
        public float EdgeY { get; private set; }   // 無の先端(プレイヤー側の端)

        /// <summary>捕まった (消費済み)。UI 演出用。</summary>
        public static System.Action<int> OnCaught;
        /// <summary>残り0で飲まれた → 終幕へ。</summary>
        public static System.Action OnFinalSwallow;

        enum St { Idle, Advancing, Caught, Done }
        St _st = St.Idle;
        float _resumeThreshold;
        float _settle;         // 再配置直後の猶予
        Rigidbody2D _prb;
        int _groundMask;
        SpriteRenderer _fill;
        SpriteRenderer _edge;

        int Dir => Mode == VoidMode.RiseFromBelow ? 1 : -1;   // プレイヤーが逃げる向き

        void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (streamer == null) streamer = FindFirstObjectByType<RollStreamer>();
            if (player != null) _prb = player.GetComponent<Rigidbody2D>();
            int g = LayerMask.NameToLayer("Ground");
            _groundMask = g >= 0 ? (1 << g) : Physics2D.AllLayers;

            var srs = GetComponentsInChildren<SpriteRenderer>(true);
            _fill = srs.Length > 0 ? srs[0] : null;
            _edge = srs.Length > 1 ? srs[1] : null;
            SetVisible(false);
        }

        void OnEnable() => GameState.OnDepleted += OnDepleted;
        void OnDisable() => GameState.OnDepleted -= OnDepleted;

        // 残り0 → 無を(まだ止まっていなければ)発動して確実に飲み込ませる
        void OnDepleted()
        {
            if (!Active) Trigger(VoidMode.RiseFromBelow, speed * 1.8f);
            else speed = Mathf.Max(speed, 6f);
        }

        public void Trigger(VoidMode mode, float spd = -1f)
        {
            Mode = mode;
            Active = true;
            _st = St.Advancing;
            if (spd > 0f) speed = spd;
            float py = player != null ? player.position.y : 0f;
            EdgeY = py - Dir * Mathf.Abs(startGap);
            SetVisible(true);
        }

        public void Stop()
        {
            Active = false;
            _st = St.Idle;
            SetVisible(false);
        }

        void SetVisible(bool v)
        {
            if (_fill != null) _fill.enabled = v;
            if (_edge != null) _edge.enabled = v;
        }

        void LateUpdate()
        {
            if (!Active || player == null) return;
            float dt = Time.deltaTime;
            _settle = Mathf.Max(0f, _settle - dt);

            if (_st == St.Advancing && _settle <= 0f)
            {
                EdgeY += Dir * speed * dt;
                bool caught = Dir > 0 ? EdgeY >= player.position.y - catchMargin
                                      : EdgeY <= player.position.y + catchMargin;
                if (caught) Catch();
            }
            else if (_st == St.Caught)
            {
                bool advanced = Dir > 0 ? player.position.y >= _resumeThreshold
                                        : player.position.y <= _resumeThreshold;
                if (advanced)
                {
                    _st = St.Advancing;
                    EdgeY = player.position.y - Dir * Mathf.Abs(startGap);
                }
            }

            LayoutSprite();
        }

        void Catch()
        {
            var gs = GameState.Instance;
            if (gs != null) gs.Spend(catchCost);

            if (gs != null && gs.Depleted)
            {
                _st = St.Done;
                EdgeY = player.position.y;           // 完全に覆う
                LayoutSprite();
                OnFinalSwallow?.Invoke();
                return;
            }

            OnCaught?.Invoke(catchCost);

            // 安全な足場(中央寄り)へ戻す
            float safeBaseY = EdgeY + Dir * 3.0f;
            Vector2 spot = FindSafeSpot(safeBaseY);
            if (_prb != null)
            {
                _prb.linearVelocity = Vector2.zero;
                _prb.position = spot;
            }
            player.position = spot;
            Physics2D.SyncTransforms();

            _resumeThreshold = spot.y + Dir * advanceToResume;
            EdgeY = spot.y - Dir * Mathf.Abs(startGap) * 0.7f;
            _st = St.Caught;
            _settle = 0.4f;
        }

        /// <summary>safeBaseY 付近で、逃げる向きにある足場の上(または下)＝中央寄りの安全地点。</summary>
        Vector2 FindSafeSpot(float baseY)
        {
            float[] xs = { 0f, -0.9f, 0.9f, -1.8f, 1.8f };
            foreach (float x in xs)
            {
                Vector2 from = new Vector2(x, baseY + Dir * 4f);
                var hit = Physics2D.Raycast(from, Dir > 0 ? Vector2.down : Vector2.up, 8f, _groundMask);
                if (hit.collider != null)
                    return new Vector2(x, hit.point.y + Dir * 0.22f);
            }
            return new Vector2(0f, baseY);
        }

        void LayoutSprite()
        {
            if (_fill == null || viewCamera == null) return;
            float camY = viewCamera.transform.position.y;
            float halfH = viewCamera.orthographicSize;
            float far = Dir > 0 ? camY - halfH - 60f : camY + halfH + 60f;   // 画面外
            float near = EdgeY;
            float mid = (far + near) * 0.5f;
            float h = Mathf.Max(0.01f, Mathf.Abs(near - far));
            float w = halfH * 4f + 60f;

            _fill.transform.position = new Vector3(0f, mid, 0f);
            _fill.transform.localScale = new Vector3(w / 0.5f, h / 0.5f, 1f);   // white.png = 0.5u

            if (_edge != null)
            {
                _edge.transform.position = new Vector3(0f, near, 0.1f);
                _edge.transform.localScale = new Vector3(w / 0.5f, 0.06f / 0.5f, 1f);
            }
        }
    }
}
