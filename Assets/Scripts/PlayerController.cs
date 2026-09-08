using UnityEngine;

namespace L8000
{
    /// <summary>
    /// 白い正方形。A=左 / D=右 / W=ジャンプ(2段可) / Enter=発射(Weapon が担当)。
    /// すべての足場はソリッド。空白の隙間を縫って登る。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移動")]
        [Tooltip("最高速度。小さいほど微調整しやすい。")]
        public float moveSpeed = 4.2f;
        [Tooltip("加速。小さいほどタップで少しだけ動く。")]
        public float groundAccel = 40f;
        public float airAccel = 26f;
        [Tooltip("キーを離したときの減速。大きいほどピタッと止まる。")]
        public float groundDecel = 55f;

        [Header("ジャンプ")]
        [Tooltip("到達高 ≈ jumpForce^2 / (2 * 9.81 * gravityScale)。maxLineGap の約1.6倍が目安(易しめ)。")]
        public float jumpForce = 13.5f;
        [Tooltip("空中ジャンプ(2段目)の強さ倍率。")]
        public float airJumpMult = 0.92f;
        [Tooltip("地上を含めた最大ジャンプ回数。2 = 2段ジャンプ。")]
        public int maxJumps = 2;
        public float gravityScale = 3.0f;
        public float fallGravityMult = 1.45f;
        [Tooltip("ジャンプ中にキーを離すと落下を早める倍率。1で無効(タップでもフル高さ)。")]
        public float lowJumpMult = 1.0f;
        public float maxFallSpeed = 18f;
        public float coyoteTime = 0.15f;
        public float jumpBuffer = 0.18f;

        [Header("接地判定")]
        public Vector2 groundCheckSize = new Vector2(0.6f, 0.16f);
        public float groundCheckOffsetY = -0.38f;
        public LayerMask groundMask;

        [Header("縁つかまり救済")]
        [Tooltip("ジャンプで足場の角にかすったら自動で上に乗る。乗れる段差の上限(world units)。0で無効。")]
        public float ledgeMountMax = 0.6f;
        [Tooltip("プレイヤーの当たり判定サイズ(縁つかまり判定に使用)。")]
        public Vector2 bodySize = new Vector2(0.26f, 0.36f);

        [Header("キー")]
        public KeyCode leftKey = KeyCode.A;
        public KeyCode rightKey = KeyCode.D;
        public KeyCode jumpKey = KeyCode.W;

        /// <summary>検証用: true の間、接地するたびに自動でジャンプし続ける。</summary>
        public static bool DebugAutoClimb;
        /// <summary>検証用: -1/0/1 で左右入力を上書き。</summary>
        public static int DebugAutoWalk;
        /// <summary>検証用: 1フレームだけジャンプ入力を発生させる(空中でも)。</summary>
        public static bool DebugForceJump;

        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        public bool IsGrounded => _grounded;

        Rigidbody2D _rb;
        bool _grounded;
        float _coyote;
        float _buffer;
        float _autoClimbCd;
        int _jumpsLeft;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.freezeRotation = true;
            _rb.gravityScale = gravityScale;
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;   // 停止中スリープ → 入力で起きない、を防ぐ
            if (groundMask.value == 0)
            {
                int g = LayerMask.NameToLayer("Ground");
                groundMask = g >= 0 ? (1 << g) : Physics2D.AllLayers;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(jumpKey)) _buffer = jumpBuffer;
            if (DebugForceJump) { _buffer = jumpBuffer; DebugForceJump = false; }

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
            Collider2D hit = Physics2D.OverlapBox(checkPos, groundCheckSize, 0f, groundMask);
            _grounded = hit != null && _rb.linearVelocity.y <= 0.1f;

            _coyote = _grounded ? coyoteTime : Mathf.Max(0f, _coyote - dt);
            _buffer = Mathf.Max(0f, _buffer - dt);
            if (_grounded) _jumpsLeft = maxJumps;

            // --- 横移動: 入力中は accel、離したら decel でピタッと ---
            float dir = (Input.GetKey(rightKey) ? 1f : 0f) - (Input.GetKey(leftKey) ? 1f : 0f);
            if (DebugAutoWalk != 0) dir = DebugAutoWalk;
            float targetX = dir * moveSpeed;
            float rate = Mathf.Abs(dir) > 0.01f ? (_grounded ? groundAccel : airAccel)
                                                : (_grounded ? groundDecel : airAccel);
            float vx = Mathf.MoveTowards(_rb.linearVelocity.x, targetX, rate * dt);
            float vy = _rb.linearVelocity.y;

            // --- ジャンプ(地上 + 空中2段目) ---
            bool wantJump = _buffer > 0f;
            bool canGroundJump = _coyote > 0f;
            bool canAirJump = !canGroundJump && _jumpsLeft > 0 && !_grounded;
            if (wantJump && (canGroundJump || canAirJump))
            {
                vy = canAirJump ? jumpForce * airJumpMult : jumpForce;
                _buffer = 0f;
                _coyote = 0f;
                _jumpsLeft = Mathf.Max(0, _jumpsLeft - 1);
            }

            float g = Physics2D.gravity.y * gravityScale;
            if (vy < 0f) vy += g * (fallGravityMult - 1f) * dt;
            else if (vy > 0f && !Input.GetKey(jumpKey)) vy += g * (lowJumpMult - 1f) * dt;
            vy = Mathf.Max(vy, -maxFallSpeed);

            _rb.linearVelocity = new Vector2(vx, vy);

            // 縁つかまり救済: 進行方向へ動いていて、ジャンプが頂点付近〜落下ぎみのときだけ。
            // (高速で横に擦っただけで瞬間移動する誤作動を防ぐ)
            if (!_grounded && vy < 2.5f && Mathf.Abs(dir) > 0.1f) TryLedgeMount(dir);
        }

        /// <summary>足場の角に引っかかったら自動で上に乗せる(あと少しで届いたジャンプの救済)。</summary>
        void TryLedgeMount(float dir)
        {
            if (ledgeMountMax <= 0f) return;
            Vector2 pos = _rb.position;
            float halfW = bodySize.x * 0.5f;
            float halfH = bodySize.y * 0.5f;
            float feetY = pos.y - halfH;

            // 進行方向のみ(呼び出し側で dir != 0 を保証)
            int[] dirs = { (int)Mathf.Sign(dir) };
            foreach (int s in dirs)
            {
                Vector2 probe = new Vector2(pos.x + s * (halfW + 0.08f), feetY + ledgeMountMax * 0.5f + 0.02f);
                var hit = Physics2D.OverlapBox(probe, new Vector2(0.14f, ledgeMountMax + 0.04f), 0f, groundMask);
                if (hit == null) continue;

                float top = hit.bounds.max.y;
                float step = top - feetY;
                if (step <= 0.02f || step > ledgeMountMax) continue;

                // 乗った先にプレイヤーが収まる空きがあるか
                Vector2 landCenter = new Vector2(pos.x + s * (halfW + 0.05f), top + halfH + 0.03f);
                if (Physics2D.OverlapBox(landCenter, new Vector2(bodySize.x * 0.85f, bodySize.y * 0.9f), 0f, groundMask) != null)
                    continue;

                _rb.position = landCenter;
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 1.2f);
                _grounded = true;
                _jumpsLeft = maxJumps;
                return;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + new Vector3(0f, groundCheckOffsetY, 0f),
                new Vector3(groundCheckSize.x, groundCheckSize.y, 0.1f));
        }
    }
}
