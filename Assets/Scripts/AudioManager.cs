using UnityEngine;

namespace L8000
{
    public enum Sfx { Jump, Shoot, Land, Hit }

    /// <summary>
    /// 手続き生成の最小 BGM / SE。アセット不要。
    /// Composer 破壊で BGM 停止、Sound Director 破壊で SE 全消滅。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Range(0f, 1f)] public float bgmVolume = 0.35f;
        [Range(0f, 1f)] public float sfxVolume = 0.5f;

        AudioSource _bgm;
        AudioSource _sfx;
        AudioClip _jump, _shoot, _land, _hit;
        bool _sfxKilled;

        void Awake()
        {
            Instance = this;

            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            _bgm.volume = bgmVolume;
            _bgm.clip = BuildBgm();

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.volume = sfxVolume;

            _jump = Blip(660f, 990f, 0.10f);
            _shoot = Blip(1400f, 700f, 0.06f);
            _land = Blip(220f, 130f, 0.09f);
            _hit = Noise(0.05f, 0.5f);
        }

        void Start() => _bgm.Play();

        public static void Play(Sfx s)
        {
            var m = Instance;
            if (m == null || m._sfxKilled) return;
            AudioClip c = s switch
            {
                Sfx.Jump => m._jump,
                Sfx.Shoot => m._shoot,
                Sfx.Land => m._land,
                _ => m._hit,
            };
            if (c != null) m._sfx.PlayOneShot(c, m.sfxVolume);
        }

        public void StopBgm()
        {
            if (_bgm != null) _bgm.Stop();
        }

        public void KillSfx()
        {
            _sfxKilled = true;
            if (_sfx != null) _sfx.Stop();
        }

        // --- 手続き生成 ------------------------------------------------
        const int SR = 44100;

        static AudioClip Blip(float f0, float f1, float dur)
        {
            int n = Mathf.CeilToInt(SR * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float u = i / (float)n;
                float f = Mathf.Lerp(f0, f1, u);
                float env = Mathf.Sin(Mathf.PI * u);                 // フェードイン/アウト
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
                data[i] = 0.25f * env * sq;
            }
            var clip = AudioClip.Create("blip", n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Noise(float dur, float amp)
        {
            int n = Mathf.CeilToInt(SR * dur);
            var data = new float[n];
            var rng = new System.Random(1);
            for (int i = 0; i < n; i++)
            {
                float env = 1f - (i / (float)n);
                data[i] = amp * env * (float)(rng.NextDouble() * 2.0 - 1.0);
            }
            var clip = AudioClip.Create("noise", n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip BuildBgm()
        {
            // ゆっくりした 4 音の柔らかいパッド(ループ)
            float dur = 8f;
            int n = Mathf.CeilToInt(SR * dur);
            var data = new float[n];
            float[] notes = { 220.00f, 261.63f, 196.00f, 246.94f }; // A3 C4 G3 B3
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                int seg = Mathf.Clamp((int)(t / (dur / notes.Length)), 0, notes.Length - 1);
                float f = notes[seg];
                float segU = (t % (dur / notes.Length)) / (dur / notes.Length);
                float env = Mathf.Sin(Mathf.PI * segU) * 0.6f + 0.2f;
                float s = Mathf.Sin(2f * Mathf.PI * f * t)
                        + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 2f * t)
                        + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 0.5f * t);
                data[i] = 0.12f * env * s;
            }
            // ループ継ぎ目のクリック回避に短いクロスフェード
            int fade = SR / 20;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[n - 1 - i] *= k;
            }
            var clip = AudioClip.Create("bgm", n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
