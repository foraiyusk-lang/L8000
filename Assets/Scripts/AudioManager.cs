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
        AudioClip _bgmFwd, _bgmRev;
        AudioClip _jump, _shoot, _land, _hit, _break, _gate;
        bool _sfxKilled;

        void Awake()
        {
            Instance = this;

            // AudioListener が無いと一切鳴らない。保険で用意する。
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                var camObj = Camera.main != null ? Camera.main.gameObject : gameObject;
                camObj.AddComponent<AudioListener>();
            }

            // タイトルの設定を反映(掛け率は「主張しすぎない」範囲で聞こえる音量に)
            bgmVolume = PlayerPrefs.GetFloat("l8000_bgm", 0.7f) * 0.85f;
            sfxVolume = PlayerPrefs.GetFloat("l8000_sfx", 0.8f) * 0.85f;

            _bgmFwd = Load("bgm") ?? ProcBgm();
            _bgmRev = Load("bgm_rev") ?? _bgmFwd;
            // DecompressOnLoad + preload なので通常は即再生可。念のため明示ロード。
            if (_bgmFwd != null && _bgmFwd.loadState != AudioDataLoadState.Loaded) _bgmFwd.LoadAudioData();
            if (_bgmRev != null && _bgmRev.loadState != AudioDataLoadState.Loaded) _bgmRev.LoadAudioData();

            // BGM は子オブジェクトに(壊れたときフィルタを BGM だけに掛けるため)
            var bgmGo = new GameObject("Bgm");
            bgmGo.transform.SetParent(transform, false);
            _bgm = bgmGo.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            _bgm.spatialBlend = 0f;         // 2D。カメラが離れても音量が変わらない
            _bgm.volume = bgmVolume;
            _bgm.clip = _bgmFwd;

            // 砂嵐は別の子 GO に(BGM のフィルタと相互作用させない)
            var hissGo = new GameObject("Hiss");
            hissGo.transform.SetParent(bgmGo.transform, false);
            _hiss = hissGo.AddComponent<AudioSource>();
            _hiss.clip = WhiteNoise(2f);
            _hiss.loop = true; _hiss.playOnAwake = false; _hiss.spatialBlend = 0f; _hiss.volume = 0f;

            // 「音が歪む」用のフィルタを先に付けておく(実行時 AddComponent は
            // 物理コールバック中に呼ばれると失敗するため)。既定は無効。
            _dist = bgmGo.AddComponent<AudioDistortionFilter>(); _dist.distortionLevel = 0f; _dist.enabled = false;
            _lp = bgmGo.AddComponent<AudioLowPassFilter>();   _lp.cutoffFrequency = 22000f; _lp.enabled = false;
            _hp = bgmGo.AddComponent<AudioHighPassFilter>();  _hp.cutoffFrequency = 10f;    _hp.enabled = false;

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

        bool _reversed;
        Coroutine _xfadeCo;

        void Start()
        {
            SetReversed(_reversed, instant: true);
        }

        /// <summary>登り中は逆再生 wav、降り中は順再生 wav。負ピッチは使わない(ストリーミングで不安定)。</summary>
        public void SetReversed(bool rev, bool instant = false)
        {
            _reversed = rev;
            if (_bgm == null) return;
            var want = rev ? _bgmRev : _bgmFwd;
            if (_bgm.clip == want && _bgm.isPlaying) return;
            _bgm.clip = want;
            _bgm.pitch = 1f;
            _bgm.volume = bgmVolume;
            _bgm.Play();
        }

        /// <summary>逆再生 → 一瞬スローになって止まる → 順再生(「巻き戻しが止まる」)。</summary>
        public void CrossfadeToForward(float dur)
        {
            if (_xfadeCo != null) StopCoroutine(_xfadeCo);
            _xfadeCo = StartCoroutine(RewindStop(dur));
        }

        System.Collections.IEnumerator RewindStop(float dur)
        {
            // 逆再生をスロー＋フェードアウト(時間が止まっていく)
            float half = dur * 0.55f, e = 0f;
            float v0 = _bgm.volume, p0 = _bgm.pitch;
            while (e < half && _bgm != null)
            {
                e += Time.unscaledDeltaTime;
                float k = e / half;
                _bgm.pitch = Mathf.Lerp(p0, 0.05f, k);
                _bgm.volume = Mathf.Lerp(v0, 0f, k);
                yield return null;
            }
            if (_bgm == null) yield break;
            _bgm.Stop();
            yield return new WaitForSecondsRealtime(0.25f);

            // 順再生を低ピッチから立ち上げる(時間が また 進みだす)
            _reversed = false;
            _bgm.clip = _bgmFwd;
            _bgm.pitch = 0.4f;
            _bgm.volume = 0f;
            _bgm.Play();
            e = 0f;
            float rise = dur * 0.45f;
            while (e < rise && _bgm != null)
            {
                e += Time.unscaledDeltaTime;
                float k = e / rise;
                _bgm.pitch = Mathf.Lerp(0.4f, 1f, k);
                _bgm.volume = Mathf.Lerp(0f, bgmVolume, k);
                yield return null;
            }
            if (_bgm != null) { _bgm.pitch = 1f; _bgm.volume = bgmVolume; }
        }

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

        bool _bgmBroken;
        AudioDistortionFilter _dist;
        AudioLowPassFilter _lp;
        AudioHighPassFilter _hp;
        AudioSource _hiss;

        /// <summary>Music/Composer 破壊: BGM を止めず、音を歪ませて砂の混じった音に。</summary>
        public void DistortBgm()
        {
            if (_bgm == null || _bgmBroken) return;
            _bgmBroken = true;

            if (_dist != null) { _dist.enabled = true; _dist.distortionLevel = 0.55f; }
            if (_lp != null)   { _lp.enabled = true;   _lp.cutoffFrequency = 2600f; }
            if (_hp != null)   { _hp.enabled = true;   _hp.cutoffFrequency = 180f; }
            if (_hiss != null) { _hiss.volume = bgmVolume * 0.35f; _hiss.Play(); }

            StartCoroutine(DipVolume());
        }

        System.Collections.IEnumerator DipVolume()
        {
            // 一瞬 音が痩せてから戻る(壊れた瞬間の違和感)
            float v0 = _bgm.volume, e = 0f;
            while (e < 0.4f && _bgm != null) { e += Time.deltaTime; _bgm.volume = Mathf.Lerp(v0, v0 * 0.4f, e / 0.4f); yield return null; }
            e = 0f;
            while (e < 0.8f && _bgm != null) { e += Time.deltaTime; _bgm.volume = Mathf.Lerp(v0 * 0.4f, v0 * 0.8f, e / 0.8f); yield return null; }
            if (_bgm != null) _bgm.volume = v0 * 0.8f;
        }

        /// <summary>降りで Music の所を通過: 歪みを解いて元の音へ。</summary>
        public void RestoreBgm()
        {
            if (!_bgmBroken) return;
            _bgmBroken = false;
            if (_dist != null) { _dist.distortionLevel = 0f; _dist.enabled = false; }
            if (_lp != null)   { _lp.cutoffFrequency = 22000f; _lp.enabled = false; }
            if (_hp != null)   { _hp.cutoffFrequency = 10f;    _hp.enabled = false; }
            if (_hiss != null) { _hiss.Stop(); _hiss.volume = 0f; }
            if (_bgm != null) _bgm.volume = bgmVolume;
        }

        static AudioClip WhiteNoise(float dur)
        {
            int n = Mathf.CeilToInt(SR * dur);
            var data = new float[n];
            var rng = new System.Random(20260909);
            for (int i = 0; i < n; i++) data[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f;
            // ループ継ぎ目を目立たなく
            int fade = SR / 40;
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] *= k; data[n - 1 - i] *= k; }
            var clip = AudioClip.Create("hiss", n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Sound Director 破壊: 効果音を消す。既存の再生を断ち切って耳障りな音を出さないよう、
        /// 音量を0にするだけ(Stop()で途中の波形を切ると "ザッ" というノイズが出るため使わない)。</summary>
        public void KillSfx()
        {
            _sfxKilled = true;
            if (_sfx != null) _sfx.volume = 0f;
        }

        /// <summary>降りで Sound Director の所を通過: 効果音を戻す。</summary>
        public void ReviveSfx()
        {
            _sfxKilled = false;
            if (_sfx != null) _sfx.volume = sfxVolume;
        }

        /// <summary>設定スライダーの反映。</summary>
        public void ApplyVolumes()
        {
            if (_bgm != null && _xfadeCo == null) _bgm.volume = bgmVolume;
            if (_sfx != null && !_sfxKilled) _sfx.volume = sfxVolume;
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
