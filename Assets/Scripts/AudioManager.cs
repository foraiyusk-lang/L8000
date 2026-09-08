using UnityEngine;

namespace L8000
{
    public enum Sfx { Jump, Shoot, Land, Hit, Break, Gate }

    /// <summary>
    /// BGM / SE。クリップは Assets/Resources/Audio/*.wav(MIDI から生成、scripts/make_bgm.py / make_sfx.py)。
    /// Composer 破壊で BGM 停止、Sound Director 破壊で SE 全消滅。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Range(0f, 1f)] public float bgmVolume = 0.4f;
        [Range(0f, 1f)] public float sfxVolume = 0.55f;

        AudioSource _bgm;
        AudioSource _sfx;
        AudioClip _jump, _shoot, _land, _hit, _break, _gate;
        bool _sfxKilled;

        void Awake()
        {
            Instance = this;

            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            _bgm.spatialBlend = 0f;         // 2D。カメラが離れても音量が変わらない
            _bgm.volume = bgmVolume;
            _bgm.clip = Load("bgm") ?? ProcBgm();

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
            _sfx.volume = sfxVolume;

            _jump = Load("sfx_jump") ?? Blip(660, 990, 0.10f);
            _shoot = Load("sfx_shoot") ?? Blip(1400, 700, 0.06f);
            _land = Load("sfx_land") ?? Blip(220, 130, 0.09f);
            _hit = Load("sfx_hit") ?? Blip(1500, 900, 0.05f);
            _break = Load("sfx_break") ?? Blip(180, 120, 0.4f);
            _gate = Load("sfx_gate") ?? Blip(500, 800, 0.3f);
        }

        void Start() { if (_bgm.clip != null) _bgm.Play(); }

        static AudioClip Load(string name) => Resources.Load<AudioClip>("Audio/" + name);

        public static void Play(Sfx s, float volScale = 1f)
        {
            var m = Instance;
            if (m == null || m._sfxKilled) return;
            AudioClip c = s switch
            {
                Sfx.Jump => m._jump,
                Sfx.Shoot => m._shoot,
                Sfx.Land => m._land,
                Sfx.Hit => m._hit,
                Sfx.Break => m._break,
                Sfx.Gate => m._gate,
                _ => null,
            };
            if (c != null) m._sfx.PlayOneShot(c, m.sfxVolume * volScale);
        }

        public void StopBgm()
        {
            if (_bgm == null) return;
            StartCoroutine(FadeOutBgm(1.2f));
        }

        System.Collections.IEnumerator FadeOutBgm(float t)
        {
            float v0 = _bgm.volume, e = 0f;
            while (e < t && _bgm != null)
            {
                e += Time.deltaTime;
                _bgm.volume = Mathf.Lerp(v0, 0f, e / t);
                yield return null;
            }
            if (_bgm != null) { _bgm.Stop(); _bgm.volume = v0; }
        }

        public void KillSfx()
        {
            _sfxKilled = true;
            if (_sfx != null) _sfx.Stop();
        }

        // --- フォールバック用の手続き生成(Resources が無い場合) ------
        const int SR = 44100;

        static AudioClip Blip(float f0, float f1, float dur)
        {
            int n = Mathf.CeilToInt(SR * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float f = Mathf.Lerp(f0, f1, u);
                data[i] = 0.22f * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(2f * Mathf.PI * f * (i / (float)SR));
            }
            var clip = AudioClip.Create("blip", n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip ProcBgm()
        {
            float dur = 8f;
            int n = Mathf.CeilToInt(SR * dur);
            var data = new float[n];
            float[] notes = { 220f, 261.63f, 196f, 246.94f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = notes[Mathf.Clamp((int)(t / (dur / 4f)), 0, 3)];
                float segU = (t % (dur / 4f)) / (dur / 4f);
                float env = Mathf.Sin(Mathf.PI * segU) * 0.6f + 0.2f;
                data[i] = 0.1f * env * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(4f * Mathf.PI * f * t));
            }
            int fade = SR / 20;
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] *= k; data[n - 1 - i] *= k; }
            var clip = AudioClip.Create("bgm", n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
