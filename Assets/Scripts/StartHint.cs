using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>スタート地点の射撃ヒント。プレイヤーが少し登ったら消える。</summary>
    public class StartHint : MonoBehaviour
    {
        public Transform player;
        public float fadeAboveY = 8f;
        public float fadeSpeed = 1.5f;

        TMP_Text _t;
        float _a = 1f;

        void Awake() => _t = GetComponent<TMP_Text>();

        void Update()
        {
            if (_t == null) return;
            bool hide = player != null && player.position.y > fadeAboveY;
            _a = Mathf.MoveTowards(_a, hide ? 0f : 1f, fadeSpeed * Time.deltaTime);
            var c = _t.color; c.a = _a * 0.9f; _t.color = c;
            if (_a <= 0.001f) enabled = false;
        }
    }
}
