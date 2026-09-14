using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace L8000
{
    /// <summary>
    /// 終幕。真っ黒な画面にプレイヤーが一人。下から「PLAYED BY / (名前)」がせり上がり、
    /// 画面上へ抜けきったら タイトルへ戻る。
    /// </summary>
    public class Finale : MonoBehaviour
    {
        public Image blackout;
        public TextMeshPro worldText;   // world 空間。プレイヤーの脇をせり上がる。
        public Transform player;
        public Camera viewCamera;
        public TileField streamer;
        public Hud hud;

        public float fadeDuration = 1.4f;
        public float hold = 1.2f;
        public float scrollSpeed = 2.2f;      // world units / 秒
        public string nextScene = "Title";

        bool _started;

        void OnEnable() => VoidRiser.OnFinalSwallow += BeginBad;
        void OnDisable() => VoidRiser.OnFinalSwallow -= BeginBad;

        void BeginBad() => Begin(true);

        public void Begin(bool badEnd)
        {
            if (_started) return;
            _started = true;
            StartCoroutine(Run(badEnd));
        }

        IEnumerator Run(bool badEnd)
        {
            if (hud != null) hud.Kill();
            Time.timeScale = 1f;
            ScreenFx.ClearAll();   // 「↓ Enter」バナー・トースト・ビネットを消す

            // 進行と無を止める(GameFlow が再発動しないように)
            var flow = FindFirstObjectByType<GameFlow>();
            if (flow != null) flow.enabled = false;
            var vr = FindFirstObjectByType<VoidRiser>();
            if (vr != null) vr.Stop();

            // 暗転
            if (blackout != null)
            {
                blackout.gameObject.SetActive(true);
                yield return Fade(blackout, 0f, 1f, fadeDuration);
            }

            // 世界を消す(プレイヤーだけ残す)
            if (vr != null) { vr.Stop(); vr.gameObject.SetActive(false); }
            if (streamer != null) streamer.gameObject.SetActive(false);

            // カメラとプレイヤーを原点にそろえる(決め打ち)
            var cf = viewCamera != null ? viewCamera.GetComponent<CameraFollow>() : null;
            if (cf != null) cf.enabled = false;
            if (viewCamera != null)
            {
                viewCamera.transform.position = new Vector3(0f, 0f, -10f);
                viewCamera.transform.rotation = Quaternion.identity;
            }

            var pc = player != null ? player.GetComponent<PlayerController>() : null;
            if (pc != null) pc.enabled = false;
            var prb = player != null ? player.GetComponent<Rigidbody2D>() : null;
            if (prb != null) { prb.bodyType = RigidbodyType2D.Kinematic; prb.linearVelocity = Vector2.zero; }
            if (player != null) player.position = new Vector3(0f, -1.4f, 0f);

            // 暗転を戻す(カメラの黒背景 + プレイヤーだけが見える)
            if (blackout != null) yield return Fade(blackout, 1f, 0f, 0.8f);

            yield return new WaitForSeconds(hold);

            // 「PLAYED BY / 名前」をせり上げる
            if (worldText != null)
            {
                worldText.gameObject.SetActive(true);
                string branch = EndSequence_FinalLine(badEnd, TileField.TotalCharsDestroyed);
                worldText.text = $"{Localization.T("finale_played_by")}\n\n{PlayerName.Display}\n\n\n<size=55%>{branch}</size>";
                worldText.ForceMeshUpdate();

                float halfH = viewCamera != null ? viewCamera.orthographicSize : 5f;
                float textH = worldText.bounds.size.y;
                float y = -halfH - textH * 0.6f;
                float endY = halfH + textH * 0.7f;
                while (y < endY)
                {
                    y += scrollSpeed * Time.deltaTime;
                    worldText.transform.position = new Vector3(0f, y, 0f);
                    yield return null;
                }
            }

            yield return new WaitForSeconds(0.6f);
            CleanStatics();
            if (Application.CanStreamedLevelBeLoaded(nextScene))
                SceneManager.LoadScene(nextScene);
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        static void CleanStatics()
        {
            TileField.TotalCharsDestroyed = 0;
            TileField.Garble = false;
            TileField.DescentDropDestroyed = false;
            PlayerController.PhysicsAnomaly = false;
            CameraFollow.FrameBroken = false;
            WritingBroken = false;
        }

        static IEnumerator Fade(Image img, float from, float to, float dur)
        {
            float t = 0f;
            var c = img.color;
            while (t < dur)
            {
                t += Time.deltaTime;
                c.a = Mathf.Lerp(from, to, t / dur);
                img.color = c;
                yield return null;
            }
            c.a = to; img.color = c;
        }

        /// <summary>Lead Writer / Script Editor 破壊 → 最後の1行が黒塗りに。</summary>
        public static bool WritingBroken;

        /// <summary>破壊数と経路で分岐する最後の1行。</summary>
        public static string EndSequence_FinalLine(bool badEnd, int destroyed)
        {
            if (WritingBroken) return "██████ ████ ███████████。";
            if (badEnd)
            {
                if (destroyed <= 0) return Localization.T("finale_bad_zero");
                return Localization.T("finale_bad_some");
            }
            if (destroyed <= 0) return "";
            if (destroyed < 20) return Localization.T("finale_good_few");
            if (destroyed < 80) return Localization.T("finale_good_many");
            return Localization.T("finale_good_most");
        }
    }
}
