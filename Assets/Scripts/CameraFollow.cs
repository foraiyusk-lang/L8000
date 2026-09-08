using UnityEngine;

namespace L8000
{
    /// <summary>
    /// カメラはプレイヤーを追う。左右は固定、Y だけ滑らかに追従(単調増加)。
    /// ロール境界でクランプ。自動スクロールは廃止(時間圧は「無」に一本化)。
    /// 降り演出中は GameFlow が enabled=false にして直接カメラを動かす。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public RollStreamer roll;

        [Tooltip(">0 なら Awake で camera.orthographicSize をこの値にする。")]
        public float orthoSize = 10f;

        [Tooltip("プレイヤーを画面のどれくらい下寄りに置くか(0.5=中央, 0.35=やや下)。")]
        [Range(0.2f, 0.6f)] public float playerScreenFrac = 0.42f;

        public float followSmooth = 0.14f;
        public float fixedX = 0f;
        public bool monotonicUp = true;   // 登り中はカメラを下げない

        [Header("クランプ")]
        public float bottomPadding = 1.2f;
        public float topPadding = 1.5f;

        [Header("ドット")]
        public bool pixelSnap = true;
        public float pixelsPerUnit = 16f;

        /// <summary>Cinematics 破壊 → フレーム(画面の水平)がゆっくり傾き続ける。</summary>
        public static bool FrameBroken;

        Camera _cam;
        float _y;
        float _vel;
        float _dutch;

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
        }

        void LateUpdate()
        {
            if (target == null) return;
            float halfH = _cam.orthographicSize;

            // プレイヤーを画面の playerScreenFrac の高さに置く
            float desired = target.position.y + halfH * (1f - 2f * playerScreenFrac);

            if (roll != null && _cam.orthographic)
            {
                float minCenter = roll.BottomY - bottomPadding + halfH;
                float maxCenter = roll.TopY + topPadding - halfH;
                if (maxCenter < minCenter) maxCenter = minCenter;
                desired = Mathf.Clamp(desired, minCenter, maxCenter);
            }

            if (monotonicUp && desired < _y) desired = Mathf.Lerp(_y, desired, 0.15f); // 落下時だけ少しだけ下げる

            _y = Mathf.SmoothDamp(_y, desired, ref _vel, followSmooth);

            float x = fixedX, y = _y;
            if (pixelSnap && pixelsPerUnit > 0f)
            {
                y = Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit;
                x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
            }
            transform.position = new Vector3(x, y, transform.position.z);

            // Cinematics 破壊: フレームがゆっくり傾く(±2°ほど、酔わない速さ)
            if (FrameBroken)
            {
                float t = Time.time;
                float tgt = Mathf.Sin(t * 0.23f) * 1.4f + Mathf.Sin(t * 0.11f + 1.3f) * 0.7f;
                _dutch = Mathf.Lerp(_dutch, tgt, 1f - Mathf.Exp(-1.2f * Time.deltaTime));
                transform.rotation = Quaternion.Euler(0f, 0f, _dutch);
            }
        }
    }
}
