using System.Collections;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// ゲーム全体の進行:
    ///   Climb   … 下から無がせり上がる / 射撃可 / BGM 逆再生
    ///   Rewind  … 頂上タイトル接触 → 「巻き戻しが止まる」演出 → BGM 順再生へ
    ///   Descent … 自動で降りる / 射撃不可 / 上から無 / 斜線の行は素通り
    ///   Bottom  … 最下部 ===== に到達 → 下向き射撃解禁 → 撃ち抜くと下へ
    ///   → Finale
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public Transform player;
        public PlayerController playerController;
        public Weapon weapon;
        public CameraFollow cameraFollow;
        public Camera viewCamera;
        public RollStreamer streamer;
        public VoidRiser voidRiser;
        public Finale finale;
        public Transform clockPivot;   // 時計の針(回転させる)

        [Header("登り")]
        public float climbGrace = 7f;
        public float climbVoidSpeed = 2.4f;

        [Header("降り")]
        public float descentSpeed = 6.5f;
        public float descentNudge = 3.5f;
        public float descentVoidSpeed = 3.0f;

        enum Phase { Climb, Rewind, Descent, Bottom, Done }
        Phase _phase = Phase.Climb;
        float _t;
        Rigidbody2D _prb;
        bool _bottomBroken;

        void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (player != null) _prb = player.GetComponent<Rigidbody2D>();
        }

        void Start()
        {
            if (weapon != null) { weapon.CanFire = true; weapon.FireDown = false; }
            if (clockPivot != null) clockPivot.gameObject.SetActive(false);
            StartCoroutine(InitAudio());
        }

        IEnumerator InitAudio()
        {
            yield return null;                          // 全 Start の後に
            AudioManager.Instance?.SetReversed(true);   // 登り = 逆再生
        }

        void OnEnable()
        {
            VoidRiser.OnFinalSwallow += OnFinalSwallow;
            CreditLineBuilder.OnBottomBroken += OnBottomBroken;
        }
        void OnDisable()
        {
            VoidRiser.OnFinalSwallow -= OnFinalSwallow;
            CreditLineBuilder.OnBottomBroken -= OnBottomBroken;
        }

        void OnFinalSwallow()
        {
            _phase = Phase.Done;
            finale?.Begin(true);
        }

        void OnBottomBroken()
        {
            // 最下部を撃ち抜いた → 安全床も外して下へ落とす
            var sf = GameObject.Find("SafetyFloor");
            if (sf != null) sf.SetActive(false);
            _ruleOpen = true;
        }
        bool _ruleOpen;

        void Update()
        {
            _t += Time.deltaTime;
            if (streamer == null || player == null) return;

            switch (_phase)
            {
                case Phase.Climb: TickClimb(); break;
                case Phase.Descent: TickDescent(); break;
                case Phase.Bottom: TickBottom(); break;
            }
        }

        // --- Climb ---------------------------------------------------------
        void TickClimb()
        {
            if (_t > climbGrace && !voidRiser.Active)
                voidRiser.Trigger(VoidMode.RiseFromBelow, climbVoidSpeed);

            if (player.position.y >= streamer.TopY - 0.6f)
                StartCoroutine(RewindCo());
        }

        // --- Rewind (頂上タイトル接触) ------------------------------------
        IEnumerator RewindCo()
        {
            _phase = Phase.Rewind;

            if (playerController != null) playerController.enabled = false;
            if (_prb != null) _prb.linearVelocity = Vector2.zero;
            if (weapon != null) weapon.CanFire = false;
            voidRiser.Stop();

            ScreenFx.Toast("― 巻き戻しが 止まる ―", new Color(0.85f, 0.9f, 1f), 3.0f);

            // 時計の針: 速く逆回転 → 減速 → 12時で停止
            if (clockPivot != null)
            {
                clockPivot.gameObject.SetActive(true);
                float ang = 0f, spin = 900f;   // deg/s (逆回転)
                float dur = 2.6f, e = 0f;
                while (e < dur)
                {
                    e += Time.deltaTime;
                    spin = Mathf.Lerp(900f, 0f, e / dur);
                    ang -= spin * Time.deltaTime;
                    clockPivot.localRotation = Quaternion.Euler(0, 0, ang);
                    yield return null;
                }
                // 12時(上向き)へスナップ
                float cur = clockPivot.localEulerAngles.z;
                for (float k = 0; k < 1f; k += Time.deltaTime * 3f)
                {
                    clockPivot.localRotation = Quaternion.Slerp(Quaternion.Euler(0, 0, cur), Quaternion.identity, k);
                    yield return null;
                }
                clockPivot.localRotation = Quaternion.identity;
            }

            ScreenFx.Flash(new Color(1f, 1f, 1f, 0.85f), 0.7f);
            AudioManager.Instance?.CrossfadeToForward(1.6f);   // 逆→順(途中で一瞬止まる)
            yield return new WaitForSeconds(0.9f);

            if (clockPivot != null) clockPivot.gameObject.SetActive(false);
            BeginDescent();
        }

        // --- Descent -----------------------------------------------------
        void BeginDescent()
        {
            _phase = Phase.Descent;
            if (cameraFollow != null) cameraFollow.enabled = false;
            if (_prb != null) { _prb.bodyType = RigidbodyType2D.Kinematic; _prb.linearVelocity = Vector2.zero; }

            CreditLineBuilder.DescentDropDestroyed = true;
            streamer.RefreshAll();   // 既存の斜線行の当たり判定を落とす

            voidRiser.Stop();
            voidRiser.Trigger(VoidMode.FallFromAbove, descentVoidSpeed);
        }

        void TickDescent()
        {
            float dt = Time.deltaTime;
            var p = player.position;
            float dir = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            p.x = Mathf.Clamp(p.x + dir * descentNudge * dt, -7f, 7f);
            p.y = Mathf.Max(streamer.BottomY + 0.3f, p.y - descentSpeed * dt);
            player.position = p;
            if (_prb != null) _prb.position = p;

            var c = viewCamera.transform.position;
            c.y = Mathf.Lerp(c.y, p.y + 1.0f, 6f * dt);
            viewCamera.transform.position = c;

            if (p.y <= streamer.BottomY + 0.35f) BeginBottom();
        }

        // --- Bottom (===== 突破) ---------------------------------------
        void BeginBottom()
        {
            _phase = Phase.Bottom;
            voidRiser.Stop();
            if (_prb != null) { _prb.bodyType = RigidbodyType2D.Dynamic; _prb.linearVelocity = Vector2.zero; }
            if (playerController != null) playerController.enabled = true;
            if (weapon != null) { weapon.CanFire = true; weapon.FireDown = true; }
            ScreenFx.Toast("↓ Enter で 下へ", new Color(0.85f, 0.9f, 1f), 3.5f);
        }

        void TickBottom()
        {
            if (_ruleOpen && player.position.y < streamer.BottomY - 1.2f && !_bottomBroken)
            {
                _bottomBroken = true;
                _phase = Phase.Done;
                if (playerController != null) playerController.enabled = false;
                if (weapon != null) weapon.CanFire = false;
                finale?.Begin(false);
            }
        }
    }
}
