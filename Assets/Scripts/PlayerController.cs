using UnityEngine;

namespace L8000
{
    /// <summary>
    /// 白い正方形。A=左 / D=右 / W=ジャンプ / Enter=発射(今回はフックのみ)。
    /// one-way 足場の上に着地でき、下からはすり抜けて上の行に乗れる。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移動")]
        public float moveSpeed = 6.5f;
        public float groundAccel = 70f;
        public float airAccel = 35f;

        [Header("ジャンプ")]
        public float jumpForce = 15f;
        public float gravityScale = 3.0f;
        public float fallGravityMult = 1.4f;
        [Tooltip("ジャンプ中にキーを離すと落下を早める倍率。1で無効(タップでもフル高さ)。")]
        public float lowJumpMult = 1.0f;
        public float maxFallSpeed = 18f;
        public float coyoteTime = 0.08f;
        public float jumpBuffer = 0.10f;

        [Header("接地判定")]
        public Vector2 groundCheckSize = new Vector2(0.6f, 0.16f);
        public float groundCheckOffsetY = -0.38f;
        public LayerMask groundMask;

        [Header("キー")]
        public KeyCode leftKey = KeyCode.A;
        public KeyCode rightKey = KeyCode.D;
        public KeyCode jumpKey = KeyCode.W;
        // 発射(Enter)は Weapon コンポーネントが担当

        /// <summary>検証用: true の間、接地するたびに自動でジャンプし続ける(登り続ける)。</summary>
        public static bool DebugAutoClimb;

        Rigidbody2D _rb;
        bool _grounded;
        float _coyote;
        float _buffer;
        float _autoClimbCd;

        public bool IsGrounded => _grounded;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.freezeRotation = true;
            _rb.gravityScale = gravityScale;
            if (groundMask.value == 0)
            {
                int g = LayerMask.NameToLayer("Ground");
                groundMask = g >= 0 ? (1 << g) : Physics2D.AllLayers;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(jumpKey)) _buffer = jumpBuffer;

            // 検証用オートクライム: 接地して落ち着いてから1回だけジャンプ(人間の操作に近づける)
            _autoClimbCd = Mathf.Max(0f, _autoClimbCd - Time.deltaTime);
            if (DebugAutoClimb && _grounded && _autoClimbCd <= 0f)
            {
                _buffer = jumpBuffer;
                _autoClimbCd = 0.35f;
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            Vector2 checkPos = (Vector2)transform.position + new Vector2(0f, groundCheckOffsetY);
            bool overlap = Physics2D.OverlapBox(checkPos, groundCheckSize, 0f, groundMask) != null;
            // 上昇中(one-way 足場をすり抜けている最中)は接地とみなさない → 空中連続ジャンプを防ぐ
            _grounded = overlap && _rb.linearVelocity.y <= 0.1f;

            _coyote = _grounded ? coyoteTime : Mathf.Max(0f, _coyote - dt);
            _buffer = Mathf.Max(0f, _buffer - dt);

            float dir = (Input.GetKey(rightKey) ? 1f : 0f) - (Input.GetKey(leftKey) ? 1f : 0f);
            float targetX = dir * moveSpeed;
            float accel = _grounded ? groundAccel : airAccel;
            float vx = Mathf.MoveTowards(_rb.linearVelocity.x, targetX, accel * dt);
            float vy = _rb.linearVelocity.y;

            if (_buffer > 0f && _coyote > 0f)
            {
                vy = jumpForce;
                _buffer = 0f;
                _coyote = 0f;
            }

            float g = Physics2D.gravity.y * gravityScale;
            if (vy < 0f) vy += g * (fallGravityMult - 1f) * dt;
            else if (vy > 0f && !Input.GetKey(jumpKey)) vy += g * (lowJumpMult - 1f) * dt;
            vy = Mathf.Max(vy, -maxFallSpeed);

            _rb.linearVelocity = new Vector2(vx, vy);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + new Vector3(0f, groundCheckOffsetY, 0f),
                new Vector3(groundCheckSize.x, groundCheckSize.y, 0.1f));
        }
    }
}
