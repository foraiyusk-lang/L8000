using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace L8000
{
    /// <summary>
    /// タイトル画面。ロゴ / フレーバー1行 / なまえ入力 / PLAY / 設定(音量)。
    /// 静かで暗い。世界観を壊さない最小限の UI。
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public SpriteRenderer logo;           // ピクセルアートのロゴ(無ければ logoText を使う)
        public TextMeshPro logoText;
        public TextMeshPro flavor;
        public TMP_InputField nameField;
        public TextMeshProUGUI playLabel;
        public GameObject settingsPanel;
        public Slider bgmSlider;
        public Slider sfxSlider;
        public TextMeshProUGUI hint;

        const string BgmKey = "l8000_bgm";
        const string SfxKey = "l8000_sfx";

        bool _settingsOpen;

        void Start()
        {
            if (nameField != null)
            {
                nameField.text = PlayerName.Value;
                nameField.characterLimit = 16;
                nameField.onSubmit.AddListener(_ => Play());
            }
            if (bgmSlider != null)
            {
                bgmSlider.value = PlayerPrefs.GetFloat(BgmKey, 0.7f);
                bgmSlider.onValueChanged.AddListener(v => { PlayerPrefs.SetFloat(BgmKey, v); Apply(); });
            }
            if (sfxSlider != null)
            {
                sfxSlider.value = PlayerPrefs.GetFloat(SfxKey, 0.8f);
                sfxSlider.onValueChanged.AddListener(v => { PlayerPrefs.SetFloat(SfxKey, v); Apply(); });
            }
            if (settingsPanel != null) settingsPanel.SetActive(false);
            Apply();

            if (AudioManager.Instance != null) AudioManager.Instance.SetReversed(false);   // タイトルは順再生
        }

        void Update()
        {
            // Enter でプレイ(名前入力中の Enter は onSubmit 経由)
            if (!_settingsOpen && Input.GetKeyDown(KeyCode.Return)
                && (nameField == null || !nameField.isFocused))
                Play();
        }

        void Apply()
        {
            if (AudioManager.Instance == null) return;
            AudioManager.Instance.bgmVolume = bgmSlider != null ? bgmSlider.value * 0.5f : 0.35f;
            AudioManager.Instance.sfxVolume = sfxSlider != null ? sfxSlider.value * 0.7f : 0.55f;
            AudioManager.Instance.ApplyVolumes();
        }

        public void ToggleSettings()
        {
            _settingsOpen = !_settingsOpen;
            if (settingsPanel != null) settingsPanel.SetActive(_settingsOpen);
        }

        public void Play()
        {
            string n = nameField != null ? nameField.text.Trim() : "";
            if (string.IsNullOrEmpty(n))
            {
                if (hint != null) StartCoroutine(FlashHint("なまえ を いれてください"));
                if (nameField != null) nameField.Select();
                return;
            }
            PlayerName.Value = n;
            StartCoroutine(Go());
        }

        IEnumerator Go()
        {
            if (playLabel != null) playLabel.text = "…";
            yield return new WaitForSecondsRealtime(0.4f);
            SceneManager.LoadScene("Main");
        }

        IEnumerator FlashHint(string msg)
        {
            hint.text = msg;
            var c = hint.color; c.a = 1f; hint.color = c;
            yield return new WaitForSecondsRealtime(2f);
            float t = 0f;
            while (t < 1f) { t += Time.unscaledDeltaTime; c.a = 1f - t; hint.color = c; yield return null; }
        }
    }
}
