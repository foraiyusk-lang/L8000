using UnityEngine;

namespace L8000
{
    /// <summary>
    /// プレイヤーを縦中央付近に保つ。小さなデッドゾーン内では動かず、外れたら追う(=常時ではないが極力中央)。
    /// 左右は追従しない(X はロール中心に固定)。スタート地点の床より下・ロール頂上より上は映さない。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public RollStreamer roll;

        [Header("ズーム")]
        [Tooltip(">0 なら Awake で camera.orthographicSize をこの値にする。小さいほど文字が大きく見える。")]
        public float orthoSize = 8f;

        [Header("追従")]
        public float smoothTime = 0.14f;
        public float verticalDeadzone = 0.4f;
        public float fixedX = 0f;

        [Header("クランプ")]
        public float bottomPadding = 1f;
        public float topPadding = 2f;

        [Header("ドット")]
        public bool pixelSnap = true;
        public float pixelsPerUnit = 16f;

        Camera _cam;
        float _vel;
        float _desiredY;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (orthoSize > 0f) _cam.orthographicSize = orthoSize;
            if (target == null)
            {
                var p = FindFirstObjectByType<PlayerController>();
                if (p != null) target = p.transform;
            }
            _desiredY = target != null ? target.position.y : 0f;
        }

        void LateUpdate()
        {
            if (target == null) return;

            float ty = target.position.y;
            if (ty > _desiredY + verticalDeadzone) _desiredY = ty - verticalDeadzone;
            else if (ty < _desiredY - verticalDeadzone) _desiredY = ty + verticalDeadzone;

            float y = Mathf.SmoothDamp(transform.position.y, _desiredY, ref _vel, smoothTime);

            if (_cam.orthographic && roll != null)
            {
                float halfH = _cam.orthographicSize;
                float minCenter = roll.BottomY - bottomPadding + halfH;
                float maxCenter = roll.TopY + topPadding - halfH;
                if (maxCenter < minCenter) maxCenter = minCenter;
                y = Mathf.Clamp(y, minCenter, maxCenter);
            }

            float x = fixedX;
            if (pixelSnap && pixelsPerUnit > 0f)
            {
                y = Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit;
                x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
            }
            transform.position = new Vector3(x, y, transform.position.z);
        }
    }
}
