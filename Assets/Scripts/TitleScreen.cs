using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace L8000
{
    /// <summary>
    /// タイトル画面。2ステップ:
    ///   1) ロゴ + フレーバー + PLAY + 設定
    ///   2) PLAY を押すと「なまえ入力だけ」の画面。入力して Enter/はじめる でゲームへ。
    /// 静かで暗い。世界観を壊さない最小限の UI。
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Header("ステップ1: タイトル")]
        public SpriteRenderer logo;
        public TextMeshPro logoText;
        public TextMeshPro flavor;
        public GameObject playButton;
        public GameObject settingsButton;
        public GameObject settingsPanel;
        public Slider bgmSlider;
        public Slider sfxSlider;

        [Header("ステップ2: なまえ入力")]
        public GameObject namePanel;      // なまえ入力だけの画面(初期は非表示)
        public TMP_InputField nameField;
        public TextMeshProUGUI hint;

        [Header("互換(未使用可)")]
        public TextMeshProUGUI playLabel;

        [Header("演出")]
        public UnityEngine.UI.Image fadeOverlay;   // 起動時に黒→透明(静かに立ち上げる)
        public float fadeInTime = 0.8f;

        const string BgmKey = "l8000_bgm";
        const string SfxKey = "l8000_sfx";

        bool _settingsOpen;
        int _step;   // 0 = タイトル, 1 = なまえ入力

        void Start()
        {
            if (nameField != null)
            {
                nameField.text = PlayerName.Value;
                nameField.characterLimit = 16;
                nameField.onSubmit.AddListener(_ => Confirm());
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
            if (namePanel != null) namePanel.SetActive(false);
            Apply();

            if (AudioManager.Instance != null) AudioManager.Instance.SetReversed(false);

            SetTitleVisible(true);          // タイトルの各要素を確実に表示状態に
            _step = 0;
            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            if (fadeOverlay == null) yield break;
            fadeOverlay.gameObject.SetActive(true);
            var c = fadeOverlay.color; c.a = 1f; fadeOverlay.color = c;
            float t = 0f;
            while (t < fadeInTime)
            {
                t += Time.unscaledDeltaTime;
                c.a = Mathf.Clamp01(1f - t / fadeInTime);
                fadeOverlay.color = c;
                yield return null;
            }
            c.a = 0f; fadeOverlay.color = c;
            fadeOverlay.raycastTarget = false;
            fadeOverlay.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_step == 0)
            {
                if (!_settingsOpen && Input.GetKeyDown(KeyCode.Return)) OpenNameStep();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Escape)) BackToTitle();
                if (Input.GetKeyDown(KeyCode.Return) && (nameField == null || !nameField.isFocused)) Confirm();
            }
        }

        void Apply()
        {
            if (AudioManager.Instance == null) return;
            AudioManager.Instance.bgmVolume = (bgmSlider != null ? bgmSlider.value : 0.7f) * 0.85f;
            AudioManager.Instance.sfxVolume = (sfxSlider != null ? sfxSlider.value : 0.8f) * 0.85f;
            AudioManager.Instance.ApplyVolumes();
        }

        public void ToggleSettings()
        {
            _settingsOpen = !_settingsOpen;
            if (settingsPanel != null) settingsPanel.SetActive(_settingsOpen);
        }

        /// <summary>PLAY ボタン。なまえ入力ステップへ。</summary>
        public void OpenNameStep()
        {
            if (_step == 1) return;
            _step = 1;
            SetTitleVisible(false);
            if (namePanel != null) namePanel.SetActive(true);
            if (nameField != null)
            {
                nameField.text = PlayerName.Value;
                nameField.Select();
                nameField.ActivateInputField();
            }
        }

        public void BackToTitle()
        {
            _step = 0;
            if (namePanel != null) namePanel.SetActive(false);
            SetTitleVisible(true);
        }

        void SetTitleVisible(bool v)
        {
            if (logo != null) logo.gameObject.SetActive(v);
            if (logoText != null) logoText.gameObject.SetActive(v);
            if (flavor != null) flavor.gameObject.SetActive(v);
            if (playButton != null) playButton.SetActive(v);
            if (settingsButton != null) settingsButton.SetActive(v);
            if (!v && settingsPanel != null) { settingsPanel.SetActive(false); _settingsOpen = false; }
        }

        /// <summary>なまえを確定してゲームへ。</summary>
        public void Confirm()
        {
            string n = nameField != null ? nameField.text.Trim() : "";
            if (string.IsNullOrEmpty(n))
            {
                if (hint != null) StartCoroutine(FlashHint("なまえ を いれてください"));
                if (nameField != null) { nameField.Select(); nameField.ActivateInputField(); }
                return;
            }
            PlayerName.Value = n;
            StartCoroutine(Go());
        }

        /// <summary>旧 API 互換(PLAY ボタンが Play を呼んでいた場合)。</summary>
        public void Play() => OpenNameStep();

        IEnumerator Go()
        {
            yield return new WaitForSecondsRealtime(0.35f);
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
