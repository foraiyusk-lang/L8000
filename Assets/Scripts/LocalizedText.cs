using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// シーンに固定で置かれた見出し・ボタンラベルなど静的テキストを、
    /// Localization の切り替えに追従させる。TextMeshProUGUI / TextMeshPro どちらにも付けられる。
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Localization のキー。")]
        public string key;

        TMP_Text _text;

        void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            Refresh();
            Localization.OnChanged += Refresh;
        }

        void OnDisable() => Localization.OnChanged -= Refresh;

        void Refresh()
        {
            if (_text == null || string.IsNullOrEmpty(key)) return;
            _text.text = Localization.T(key);
        }
    }
}
