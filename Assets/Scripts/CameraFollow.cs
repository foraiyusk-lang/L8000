using UnityEngine;

namespace L8000
{
    /// <summary>
    /// カメラはプレイヤーを追って上へ登る。プレイヤーがロールの一定割合(既定=中央)まで
    /// 登ると「自動で上へ流れる(下から迫る)」が始まる。それまでは時間制限なし。
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

        [Header("自動スクロール(下から迫る)")]
        [Tooltip("プレイヤーがロール全高のこの割合まで登ると自動スクロール開始。0.67=2/3地点。")]
        [Range(0f, 1f)] public float scrollStartFraction = 0.67f;

        [Tooltip("上へ流れる速さ(world units/秒)。開始後のみ。")]
        public float autoScrollSpeed = 0.55f;

        [Tooltip("開始条件を満たしてからこの秒数待ってスクロール開始。")]
        public float startDelay = 0.6f;

        [Tooltip("プレイヤーを画面下端からこの距離より下には行かせない(取り残され防止)。")]
        public float keepPlayerAbove = 2f;

        [Tooltip("true にすると最初からスクロール(デバッグ用)。")]
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
            // 古いシーンで未設定(0)のまま来た場合の保険。即開始したいときは autoStart を使う。
            if (scrollStartFraction < 0.02f) scrollStartFraction = 0.5f;
            if (autoStart) BeginScroll();
        }

        public void BeginScroll()
        {
            if (_started) return;
            _started = true;
            _sinceStart = 0f;
        }

        void LateUpdate()
        {
            // 中央まで登ったら自動スクロール開始
            if (!_started && target != null && roll != null && roll.TopY > 1f
                && target.position.y >= roll.TopY * scrollStartFraction)
                BeginScroll();

            if (_started)
            {
                _sinceStart += Time.deltaTime;
                if (_sinceStart > startDelay && autoScrollSpeed > 0f)
                    _scrollY += autoScrollSpeed * Time.deltaTime;
            }

            float halfH = _cam.orthographicSize;
            float desired = _scrollY;
            if (target != null)
            {
                float followY = target.position.y - followAheadOffset;
                if (followY > desired) desired = followY;
                // 取り残され防止: プレイヤーが画面下端に近づいたらカメラを引き戻す
                float minToKeepPlayer = target.position.y + halfH - keepPlayerAbove;
                if (desired > minToKeepPlayer) desired = minToKeepPlayer;
            }

            if (_cam.orthographic && roll != null)
            {
                float minCenter = roll.BottomY - bottomPadding + halfH;
                float maxCenter = roll.TopY + topPadding - halfH;
                if (maxCenter < minCenter) maxCenter = minCenter;
                desired = Mathf.Clamp(desired, minCenter, maxCenter);
                _scrollY = Mathf.Clamp(_scrollY, minCenter, maxCenter);
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
