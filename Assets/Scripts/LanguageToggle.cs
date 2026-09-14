using UnityEngine;
using UnityEngine.UI;

namespace L8000
{
    /// <summary>
    /// タイトル右上の「日本語 / English」切り替えボタン。設定パネルには置かない独立UI。
    /// 押すたびに Localization.Toggle() でゲーム全体の言語(HUD・トースト・終幕など)を切り替える。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LanguageToggle : MonoBehaviour
    {
        void Awake() => GetComponent<Button>().onClick.AddListener(Localization.Toggle);
    }
}
