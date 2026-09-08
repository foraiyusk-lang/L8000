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

        void OnEnable()
        {
            GameState.OnChanged += Refresh;
            if (GameState.Instance != null) Refresh(GameState.Instance.Remaining);
        }

        void OnDisable() => GameState.OnChanged -= Refresh;

        void Refresh(int remaining)
        {
            if (_killed || label == null) return;
            label.text = string.Format(format, remaining);
        }

        /// <summary>UI 担当破壊 → 残り回数表示を永久に消す。</summary>
        public void Kill()
        {
            _killed = true;
            if (label != null) label.gameObject.SetActive(false);
        }
    }
}
