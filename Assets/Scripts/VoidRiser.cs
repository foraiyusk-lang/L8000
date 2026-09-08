using UnityEngine;

namespace L8000
{
    /// <summary>
    /// 下からせり上がる「無」。残り回数が尽きる(GameState.OnDepleted)と発動し、
    /// プレイヤーの下からせり上がって飲み込む。飲み込んだ瞬間 OnSwallowed。
    /// アウトロ(頂上タイトル発動)からも Trigger() で呼ばれ、降りてくるプレイヤーと合流する。
    /// </summary>
    public class VoidRiser : MonoBehaviour
    {
        public Transform player;
        public Camera viewCamera;

        [Tooltip("発動時、プレイヤーの何 world 下から現れるか。")]
        public float startGapBelowPlayer = 9f;

        [Tooltip("せり上がる速さ(world units / 秒)。")]
        public float riseSpeed = 3.5f;

        [Tooltip("この距離まで迫ったら『飲まれた』。")]
        public float catchMargin = 0.3f;

        public bool Active { get; private set; }
        public bool Swallowed { get; private set; }
        public float TopY { get; private set; }

        public static System.Action OnSwallowed;

        SpriteRenderer _sr;

        void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            if (viewCamera == null) viewCamera = Camera.main;
            gameObject.SetActive(true);
            if (_sr != null) _sr.enabled = false;
        }

        void OnEnable() => GameState.OnDepleted += TriggerFromDepletion;
        void OnDisable() => GameState.OnDepleted -= TriggerFromDepletion;

        void TriggerFromDepletion() => Trigger(riseSpeed * 1.6f);

        /// <summary>「無」をせり上がらせ始める。</summary>
        public void Trigger(float speed = -1f)
        {
            if (Active) return;
            Active = true;
            if (speed > 0f) riseSpeed = speed;
            float py = player != null ? player.position.y : 0f;
            TopY = py - Mathf.Abs(startGapBelowPlayer);
            if (_sr != null) _sr.enabled = true;
        }

        void LateUpdate()
        {
            if (!Active) return;

            if (!Swallowed)
            {
                TopY += riseSpeed * Time.deltaTime;
                float py = player != null ? player.position.y : TopY;
                if (TopY >= py - catchMargin)
                {
                    TopY = py;
                    Swallowed = true;
                    OnSwallowed?.Invoke();
                }
            }
            else
            {
                // 飲み込んだ後もゆっくり画面を覆い続ける
                TopY += riseSpeed * 0.4f * Time.deltaTime;
            }

            LayoutSprite();
        }

        void LayoutSprite()
        {
            if (_sr == null || viewCamera == null) return;
            float camY = viewCamera.transform.position.y;
            float halfH = viewCamera.orthographicSize;
            float bottom = camY - halfH - 40f;   // 画面下端よりずっと下から
            float top = TopY;
            float h = Mathf.Max(0.01f, top - bottom);
            var t = _sr.transform;
            t.position = new Vector3(0f, (top + bottom) * 0.5f, 0f);
            // white.png は 8px @ PPU16 = 0.5 unit。横は画面より十分広く。
            t.localScale = new Vector3((halfH * 4f + 40f) / 0.5f, h / 0.5f, 1f);
        }
    }
}
