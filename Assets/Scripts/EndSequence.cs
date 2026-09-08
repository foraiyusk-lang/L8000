using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace L8000
{
    /// <summary>
    /// 「無」に飲まれた後 → 暗転 → 画面下から普通のスタッフロールが流れる。
    /// 最後の1行は破壊した人数で分岐する。
    /// </summary>
    public class EndSequence : MonoBehaviour
    {
        public Image blackout;
        public TextMeshProUGUI rollText;
        public RollStreamer streamer;
        public Hud hud;

        public float fadeDuration = 1.6f;
        public float holdBeforeRoll = 0.8f;
        public float rollScrollSpeed = 70f;   // px / 秒

        bool _started;

        void OnEnable() => VoidRiser.OnSwallowed += Begin;
        void OnDisable() => VoidRiser.OnSwallowed -= Begin;

        public void Begin()
        {
            if (_started) return;
            _started = true;
            ScreenFx.Flash(new Color(1f, 1f, 1f, 0.7f), 0.5f);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (hud != null) hud.Kill();

            if (blackout != null)
            {
                blackout.gameObject.SetActive(true);
                var c = blackout.color; c.a = 0f; blackout.color = c;
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    c.a = Mathf.Clamp01(t / fadeDuration);
                    blackout.color = c;
                    yield return null;
                }
                c.a = 1f; blackout.color = c;
            }

            yield return new WaitForSeconds(holdBeforeRoll);

            if (rollText == null) yield break;

            rollText.gameObject.SetActive(true);
            rollText.text = BuildPlainRoll();
            rollText.ForceMeshUpdate();

            var rt = rollText.rectTransform;
            float screenH = ((RectTransform)rollText.canvas.transform).rect.height;
            float textH = rollText.preferredHeight;

            // rect を本文と同じ高さにする(小さい rect のままだと Canvas が画面外で
            // 見た目を cull してしまい、スクロール中に消える)。pivot 下端で下から上へ。
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, textH);

            float startY = -(screenH * 0.5f) - textH;   // 本文が画面下に隠れた位置
            float endY = (screenH * 0.5f);              // 本文が画面上に抜けた位置
            float y = startY;
            rt.anchoredPosition = new Vector2(0f, y);

            while (y < endY)
            {
                y += rollScrollSpeed * Time.deltaTime;
                rt.anchoredPosition = new Vector2(0f, y);
                yield return null;
            }
        }

        string BuildPlainRoll()
        {
            var sb = new StringBuilder();
            int destroyed = CreditLineBuilder.TotalCharsDestroyed;

            if (streamer != null && streamer.Roll != null)
            {
                var lines = streamer.Roll.Lines;
                // Roll.Lines は WorldY 降順(上=タイトルが先頭)。表示順もその通り。
                for (int i = 0; i < lines.Count; i++)
                {
                    var s = lines[i];
                    switch (s.Kind)
                    {
                        case LineKind.Rule: sb.AppendLine(); break;
                        case LineKind.Header: sb.AppendLine(); sb.AppendLine($"― {s.Left} ―"); sb.AppendLine(); break;
                        case LineKind.Title: sb.AppendLine(s.Left); sb.AppendLine(); break;
                        case LineKind.RoleName: sb.AppendLine($"{s.Left}   {s.Right}"); break;
                        default: sb.AppendLine(s.Left); break;
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(FinalLine(destroyed));
            return sb.ToString();
        }

        /// <summary>頂上に到達してからのアウトロか(false = 残り0で飲まれた)。</summary>
        public static bool ReachedTop;

        /// <summary>破壊数(と到達経路)で分岐する最後の1行。</summary>
        public static string FinalLine(int destroyed)
        {
            if (!ReachedTop)
            {
                if (destroyed <= 0) return "指が止まった。ここまでだった。";
                return "動けなくなって、名前ごと沈んだ。";
            }
            if (destroyed <= 0) return "だれも消さずに、てっぺんまで来た。";
            if (destroyed < 20) return "いくつかの名前は、もう戻らない。";
            if (destroyed < 80) return "多くの名前が、無くなった。";
            return "ほとんど、何も残らなかった。それでも、登りきった。";
        }
    }
}
