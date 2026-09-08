using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace L8000
{
    /// <summary>
    /// 画面演出のハブ: 中央のトースト文字 と 全画面フラッシュ。
    /// 破壊の副作用・必須ゲート・戻り演出から静的に呼ぶ。
    /// </summary>
    public class ScreenFx : MonoBehaviour
    {
        public static ScreenFx Instance { get; private set; }

        public TextMeshProUGUI toast;
        public TextMeshProUGUI banner;   // 画面下の常時表示(降下中「▼ もどっている」など)
        public Image flash;
        public Image vignette;           // Lighting 破壊で画面が暗くなる

        Coroutine _toastCo, _flashCo, _bannerCo, _vigCo;

        void Awake()
        {
            Instance = this;
            if (toast != null) { var c = toast.color; c.a = 0f; toast.color = c; }
            if (banner != null) { var c = banner.color; c.a = 0f; banner.color = c; }
            if (vignette != null) { var c = vignette.color; c.a = 0f; vignette.color = c; vignette.gameObject.SetActive(true); }
            if (flash != null) { var c = flash.color; c.a = 0f; flash.color = c; flash.gameObject.SetActive(true); }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Toast(string msg, Color color, float hold = 1.8f)
        {
            var s = Instance;
            if (s == null || s.toast == null) return;
            if (s._toastCo != null) s.StopCoroutine(s._toastCo);
            s._toastCo = s.StartCoroutine(s.ToastCo(msg, color, hold));
        }

        IEnumerator ToastCo(string msg, Color color, float hold)
        {
            toast.text = msg;
            float t = 0f;
            while (t < 0.25f) { t += Time.deltaTime; SetToastA(color, t / 0.25f); yield return null; }
            SetToastA(color, 1f);
            yield return new WaitForSeconds(hold);
            t = 0f;
            while (t < 0.6f) { t += Time.deltaTime; SetToastA(color, 1f - t / 0.6f); yield return null; }
            SetToastA(color, 0f);
        }

        void SetToastA(Color c, float a) { c.a = Mathf.Clamp01(a); toast.color = c; }

        public static void Flash(Color color, float dur = 0.35f)
        {
            var s = Instance;
            if (s == null || s.flash == null) return;
            if (s._flashCo != null) s.StopCoroutine(s._flashCo);
            s._flashCo = s.StartCoroutine(s.FlashCo(color, dur));
        }

        IEnumerator FlashCo(Color color, float dur)
        {
            float peak = color.a <= 0f ? 0.5f : color.a;
            float t = 0f;
            var rise = Mathf.Min(0.06f, dur * 0.3f);
            while (t < rise) { t += Time.deltaTime; SetFlashA(color, Mathf.Lerp(0f, peak, t / rise)); yield return null; }
            t = 0f;
            float fall = Mathf.Max(0.01f, dur - rise);
            while (t < fall) { t += Time.deltaTime; SetFlashA(color, Mathf.Lerp(peak, 0f, t / fall)); yield return null; }
            SetFlashA(color, 0f);
        }

        void SetFlashA(Color c, float a) { c.a = Mathf.Clamp01(a); flash.color = c; }

        /// <summary>画面を暗くする(Lighting 破壊)。0で戻す。</summary>
        public static void Vignette(float alpha, float dur = 1.5f)
        {
            var s = Instance;
            if (s == null || s.vignette == null) return;
            if (s._vigCo != null) s.StopCoroutine(s._vigCo);
            s._vigCo = s.StartCoroutine(s.VignetteCo(alpha, dur));
        }

        IEnumerator VignetteCo(float target, float dur)
        {
            float from = vignette.color.a, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                var c = vignette.color; c.a = Mathf.Lerp(from, target, t / dur); vignette.color = c;
                yield return null;
            }
            var cc = vignette.color; cc.a = target; vignette.color = cc;
        }

        /// <summary>全演出を即座に消す(終幕に入るときなど)。</summary>
        public static void ClearAll()
        {
            var s = Instance;
            if (s == null) return;
            if (s._toastCo != null) { s.StopCoroutine(s._toastCo); s._toastCo = null; }
            if (s._bannerCo != null) { s.StopCoroutine(s._bannerCo); s._bannerCo = null; }
            if (s._vigCo != null) { s.StopCoroutine(s._vigCo); s._vigCo = null; }
            if (s.toast != null) { var c = s.toast.color; c.a = 0f; s.toast.color = c; }
            if (s.banner != null) { var c = s.banner.color; c.a = 0f; s.banner.color = c; }
            if (s.vignette != null) { var c = s.vignette.color; c.a = 0f; s.vignette.color = c; }
        }

        /// <summary>画面下に常時表示。null/空 で消す。</summary>
        public static void Banner(string msg, Color color)
        {
            var s = Instance;
            if (s == null || s.banner == null) return;
            if (s._bannerCo != null) s.StopCoroutine(s._bannerCo);
            s._bannerCo = s.StartCoroutine(s.BannerCo(msg, color));
        }

        IEnumerator BannerCo(string msg, Color color)
        {
            bool show = !string.IsNullOrEmpty(msg);
            if (show) banner.text = msg;
            float from = banner.color.a, to = show ? 1f : 0f, t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                var c = color; c.a = Mathf.Lerp(from, to, t / 0.4f); banner.color = c;
                yield return null;
            }
            var cc = color; cc.a = to; banner.color = cc;
        }
    }
}
