using UnityEngine;

namespace L8000
{
    /// <summary>
    /// カメラは自動で上へ流れ続ける(どんどん上にスクロール)。同時にプレイヤーが上にいれば追う。
    /// カメラの Y は下がらない(単調増加)。左右は追従しない。ロール頂上でクランプ。
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
        [Tooltip("カメラが自動で上へ流れる速さ(world units/秒)。0 で自動スクロール無効。")]
        public float autoScrollSpeed = 1.25f;

        [Tooltip("開始からこの秒数だけ自動スクロールを待つ。")]
        public float autoScrollDelay = 1.5f;

        [Header("プレイヤー追従")]
        [Tooltip("プレイヤーがこの位置(画面中央から上方向のオフセット)より上に行ったらカメラも追う。")]
        public float followAheadOffset = 1.5f;
        public float followSmooth = 0.12f;
        public float fixedX = 0f;

        [Header("クランプ")]
        public float bottomPadding = 1f;
        public float topPadding = 1.5f;

        [Header("ドット")]
        public bool pixelSnap = true;
        public float pixelsPerUnit = 16f;

        Camera _cam;
        float _scrollY;      // 自動スクロールで積み上がる下限
        float _y;            // 実際のカメラ Y(スムージング用)
        float _vel;
        float _elapsed;

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
        }

        void LateUpdate()
        {
            _elapsed += Time.deltaTime;
            if (autoScrollSpeed > 0f && _elapsed > autoScrollDelay)
                _scrollY += autoScrollSpeed * Time.deltaTime;

            // プレイヤーが十分上にいれば、その位置も下限に加える
            float desired = _scrollY;
            if (target != null)
            {
                float followY = target.position.y - followAheadOffset;
                if (followY > desired) desired = followY;
            }

            // クランプ(ロール頂上より上は映さない / 下限)
            if (_cam.orthographic && roll != null)
            {
                float halfH = _cam.orthographicSize;
                float minCenter = roll.BottomY - bottomPadding + halfH;
                float maxCenter = roll.TopY + topPadding - halfH;
                if (maxCenter < minCenter) maxCenter = minCenter;
                desired = Mathf.Clamp(desired, minCenter, maxCenter);
                if (_scrollY > maxCenter) _scrollY = maxCenter;
            }

            _y = Mathf.SmoothDamp(_y, desired, ref _vel, followSmooth);
            if (_y < desired && desired - _y < 0.001f) _y = desired;

            float x = fixedX;
            float y = _y;
            if (pixelSnap && pixelsPerUnit > 0f)
            {
                y = Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit;
                x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
            }
            transform.position = new Vector3(x, y, transform.position.z);
        }

        /// <summary>いま画面に映っている一番下の world Y(足場の生成/破棄やフェイル判定に使える)。</summary>
        public float ViewBottomY => _y - (_cam != null ? _cam.orthographicSize : 0f);
    }
}
