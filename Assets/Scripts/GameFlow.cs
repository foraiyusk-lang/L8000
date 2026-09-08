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
        public UnityEngine.UI.Image dim;  // 巻き戻し中に背景を暗く(Clock の子・HUD Canvas 上)
        public Transform clockRoot;    // 時計まるごと(HUD Canvas 上・画面中央固定)
        public Transform clockPivot;   // 時計の針(回転させる)

        [Header("登り")]
        public float climbGrace = 7f;
        public float climbVoidSpeed = 1.0f;   // ジグザグ登り(端まで歩く)は ~1.5-2.5u/s。動いていれば振り切れる。止まると ~10秒で捕まる(-100・復帰可)。

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
            if (clockRoot != null) clockRoot.gameObject.SetActive(false);
            StartCoroutine(InitAudio());
        }

        IEnumerator InitAudio()
        {
            yield return null;                          // 全 Start の後に
            AudioManager.Instance?.SetReversed(true);   // 登り = 逆再生
        }

        static IEnumerator FadeGraphic(UnityEngine.UI.Graphic g, float from, float to, float dur)
        {
            float e = 0f;
            var c = g.color;
            while (e < dur && g != null)
            {
                e += Time.deltaTime;
                c.a = Mathf.Lerp(from, to, e / dur);
                g.color = c;
                yield return null;
            }
            if (g != null) { c.a = to; g.color = c; }
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
            if (cameraFollow != null) cameraFollow.enabled = false;   // カメラを固定
            if (viewCamera != null) viewCamera.transform.rotation = Quaternion.identity;  // 傾き(Cinematics破壊)を戻す
            if (_prb != null) _prb.linearVelocity = Vector2.zero;
            if (weapon != null) weapon.CanFire = false;
            voidRiser.Stop();

            ScreenFx.Toast("― 巻き戻しが 止まる ―", new Color(0.85f, 0.9f, 1f), 3.4f);
            AudioManager.Instance?.CrossfadeToForward(2.8f);   // 逆→順(途中で一瞬止まる)

            // 背景を暗く(時計を見せる。HUD Canvas 上なので確実に前面)
            if (dim != null) StartCoroutine(FadeGraphic(dim, 0f, 0.78f, 0.5f));

            // 時計を表示(画面中央固定・追従不要)
            if (clockRoot != null) clockRoot.gameObject.SetActive(true);

            // 針: 速い逆回転 → 減速 → 12時でスナップ停止
            if (clockPivot != null)
            {
                float ang = clockPivot.localEulerAngles.z;
                float dur = 2.4f, e = 0f;
                while (e < dur)
                {
                    e += Time.deltaTime;
                    float spin = Mathf.Lerp(720f, 0f, Mathf.SmoothStep(0f, 1f, e / dur));
                    ang -= spin * Time.deltaTime;                   // 逆回転
                    clockPivot.localRotation = Quaternion.Euler(0, 0, ang);
                    yield return null;
                }
                // 12時(上向き)へなめらかに
                var from = clockPivot.localRotation;
                for (float k = 0; k < 1f; k += Time.deltaTime * 2.2f)
                {
                    clockPivot.localRotation = Quaternion.Slerp(from, Quaternion.identity, Mathf.SmoothStep(0f, 1f, k));
                    yield return null;
                }
                clockPivot.localRotation = Quaternion.identity;
            }

            ScreenFx.Flash(new Color(1f, 1f, 1f, 0.85f), 0.7f);
            if (dim != null) StartCoroutine(FadeGraphic(dim, dim.color.a, 0f, 0.6f));
            yield return new WaitForSeconds(0.7f);

            if (clockRoot != null) clockRoot.gameObject.SetActive(false);
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

            ScreenFx.Banner("▼ もどっている", new Color(0.7f, 0.76f, 0.88f, 1f));
        }

        void TickDescent()
        {
            float dt = Time.deltaTime;
            var p = player.position;
            float dir = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            p.x = Mathf.Clamp(p.x + dir * descentNudge * dt, -7f, 7f);
            float target = streamer.BottomY + 0.9f;   // ===== の上でぴたっと止まる
            p.y = Mathf.Max(target, p.y - descentSpeed * dt);
            player.position = p;
            if (_prb != null) _prb.position = p;

            var c = viewCamera.transform.position;
            c.y = Mathf.Lerp(c.y, p.y + 1.0f, 6f * dt);
            viewCamera.transform.position = c;

            if (p.y <= target + 0.05f) BeginBottom();
        }

        // --- Bottom (===== 突破) ---------------------------------------
        void BeginBottom()
        {
            _phase = Phase.Bottom;
            voidRiser.Stop();
            // ===== のすぐ上へスナップして着地
            var p = new Vector3(Mathf.Clamp(player.position.x, -2f, 2f), streamer.BottomY + 0.9f, 0f);
            player.position = p;
            if (_prb != null) { _prb.bodyType = RigidbodyType2D.Dynamic; _prb.position = p; _prb.linearVelocity = Vector2.zero; }
            Physics2D.SyncTransforms();
            if (playerController != null) playerController.enabled = true;
            if (weapon != null) { weapon.CanFire = true; weapon.FireDown = true; }
            var cam = viewCamera.transform.position; cam.y = streamer.BottomY + 2.5f; viewCamera.transform.position = cam;
            ScreenFx.Banner(null, Color.white);
            ScreenFx.Toast("↓ Enter で この線を 抜ける", new Color(0.85f, 0.9f, 1f), 4f);
            ScreenFx.Banner("↓ Enter", new Color(0.7f, 0.76f, 0.88f, 1f));
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
