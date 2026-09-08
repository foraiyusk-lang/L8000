using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// 画面左上の「残り NNNN」表示。UI Designer / UI Programmer を破壊すると消える。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public TextMeshProUGUI label;
        [Tooltip("表示テキストの書式。{0}=残り回数。")]
        public string format = "残り {0}";

        bool _killed;
        int _last = -1;
        Color _baseColor;
        float _flash;

        void OnEnable()
        {
            GameState.OnChanged += Refresh;
            if (label != null) _baseColor = label.color;
            if (GameState.Instance != null) { _last = GameState.Instance.Remaining; Refresh(_last); }
        }

        void OnDisable() => GameState.OnChanged -= Refresh;

        void Update()
        {
            if (_killed || label == null || _flash <= 0f) return;
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 2f);
            label.color = Color.Lerp(_baseColor, new Color(1f, 0.5f, 0.45f), _flash);
        }

        void Refresh(int remaining)
        {
            if (_killed || label == null) return;
            label.text = string.Format(format, remaining);
            if (_last >= 0 && _last - remaining >= 25) _flash = 1f;   // 大きく減ったら赤く点滅(無に捕まった等)
            _last = remaining;
        }

        /// <summary>UI 担当破壊 → 残り回数表示を永久に消す。</summary>
        public void Kill()
        {
            _killed = true;
            if (label != null) label.gameObject.SetActive(false);
        }
    }
}
