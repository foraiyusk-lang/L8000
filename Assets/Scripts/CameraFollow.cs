using UnityEngine;

namespace L8000
{
    /// <summary>
    /// カメラはゲーム開始(スタート足場に乗る)まで動かない。開始後は一定速度で上へ流れ続ける。
    /// カメラ Y は下がらない(単調増加)。左右は追従しない。ロール頂上でクランプ。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public RollStreamer roll;

        [Header("ズーム")]
        [Tooltip(">0 なら Awake で camera.orthographicSize をこの値にする。大きいほど文字が小さく見える。")]
        public float orthoSize = 10f;

        [Header("自動スクロール")]
        [Tooltip("上へ流れる速さ(world units/秒)。ゲーム開始後のみ。")]
        public float autoScrollSpeed = 0.85f;

        [Tooltip("ゲーム開始(スタート足場に乗る)からこの秒数待ってからスクロール開始。")]
        public float startDelay = 0.8f;

        [Tooltip("true にすると開始トリガー無しで最初からスクロール(デバッグ用)。")]
        public bool autoStart = false;

        [Header("プレイヤー追従")]
        [Tooltip("プレイヤーがこの分だけ画面中央より上に行ったらカメラも追う。")]
        public float followAheadOffset = 1.2f;
        public float followSmooth = 0.12f;
        public float fixedX = 0f;

        [Header("クランプ")]
        public float bottomPadding = 1.2f;
        public float topPadding = 1.5f;

        [Header("ドット")]
        public bool pixelSnap = true;
        public float pixelsPerUnit = 16f;

        Camera _cam;
        float _scrollY;
        float _y;
        float _vel;
        bool _started;
        float _sinceStart;

        public bool Started => _started;

        void OnEnable() { PlayerController.StartLineReached += BeginScroll; }
        void OnDisable() { PlayerController.StartLineReached -= BeginScroll; }

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (orthoSize > 0f) _cam.orthographicSize = orthoSize;
            if (target == null)
            {
                var p = FindFirstObjectByType<PlayerController>();
                if (p != null) target = p.transform;
            }
            _y = transform.position.y;
            _scrollY = _y;
            if (autoStart) _started = true;
        }

        public void BeginScroll()
        {
            if (_started) return;
            _started = true;
            _sinceStart = 0f;
        }

        void LateUpdate()
        {
            if (_started)
            {
                _sinceStart += Time.deltaTime;
                if (_sinceStart > startDelay && autoScrollSpeed > 0f)
                    _scrollY += autoScrollSpeed * Time.deltaTime;
            }

            float desired = _scrollY;
            if (target != null)
            {
                float followY = target.position.y - followAheadOffset;
                if (followY > desired) desired = followY;
            }

            if (_cam.orthographic && roll != null)
            {
                float halfH = _cam.orthographicSize;
                float minCenter = roll.BottomY - bottomPadding + halfH;
                float maxCenter = roll.TopY + topPadding - halfH;
                if (maxCenter < minCenter) maxCenter = minCenter;
                desired = Mathf.Clamp(desired, minCenter, maxCenter);
                if (_scrollY > maxCenter) _scrollY = maxCenter;
                if (_scrollY < minCenter) _scrollY = minCenter;
            }

            _y = Mathf.SmoothDamp(_y, desired, ref _vel, followSmooth);

            float x = fixedX;
            float y = _y;
            if (pixelSnap && pixelsPerUnit > 0f)
            {
                y = Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit;
                x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
            }
            transform.position = new Vector3(x, y, transform.position.z);
        }

        /// <summary>いま画面に映っている一番下の world Y。</summary>
        public float ViewBottomY => _y - (_cam != null ? _cam.orthographicSize : 0f);
    }
}
