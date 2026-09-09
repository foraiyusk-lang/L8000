using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace L8000.EditorTools
{
    /// <summary>
    /// 残り8000回 - Slice 1 のシーンとアセットをヘッドレスで構築する。
    ///   Unity.exe -batchmode -projectPath &lt;repo&gt; -executeMethod L8000.EditorTools.ProjectBootstrap.Build -quit
    /// もしくは Unity メニュー  L8000 / Build Slice 1 Scene  から。
    /// </summary>
    public static class ProjectBootstrap
    {
        const string FontTtfPath   = "Assets/Fonts/x12y16pxMaruMonica.ttf";
        const string FontAssetPath = "Assets/Fonts/MaruMonica.asset";
        const string LayoutPath    = "Assets/Settings/GameLayout.asset";
        const string CreditsPath   = "Assets/Credits/credits.txt";
        const string WhitePngPath  = "Assets/Art/white.png";
        const string LogoPngPath   = "Assets/Art/logo.png";
        const string ScenePath     = "Assets/Scenes/Main.unity";
        const string TitleScenePath = "Assets/Scenes/Title.unity";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("L8000/Build Slice 1 Scene")]
        public static void BuildMenu() => Build();

        // -----------------------------------------------------------------
        // ヘッドレスで Play して数秒後にカメラをPNG保存する検証用。
        //   Unity.exe -batchmode -projectPath <repo> -executeMethod L8000.EditorTools.ProjectBootstrap.PlayCapture
        //   ( -quit は付けない。終了は自前で行う )
        // 追加引数:  -l8000-frames N  -l8000-out <path>  -l8000-climb (自動で登り続ける)
        // -----------------------------------------------------------------
        static int _capFrame;
        static string _capOut = "Screenshots/slice1.png";

        // Dynamic Rigidbody2D は transform ではなく rb.position で動かす(でないと固まる)
        static void TeleportPlayer(PlayerController pc, float x, float y)
        {
            if (pc == null) return;
            var rb = pc.GetComponent<Rigidbody2D>();
            pc.transform.position = new Vector3(x, y, 0f);
            if (rb != null) { rb.position = new Vector2(x, y); rb.linearVelocity = Vector2.zero; rb.WakeUp(); }
            Physics2D.SyncTransforms();
        }
        static bool _capClimb;
        // 検証: プレイヤーを各高さへワープ → 「・」を上へ連射 → 待って撮影。文字が消えるか確認。
        static readonly float[] _capStops = { 3f, 35f, 80f, 150f };
        static int _capStopIdx;
        static int _capStage;   // 0=移動/ジャンプ/スクロール確認, 1=撃ちテスト
        static int _capPhase;   // 0=移動/待機, 1=連射, 2=撮影待ち
        static int _capPhaseFrame;
        static double _capLastLog;
        static float _capMarkX = -999f, _capPeakY = -999f, _capBaseY = -999f, _capDblPeak = -999f;
        static bool _capJumpInit, _capMidInit, _capDblArmed, _capJumpDone; static int _capJumpFrame;
        static double _capT1;
        static float _layoutMaxGap;

        public static void PlayCapture()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-l8000-out" && i + 1 < args.Length) _capOut = args[i + 1];
                if (args[i] == "-l8000-climb") _capClimb = true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_capOut)));

            // Play mode に入るときのドメインリロードを止めないと、静的状態と update 購読が飛ぶ。
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _capFrame = 0;
            _capStage = 0;
            _capStopIdx = 0;
            _capPhase = 0;
            _capPhaseFrame = 0;
            _capLastLog = 0;
            _capMarkX = -999f; _capPeakY = -999f; _capBaseY = -999f; _capDblPeak = -999f;
            _capJumpInit = false; _capMidInit = false; _capDblArmed = false; _capJumpDone = false; _capJumpFrame = 0;
            var lay = AssetDatabase.LoadAssetAtPath<GameLayout>(LayoutPath);
            _layoutMaxGap = lay != null ? lay.maxLineGap : 0f;
            EditorApplication.update += CapUpdate;
            Log("entering play mode for capture");
            EditorApplication.EnterPlaymode();
        }

        static double _capStartTime = -1;

        static void CapUpdate()
        {
            if (_capStartTime < 0) _capStartTime = EditorApplication.timeSinceStartup;

            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - _capStartTime > 90)
                {
                    Debug.LogError("[Bootstrap] play mode に入れなかった。中断。");
                    EditorApplication.update -= CapUpdate;
                    EditorApplication.Exit(2);
                }
                return;
            }
            _capFrame++;
            if (_capFrame < 30) return; // 起動直後は待つ

            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;

            bool done = false;

            // --- Stage 0: 左右移動 / ジャンプ高 / 中央スクロール開始の確認 ---
            if (_capStage == 0)
            {
                _capPhaseFrame++;
                var streamer0 = UnityEngine.Object.FindFirstObjectByType<TileField>();
                float topY = streamer0 != null ? streamer0.TopY : 0f;
                var rb0 = pc != null ? pc.GetComponent<Rigidbody2D>() : null;
                double t = EditorApplication.timeSinceStartup - _capStartTime;

                // A: スポーン床の上で右へ歩く(継ぎ目/スリープで止まらないか)
                if (t < 5.0)
                {
                    if (t > 1.5)
                    {
                        PlayerController.DebugAutoWalk = 1;
                        if (_capMarkX < -900f && pc != null) _capMarkX = pc.transform.position.x;
                    }
                    if (t - _capLastLog > 1.0 && pc != null)
                    {
                        _capLastLog = t;
                        Log($"walk t{t:0.0}s: x={pc.transform.position.x:0.00} y={pc.transform.position.y:0.00} " +
                            $"vx={pc.Velocity.x:0.0} grounded={pc.IsGrounded} 進んだ距離={pc.transform.position.x - (_capMarkX < -900f ? pc.transform.position.x : _capMarkX):0.0}");
                    }
                }
                // B: タイトルの上(天井なし)で 単ジャンプ→2段ジャンプ の高さ計測
                else if (!_capJumpDone)
                {
                    PlayerController.DebugAutoWalk = 0;
                    if (!_capJumpInit && pc != null)
                    {
                        _capJumpInit = true; _capJumpFrame = 0;
                        TeleportPlayer(pc, 0f, topY + 0.9f);
                        _capBaseY = -999f; _capPeakY = -999f; _capDblPeak = -999f;
                    }
                    _capJumpFrame++;
                    if (pc != null)
                    {
                        if (pc.IsGrounded && _capBaseY < -900f) _capBaseY = pc.transform.position.y;
                        if (_capJumpFrame == 90) PlayerController.DebugForceJump = true;              // 1段目
                        if (_capJumpFrame == 150) { PlayerController.DebugForceJump = true; _capDblArmed = true; } // 2段目
                        if (_capBaseY > -900f && _capJumpFrame > 90)
                        {
                            float py = pc.transform.position.y;
                            if (!_capDblArmed) { if (py > _capPeakY) _capPeakY = py; }
                            else if (py > _capDblPeak) _capDblPeak = py;
                        }
                        if (_capJumpFrame > 320) _capJumpDone = true;
                    }
                }
                // C: 中央へワープして自動スクロール開始を確認
                else if (t < 17.0)
                {
                    PlayerController.DebugAutoClimb = false;
                    if (!_capMidInit)
                    {
                        _capMidInit = true;
                        if (_capBaseY > -900f)
                            Log($"jump: 単={_capPeakY - _capBaseY:0.00}  2段合計={_capDblPeak - _capBaseY:0.00}  " +
                                $"(base {_capBaseY:0.0}) / maxLineGap={_layoutMaxGap:0.00}  比={(_capPeakY - _capBaseY) / Mathf.Max(0.01f, _layoutMaxGap):0.00}");
                        if (rb0 != null) rb0.simulated = false;
                        if (pc != null) TeleportPlayer(pc, 0f, topY * 0.55f);
                    }
                    if (t - _capLastLog > 1.0)
                    {
                        _capLastLog = t;
                        Log($"mid t{t:0.0}s: camY={(cam != null ? cam.transform.position.y : 0):0.0} (中央=~{topY * 0.5f:0.0})");
                    }
                }
                else
                {
                    Log($"stage0 done.");
                    _capStage = 1; _capPhaseFrame = 0;
                }
                if (_capPhaseFrame == 120) CaptureCameraTo(Path.ChangeExtension(_capOut, null) + "_walk.png");
                return;
            }

            // --- Stage 1: 撮影中はカメラを手動制御 ---
            if (follow != null && follow.enabled) follow.enabled = false;

            if (_capClimb && pc != null && _capStopIdx < _capStops.Length)
            {
                float y = _capStops[_capStopIdx];
                var rb = pc.GetComponent<Rigidbody2D>();
                _capPhaseFrame++;

                if (_capPhase == 0) // ワープして静止、少し待つ
                {
                    TeleportPlayer(pc, 0f, y + 1.0f);
                    if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
                    if (cam != null) cam.transform.position = new Vector3(0f, y, -10f);
                    Weapon.DebugAutoFire = false;
                    if (_capPhaseFrame > 20)
                    {
                        CaptureCameraTo(Path.ChangeExtension(_capOut, null) + $"_{_capStopIdx}a_before.png");
                        _capPhase = 1; _capPhaseFrame = 0;
                    }
                }
                else if (_capPhase == 1) // 左右に振りながら上へ連射
                {
                    CreditLineBuilder.DebugHitLog = _capStopIdx == 0;
                    if (rb != null) rb.simulated = false;
                    if (pc != null) TeleportPlayer(pc, Mathf.PingPong(_capPhaseFrame * 0.06f, 3f) - 1.5f, y + 0.3f);
                    Weapon.DebugAutoFire = true;
                    if (_capPhaseFrame == 60)
                        Log($"  bullets alive = {UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length}");
                    if (_capPhaseFrame > 150) { Weapon.DebugAutoFire = false; CreditLineBuilder.DebugHitLog = false; _capPhase = 2; _capPhaseFrame = 0; }
                }
                else // 弾が飛び切るのを待って撮影
                {
                    if (_capPhaseFrame > 90)
                    {
                        CaptureCameraTo(Path.ChangeExtension(_capOut, null) + $"_{_capStopIdx}b_after.png");
                        Log($"stop y={y}: destroyed so far = {CreditLineBuilder.TotalCharsDestroyed}");
                        _capStopIdx++;
                        _capPhase = 0; _capPhaseFrame = 0;
                    }
                }

                if (EditorApplication.timeSinceStartup - _capStartTime > 300.0) done = true;
            }
            else done = true;

            if (!done) return;

            EditorApplication.update -= CapUpdate;
            Weapon.DebugAutoFire = false;
            DumpDiagnostics();
            Log($"[Bootstrap] total chars destroyed by bullets = {CreditLineBuilder.TotalCharsDestroyed}");
            EditorApplication.isPlaying = false;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        static void CaptureCameraTo(string path)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Bootstrap] no Camera.main for capture"); return; }
            const int W = 960, H = 540;
            var rt = new RenderTexture(W, H, 24);
            var prevT = cam.targetTexture;
            var prevA = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            cam.targetTexture = prevT;
            RenderTexture.active = prevA;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            Log($"screenshot {path} (frame {_capFrame})");
        }

        static void DumpDiagnostics()
        {
            var streamer = UnityEngine.Object.FindFirstObjectByType<TileField>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            int liveLines = UnityEngine.Object.FindObjectsByType<CreditTile>(FindObjectsSortMode.None).Length;
            int liveBoxes = UnityEngine.Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Length;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("--- L8000 diagnostics ---");
            if (streamer != null)
                sb.AppendLine($"bottomY={streamer.BottomY:0.0} topY={streamer.TopY:0.0}");
            sb.AppendLine($"tiles={liveLines}  total BoxCollider2D={liveBoxes}");
            if (player != null)
                sb.AppendLine($"player pos={player.transform.position} grounded={player.IsGrounded}");
            Debug.Log("[Bootstrap] " + sb);
        }

        public static void Build()
        {
            int code = 0;
            try
            {
                Log("=== ProjectBootstrap.Build start ===");
                EnsureDir("Assets/Settings");
                EnsureDir("Assets/Art");
                EnsureDir("Assets/Scenes");

                EnsureLayer("Ground");
                EnsureLayer("Player");
                AssetDatabase.SaveAssets();

                var layout = EnsureLayout();
                ImportTmpEssentials();          // TMP のシェーダ/Settings は essentials に入っている。先に取り込む。
                EnsureTmpSettings();            // CreateFontAsset は TMP_Settings.instance を触るので先に。
                var font = EnsureFontAsset();
                SetTmpDefaultFont(font);
                var white = EnsureWhiteSprite();
                var credits = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsPath);
                if (credits == null) throw new Exception("credits.txt が見つからない: " + CreditsPath);

                var logo = EnsureLogoSprite();
                BuildScene(layout, font, white, credits);
                BuildTitleScene(font, white, logo);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log("=== ProjectBootstrap.Build done ===");
            }
            catch (Exception e)
            {
                Debug.LogError("[Bootstrap] FAILED: " + e);
                code = 1;
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(code);
        }

        // ---------------------------------------------------------------
        static GameLayout EnsureLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<GameLayout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<GameLayout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
                Log("created " + LayoutPath);
            }
            // 新規追加フィールドが 0 のまま(古い .asset)だと足場が消えるので既定へ補正
            bool fixedAny = false;
            if (layout.charColliderWidthRatio <= 0f) { layout.charColliderWidthRatio = 1f; fixedAny = true; }
            if (layout.maxLineGap <= 0f) { layout.maxLineGap = 2.6f; fixedAny = true; }
            if (layout.minLineGap <= 0f) { layout.minLineGap = 1.3f; fixedAny = true; }
            if (layout.maxSectionGap <= 0f) { layout.maxSectionGap = 2.2f; fixedAny = true; }
            if (layout.gapUnit <= 0f) { layout.gapUnit = 0.6f; fixedAny = true; }
            if (layout.cellHeight <= 0f) { layout.cellHeight = 1f; fixedAny = true; }
            if (layout.colliderHeightRatio <= 0f) { layout.colliderHeightRatio = 0.9f; fixedAny = true; }
            if (fixedAny) { EditorUtility.SetDirty(layout); AssetDatabase.SaveAssets(); Log("layout: 既定値を補正"); }
            return layout;
        }

        static TMP_FontAsset EnsureFontAsset()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            if (fontAsset == null)
            {
                var srcFont = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
                if (srcFont == null) throw new Exception("マルモニカ TTF が見つからない: " + FontTtfPath);

                // ピクセルフォントなので SDF ではなく RASTER。滲ませない。
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    srcFont,
                    samplingPointSize: 16,
                    atlasPadding: 1,
                    renderMode: GlyphRenderMode.RASTER_HINTED,
                    atlasWidth: 1024,
                    atlasHeight: 1024,
                    atlasPopulationMode: AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);
                if (fontAsset == null) throw new Exception("CreateFontAsset が null を返した");
                fontAsset.name = "MaruMonica";

                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = "MaruMonica Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.name = "MaruMonica Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                }
                Log("created " + FontAssetPath);
            }

            // 必要な文字をすべて編集時にベイクする(実行時の動的ベイクが不安定で
            //  黒画面になる問題を回避)。アトラスをクリアしてから入れ直す。
            fontAsset.ClearFontAssetData(setAtlasSizeToZero: false);
            var chars = new System.Collections.Generic.HashSet<char>();
            var credits = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsPath);
            if (credits != null)
                foreach (char c in credits.text) if (!char.IsControl(c)) chars.Add(c);
            const string extra =
                "残り8000回 PLAYED BY 名も無き声 巻き戻しが止まる もどっている ↓ ▼ ▶ ― " +
                "なまえ はじめる とじる 設定 効果音 音量 BGM Enter エンドロールのいちばん下から。 " +
                "英字かなカナ漢字記号数字 消失 画面の水平 だれも消さずに下まで戻った いくつかの名前はもう戻らない " +
                "多くの名前が無くなった ほとんど何も残らなかったそれでも戻ってきた 指が止まったここまでだった " +
                "動けなくなって名前ごと沈んだ ██ 　0123456789()/.,:;'\"!?&+- " +
                "音が歪む 効果音 物理 描画 翻訳 照明 言葉 進行管理 もどした なまえをいれてください " +
                "█▉▊▋▌▍▎▏░▒▓■□▚▞▙▟▛▜╳╱╲─│┼※#×〓＃";   // 文字化け用
            foreach (char c in extra) if (!char.IsControl(c)) chars.Add(c);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;   // 追加のため一時的に
            var arr = new char[chars.Count]; chars.CopyTo(arr);
            fontAsset.TryAddCharacters(new string(arr), out string missing);
            if (!string.IsNullOrEmpty(missing))
                Log($"font: could not add: {missing}");
            // ベイク済みにして実行時の動的ベイク(不安定・黒画面)を止める
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            SetAtlasPointFilter(fontAsset);
            EditorUtility.SetDirty(fontAsset);
            if (fontAsset.atlasTexture != null) EditorUtility.SetDirty(fontAsset.atlasTexture);
            AssetDatabase.SaveAssets();
            Log($"font: baked {fontAsset.characterTable.Count} glyphs");
            return fontAsset;
        }

        static void SetAtlasPointFilter(TMP_FontAsset fontAsset)
        {
            if (fontAsset.atlasTextures != null)
                foreach (var tex in fontAsset.atlasTextures)
                    if (tex != null) tex.filterMode = FilterMode.Point;
            if (fontAsset.atlasTexture != null)
                fontAsset.atlasTexture.filterMode = FilterMode.Point;
            if (fontAsset.material != null && fontAsset.material.mainTexture != null)
                fontAsset.material.mainTexture.filterMode = FilterMode.Point;
        }

        static TMP_Settings _settings;

        static void ImportTmpEssentials()
        {
            bool shadersOk = Shader.Find("TextMeshPro/Distance Field") != null
                             && Shader.Find("TextMeshPro/Bitmap") != null;
            bool settingsOk = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null;
            if (shadersOk && settingsOk) { Log("TMP essentials already present"); return; }

            string pkg = null;
            if (Directory.Exists("Library/PackageCache"))
            {
                foreach (var dir in Directory.GetDirectories("Library/PackageCache", "com.unity.ugui@*")
                         .Concat(Directory.GetDirectories("Library/PackageCache", "com.unity.textmeshpro@*")))
                {
                    string p = Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
                    if (File.Exists(p)) { pkg = p.Replace('\\', '/'); break; }
                }
            }
            if (pkg == null) throw new Exception("TMP Essential Resources.unitypackage が見つからない");

            // 以前 blank で作った TMP Settings 等があると GUID 衝突するので一旦掃除
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                AssetDatabase.DeleteAsset("Assets/TextMesh Pro");
                AssetDatabase.Refresh();
            }

            Log("importing " + pkg);
            var mi = typeof(AssetDatabase).GetMethod("ImportPackageImmediately",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
            if (mi != null)
                mi.Invoke(null, new object[] { pkg });
            else
                AssetDatabase.ImportPackage(pkg, false);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (Shader.Find("TextMeshPro/Distance Field") == null)
                throw new Exception("TMP essentials を取り込んだがシェーダが見つからない。"
                    + "Unity で Window > TextMeshPro > Import TMP Essential Resources を手動実行してください。");
            Log("TMP essentials imported");
        }

        static void EnsureTmpSettings()
        {
            _settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (_settings == null)
            {
                EnsureDir("Assets/TextMesh Pro");
                EnsureDir("Assets/TextMesh Pro/Resources");
                _settings = ScriptableObject.CreateInstance<TMP_Settings>();
                _settings.name = "TMP Settings";
                AssetDatabase.CreateAsset(_settings, TmpSettingsPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Log("created " + TmpSettingsPath);
            }

            // TMP_Settings.instance が null だと CreateFontAsset 内で NRE になるため、
            // 静的インスタンスを直に差し込んでおく(Resources.Load が間に合わない場合の保険)。
            var f = typeof(TMP_Settings).GetField("s_Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (f != null) f.SetValue(null, _settings);

            var so = new SerializedObject(_settings);
            var missing = so.FindProperty("m_missingGlyphCharacter");
            if (missing != null && missing.intValue == 0) missing.intValue = '?';
            var warnings = so.FindProperty("m_warningsDisabled");
            if (warnings != null) warnings.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_settings);
        }

        static void SetTmpDefaultFont(TMP_FontAsset font)
        {
            if (_settings == null || font == null) return;
            var so = new SerializedObject(_settings);
            var p = so.FindProperty("m_defaultFontAsset");
            if (p != null) p.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
        }

        static Sprite EnsureWhiteSprite()
        {
            if (!File.Exists(WhitePngPath))
            {
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var px = Enumerable.Repeat(Color.white, 64).ToArray();
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(WhitePngPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(WhitePngPath, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(WhitePngPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.spritePixelsPerUnit = 16;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(WhitePngPath);
        }

        // ---------------------------------------------------------------
        static void BuildScene(GameLayout layout, TMP_FontAsset font, Sprite white, TextAsset credits)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---------------------------------------------------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10f;   // CameraFollow.orthoSize で上書きされる。大きいほど文字が小さく見える。
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = -50f;
            cam.farClipPlane = 50f;
            cam.allowMSAA = false;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();   // これが無いと BGM/SE が一切鳴らない

            // --- Roll (タイル方式: 単語/名前ごとの CreditTile。シーンで手配置) ---
            var rollGo = new GameObject("CreditTiles");
            var streamer = rollGo.AddComponent<TileField>();
            L8000.EditorTools.CreditTileBuilder.Populate(rollGo.transform, font,
                credits != null ? credits.text : string.Empty, layout);

            // --- Player ------------------------------------------------
            int playerLayer = LayerMask.NameToLayer("Player");
            var playerGo = new GameObject("Player");
            if (playerLayer >= 0) playerGo.layer = playerLayer;
            playerGo.transform.position = new Vector3(0f, 2f, 0f);
            // localScale は 1 のまま(スクリプトの接地判定は非スケールの world 距離で計算するため)

            // 細い正方形。文字間のスペース(halfCellWidth) / 中央の縦の道 を通れるサイズ。
            const float PlayerW = 0.26f;
            const float PlayerH = 0.36f;
            const float visSprite = 8f / 16f; // white.png 8px @ PPU16 = 0.5 unit
            var vis = new GameObject("Sprite");
            vis.transform.SetParent(playerGo.transform, false);
            vis.transform.localScale = new Vector3(PlayerW / visSprite, PlayerH / visSprite, 1f);
            var sr = vis.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = new Color(0.22f, 0.92f, 1f); // シアン。白い文字と混ざらない。
            sr.sortingOrder = 10;

            var outline = new GameObject("Outline");
            outline.transform.SetParent(playerGo.transform, false);
            outline.transform.localScale = new Vector3((PlayerW + 0.05f) / visSprite, (PlayerH + 0.05f) / visSprite, 1f);
            var osr = outline.AddComponent<SpriteRenderer>();
            osr.sprite = white;
            osr.color = Color.black;
            osr.sortingOrder = 9;

            var rb = playerGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // 当たり判定は見た目とほぼ同寸(0.26 x 0.36)。角だけ僅かに丸める。
            var col = playerGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(PlayerW - 0.04f, PlayerH - 0.04f);
            col.edgeRadius = 0.02f;
            col.sharedMaterial = new PhysicsMaterial2D("PlayerLowFriction") { friction = 0.05f, bounciness = 0f };

            var pc = playerGo.AddComponent<PlayerController>();
            int groundLayer = LayerMask.NameToLayer("Ground");
            pc.groundMask = groundLayer >= 0 ? (1 << groundLayer) : Physics2D.AllLayers;
            pc.groundCheckSize = new Vector2(PlayerW - 0.05f, 0.14f);
            pc.groundCheckOffsetY = -(PlayerH * 0.5f + 0.03f);
            pc.bodySize = new Vector2(PlayerW, PlayerH);

            // --- 武器(Enter で「・」発射) ---------------------------
            var weapon = playerGo.AddComponent<Weapon>();
            weapon.font = font;
            weapon.bulletColliderRadius = 0.22f;
            weapon.bulletVisualHeight = 0.5f;
            weapon.bulletSpeed = 9f;
            weapon.fireCooldown = 0.22f;
            weapon.muzzleOffsetY = PlayerH * 0.5f + 0.1f;

            // --- Player start / safety floor ---------------------------
            var startGo = new GameObject("PlayerStart");
            var ps = startGo.AddComponent<PlayerStart>();
            ps.roll = streamer;
            ps.player = playerGo.transform;

            // --- Camera follow ----------------------------------------
            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = playerGo.transform;
            follow.roll = streamer;
            follow.pixelsPerUnit = 16f;

            // --- ゲーム状態 (残り回数) --------------------------------
            var stateGo = new GameObject("GameState");
            var state = stateGo.AddComponent<GameState>();
            state.startingActions = 2000;   // TGS 出展ビルドは時間の都合で 2000

            // --- HUD (残り回数表示) ----------------------------------
            var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var labelGo = new GameObject("RemainingLabel", typeof(RectTransform));
            labelGo.transform.SetParent(canvasGo.transform, false);
            var label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 34f;
            label.color = new Color(0.85f, 0.9f, 1f);
            label.alignment = TMPro.TextAlignmentOptions.TopLeft;
            label.text = "残り 2000";
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f);
            lrt.anchoredPosition = new Vector2(24f, -18f);
            lrt.sizeDelta = new Vector2(420f, 60f);

            var hud = stateGo.AddComponent<Hud>();
            hud.label = label;

            // --- 「無」(登り=下から / 降り=上から せり寄る黒) -------
            var voidGo = new GameObject("VoidRiser");
            var voidFillGo = new GameObject("Fill");
            voidFillGo.transform.SetParent(voidGo.transform, false);
            var voidFill = voidFillGo.AddComponent<SpriteRenderer>();
            voidFill.sprite = white; voidFill.color = Color.black; voidFill.sortingOrder = 15;
            var voidEdgeGo = new GameObject("Edge");
            voidEdgeGo.transform.SetParent(voidGo.transform, false);
            var voidEdge = voidEdgeGo.AddComponent<SpriteRenderer>();
            voidEdge.sprite = white; voidEdge.color = new Color(0.55f, 0.55f, 0.62f, 0.9f); voidEdge.sortingOrder = 16;
            var voidRiser = voidGo.AddComponent<VoidRiser>();
            voidRiser.player = playerGo.transform;
            voidRiser.viewCamera = cam;
            voidRiser.streamer = streamer;

            // --- 時計(巻き戻しが止まる演出) --------------------
            //  HUD Canvas(ScreenSpaceOverlay)上に置く。ワールドのクレジット文字と
            //  スプライトのソート順が噛み合わない問題を避け、確実に前面へ。
            var clockFace = EnsureClockSprite();
            var clockGo = new GameObject("Clock", typeof(RectTransform));
            clockGo.transform.SetParent(canvasGo.transform, false);
            var clockRt = (RectTransform)clockGo.transform;
            clockRt.anchorMin = clockRt.anchorMax = new Vector2(0.5f, 0.5f);
            clockRt.pivot = new Vector2(0.5f, 0.5f);
            clockRt.anchoredPosition = new Vector2(0f, -24f);
            clockRt.sizeDelta = Vector2.one;

            // 背景を暗く(クレジットを沈める。子順で Face/Hand より下)
            var clockDimGo = new GameObject("Dim", typeof(RectTransform));
            clockDimGo.transform.SetParent(clockGo.transform, false);
            var clockDim = clockDimGo.AddComponent<UnityEngine.UI.Image>();
            clockDim.color = new Color(0f, 0f, 0f, 0f);
            clockDim.raycastTarget = false;
            var cdrt = clockDim.rectTransform;
            cdrt.anchorMin = cdrt.anchorMax = new Vector2(0.5f, 0.5f);
            cdrt.sizeDelta = new Vector2(4000f, 4000f);          // 画面全体を覆う

            // 文字盤
            var clockFaceGo = new GameObject("Face", typeof(RectTransform));
            clockFaceGo.transform.SetParent(clockGo.transform, false);
            var clockFaceImg = clockFaceGo.AddComponent<UnityEngine.UI.Image>();
            clockFaceImg.sprite = clockFace;
            clockFaceImg.color = new Color(1f, 1f, 1f, 0.96f);
            clockFaceImg.raycastTarget = false;
            var cfrt = clockFaceImg.rectTransform;
            cfrt.anchorMin = cfrt.anchorMax = new Vector2(0.5f, 0.5f);
            cfrt.sizeDelta = new Vector2(216f, 216f);            // 直径 ~216px

            // 針(下端を軸に回す)
            var clockPivotGo = new GameObject("HandPivot", typeof(RectTransform));
            clockPivotGo.transform.SetParent(clockGo.transform, false);
            var cprt = (RectTransform)clockPivotGo.transform;
            cprt.anchorMin = cprt.anchorMax = new Vector2(0.5f, 0.5f);
            cprt.sizeDelta = Vector2.one;
            var clockHandGo = new GameObject("Hand", typeof(RectTransform));
            clockHandGo.transform.SetParent(clockPivotGo.transform, false);
            var clockHandImg = clockHandGo.AddComponent<UnityEngine.UI.Image>();
            clockHandImg.sprite = white;
            clockHandImg.color = Color.white;
            clockHandImg.raycastTarget = false;
            var chrt = clockHandImg.rectTransform;
            chrt.anchorMin = chrt.anchorMax = new Vector2(0.5f, 0.5f);
            chrt.pivot = new Vector2(0.5f, 0f);                  // 下端が回転軸
            chrt.sizeDelta = new Vector2(6f, 84f);
            chrt.anchoredPosition = Vector2.zero;
            clockGo.SetActive(false);

            // --- 暗転 + 終幕(PLAYED BY / 名前) --------------------
            var blackoutGo = new GameObject("Blackout", typeof(RectTransform));
            blackoutGo.transform.SetParent(canvasGo.transform, false);
            var blackout = blackoutGo.AddComponent<UnityEngine.UI.Image>();
            blackout.color = new Color(0f, 0f, 0f, 0f);
            var brt = blackout.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            blackoutGo.SetActive(false);

            var finaleTextGo = new GameObject("FinaleText", typeof(TextMeshPro));
            var finaleText = finaleTextGo.GetComponent<TextMeshPro>();
            finaleText.font = font;
            finaleText.fontSize = 12f;
            finaleText.color = Color.white;
            finaleText.alignment = TMPro.TextAlignmentOptions.Center;
            finaleText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            finaleText.sortingOrder = 45;
            finaleTextGo.SetActive(false);

            // --- 音 ---------------------------------------------------
            stateGo.AddComponent<AudioManager>();

            // --- 破壊の副作用 --------------------------------------
            // 砂嵐は専用 Canvas に載せる(毎フレーム uvRect を変えるので、HUD Canvas に
            // 同居すると HUD 全体(TMP 含む)が毎フレーム再バッチされて重くなる)。
            var noiseCanvasGo = new GameObject("NoiseCanvas", typeof(RectTransform));
            var noiseCanvas = noiseCanvasGo.AddComponent<Canvas>();
            noiseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            noiseCanvas.sortingOrder = 90;   // HUD より上・Flash より下くらい
            noiseCanvasGo.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
                UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            var noiseGo = new GameObject("GraphicsNoise", typeof(RectTransform));
            noiseGo.transform.SetParent(noiseCanvasGo.transform, false);
            var noise = noiseGo.AddComponent<UnityEngine.UI.RawImage>();
            var nrt = noise.rectTransform;
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
            nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
            noise.raycastTarget = false;
            noise.enabled = false;

            var sabotage = stateGo.AddComponent<SabotageDirector>();
            sabotage.hud = hud;
            sabotage.graphicsNoise = noise;

            // --- 画面演出(トースト / フラッシュ) ------------------
            var flashGo = new GameObject("Flash", typeof(RectTransform));
            flashGo.transform.SetParent(canvasGo.transform, false);
            var flashImg = flashGo.AddComponent<UnityEngine.UI.Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;
            var frt = flashImg.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

            var toastGo = new GameObject("Toast", typeof(RectTransform));
            toastGo.transform.SetParent(canvasGo.transform, false);
            var toast = toastGo.AddComponent<TMPro.TextMeshProUGUI>();
            toast.font = font;
            toast.fontSize = 40f;
            toast.color = new Color(1f, 1f, 1f, 0f);
            toast.alignment = TMPro.TextAlignmentOptions.Center;
            toast.raycastTarget = false;
            toast.fontStyle = TMPro.FontStyles.Bold;
            var trt = toast.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 0.8f);
            trt.anchorMax = new Vector2(0.5f, 0.8f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(900f, 60f);

            // 「残り」表示の下に置く(クレジットは中央寄せなので左上は常に空いている)
            var bannerGo = new GameObject("Banner", typeof(RectTransform));
            bannerGo.transform.SetParent(canvasGo.transform, false);
            var banner = bannerGo.AddComponent<TMPro.TextMeshProUGUI>();
            banner.font = font;
            banner.fontSize = 22f;
            banner.color = new Color(1f, 1f, 1f, 0f);
            banner.alignment = TMPro.TextAlignmentOptions.TopLeft;
            banner.raycastTarget = false;
            var brt2 = banner.rectTransform;
            brt2.anchorMin = new Vector2(0f, 1f);
            brt2.anchorMax = new Vector2(0f, 1f);
            brt2.pivot = new Vector2(0f, 1f);
            brt2.anchoredPosition = new Vector2(26f, -62f);
            brt2.sizeDelta = new Vector2(420f, 40f);

            var vigGo = new GameObject("Vignette", typeof(RectTransform));
            vigGo.transform.SetParent(canvasGo.transform, false);
            var vigImg = vigGo.AddComponent<UnityEngine.UI.Image>();
            vigImg.color = new Color(0f, 0f, 0.02f, 0f);
            vigImg.raycastTarget = false;
            var vrt = vigImg.rectTransform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;

            var screenFx = stateGo.AddComponent<ScreenFx>();
            screenFx.flash = flashImg;
            screenFx.toast = toast;
            screenFx.banner = banner;
            screenFx.vignette = vigImg;

            // --- 終幕 -----------------------------------------------
            var finaleC = stateGo.AddComponent<Finale>();
            finaleC.blackout = blackout;
            finaleC.worldText = finaleText;
            finaleC.player = playerGo.transform;
            finaleC.viewCamera = cam;
            finaleC.streamer = streamer;
            finaleC.hud = hud;

            // --- 進行(登り→巻き戻し→降り→最下部→終幕) ----------
            var flow = stateGo.AddComponent<GameFlow>();
            flow.player = playerGo.transform;
            flow.playerController = pc;
            flow.weapon = weapon;
            flow.cameraFollow = follow;
            flow.viewCamera = cam;
            flow.streamer = streamer;
            flow.voidRiser = voidRiser;
            flow.finale = finaleC;
            flow.dim = clockDim;
            flow.clockRoot = clockGo.transform;
            flow.clockPivot = clockPivotGo.transform;

            stateGo.AddComponent<ColliderViz>();   // F1 で当たり判定を可視化(開発用)

            // 描画順: HUD / ノイズ / フラッシュ / トースト / 暗転
            vigGo.transform.SetSiblingIndex(1);       // クレジットより上、他の演出より下
            noiseGo.transform.SetSiblingIndex(2);
            clockGo.transform.SetSiblingIndex(3);     // 時計はノイズ/暗みより上、フラッシュ/トースト/暗転より下
            flashGo.transform.SetAsLastSibling();
            bannerGo.transform.SetAsLastSibling();
            toastGo.transform.SetAsLastSibling();
            blackoutGo.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Log("saved scene " + ScenePath);

            SyncBuildScenes();
        }

        static void SyncBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),   // index 0 = 起動シーン
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }

        // ---------------------------------------------------------------
        // タイトル画面(ロゴ / なまえ / PLAY / 設定)
        // ---------------------------------------------------------------
        static void BuildTitleScene(TMP_FontAsset font, Sprite white, Sprite logo)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            var mgr = new GameObject("Title");
            mgr.AddComponent<AudioManager>();
            var ts = mgr.AddComponent<TitleScreen>();

            // ロゴ(ドット絵・2段組)。縦長なので幅は控えめ・少し上寄せ。
            var logoGo = new GameObject("Logo");
            logoGo.transform.position = new Vector3(0f, 2.6f, 0f);
            var logoSr = logoGo.AddComponent<SpriteRenderer>();
            logoSr.sprite = logo;
            logoSr.sortingOrder = 5;
            float logoScale = 7.4f / Mathf.Max(1, logo.rect.width) * 16f;   // 目標幅 ~7.4u
            logoGo.transform.localScale = Vector3.one * logoScale;
            ts.logo = logoSr;

            // フレーバー(ロゴの下、間を空ける)
            var flavGo = new GameObject("Flavor", typeof(TextMeshPro));
            var flav = flavGo.GetComponent<TextMeshPro>();
            flav.font = font; flav.fontSize = 3.2f; flav.alignment = TMPro.TextAlignmentOptions.Center;
            flav.color = new Color(0.55f, 0.6f, 0.72f);
            flav.text = "エンドロールの いちばん下から。";
            flavGo.transform.position = new Vector3(0f, -0.55f, 0f);
            ts.flavor = flav;

            // --- Canvas (なまえ入力 / PLAY / 設定) ---
            var canvasGo = new GameObject("UI", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            // === なまえ入力ステップ(PLAY を押すと出る。初期は非表示)===
            var nameStepGo = MakeUI(canvasGo.transform, "NamePanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 540));
            ts.namePanel = nameStepGo.gameObject;

            // なまえ ラベル
            var nameLblGo = MakeUI(nameStepGo, "NameLabel", new Vector2(0.5f, 0.5f), new Vector2(0, 44), new Vector2(560, 34));
            var nameLbl = nameLblGo.gameObject.AddComponent<TextMeshProUGUI>();
            nameLbl.font = font; nameLbl.fontSize = 22f; nameLbl.color = new Color(0.6f, 0.66f, 0.78f);
            nameLbl.alignment = TMPro.TextAlignmentOptions.Center;
            nameLbl.text = "なまえ";

            // なまえ入力
            var nameRow = MakeUI(nameStepGo, "NameRow", new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(520, 56));
            var nameBg = nameRow.gameObject.AddComponent<UnityEngine.UI.Image>();
            nameBg.color = new Color(1f, 1f, 1f, 0.07f);
            var nameField = nameRow.gameObject.AddComponent<TMP_InputField>();

            var viewport = FillRect(nameRow, "Viewport", 16, 6);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

            var nameTextGo = FillRect(viewport, "Text", 0, 0);
            var nameText = nameTextGo.gameObject.AddComponent<TextMeshProUGUI>();
            nameText.font = font; nameText.fontSize = 26f; nameText.color = Color.white;
            nameText.alignment = TMPro.TextAlignmentOptions.Left;
            nameText.richText = false;
            nameText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            var placeGo = FillRect(viewport, "Placeholder", 0, 0);
            var placeText = placeGo.gameObject.AddComponent<TextMeshProUGUI>();
            placeText.font = font; placeText.fontSize = 24f;
            placeText.color = new Color(1f, 1f, 1f, 0.32f);
            placeText.text = "英字 かな カナ 漢字 記号 数字";
            placeText.alignment = TMPro.TextAlignmentOptions.Left;
            placeText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            nameField.textViewport = viewport;
            nameField.textComponent = nameText;
            nameField.placeholder = placeText;
            nameField.fontAsset = font;
            nameField.pointSize = 26f;
            nameField.lineType = TMP_InputField.LineType.SingleLine;
            nameField.characterLimit = 16;
            nameField.targetGraphic = nameBg;
            ts.nameField = nameField;

            // はじめる ボタン(なまえステップ)
            var goGo = MakeUI(nameStepGo, "Begin", new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(240, 52));
            var goBtn = goGo.gameObject.AddComponent<UnityEngine.UI.Button>();
            var goImg = goGo.gameObject.AddComponent<UnityEngine.UI.Image>();
            goImg.color = new Color(0.22f, 0.5f, 0.95f, 0.16f);
            goBtn.targetGraphic = goImg;
            var goTxtGo = MakeUI(goGo, "L", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 52));
            var goTxt = goTxtGo.gameObject.AddComponent<TextMeshProUGUI>();
            goTxt.font = font; goTxt.fontSize = 24f; goTxt.color = new Color(0.8f, 0.88f, 1f);
            goTxt.alignment = TMPro.TextAlignmentOptions.Center; goTxt.text = "はじめる";
            // ※ AddListener の登録はシーン保存で消える。必ず永続リスナで。
            UnityEditor.Events.UnityEventTools.AddPersistentListener(goBtn.onClick, ts.Confirm);

            // === タイトルステップ: PLAY ===
            var playGo = MakeUI(canvasGo.transform, "Play", new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(300, 62));
            ts.playButton = playGo.gameObject;
            var playBtn = playGo.gameObject.AddComponent<UnityEngine.UI.Button>();
            var playImg = playGo.gameObject.AddComponent<UnityEngine.UI.Image>();
            playImg.color = new Color(0.22f, 0.5f, 0.95f, 0.18f);
            playBtn.targetGraphic = playImg;
            var playTxtGo = MakeUI(playGo, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 62));
            var playLabel = playTxtGo.gameObject.AddComponent<TextMeshProUGUI>();
            playLabel.font = font; playLabel.fontSize = 30f; playLabel.color = new Color(0.8f, 0.88f, 1f);
            playLabel.alignment = TMPro.TextAlignmentOptions.Center;
            playLabel.text = "PLAY";
            ts.playLabel = playLabel;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(playBtn.onClick, ts.OpenNameStep);

            // 設定ボタン
            var setGo = MakeUI(canvasGo.transform, "SettingsBtn", new Vector2(1f, 1f), new Vector2(-90, -34), new Vector2(140, 44));
            ts.settingsButton = setGo.gameObject;
            var setBtn = setGo.gameObject.AddComponent<UnityEngine.UI.Button>();
            var setImg = setGo.gameObject.AddComponent<UnityEngine.UI.Image>();
            setImg.color = new Color(1f, 1f, 1f, 0.05f);
            setBtn.targetGraphic = setImg;
            var setTxtGo = MakeUI(setGo, "L", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 44));
            var setTxt = setTxtGo.gameObject.AddComponent<TextMeshProUGUI>();
            setTxt.font = font; setTxt.fontSize = 22f; setTxt.color = new Color(0.7f, 0.75f, 0.85f);
            setTxt.alignment = TMPro.TextAlignmentOptions.Center; setTxt.text = "設定";
            UnityEditor.Events.UnityEventTools.AddPersistentListener(setBtn.onClick, ts.ToggleSettings);

            // 設定パネル(ロゴに被らないよう中央やや下・不透明。奥に画面全体の暗幕)
            var panelRoot = MakeUI(canvasGo.transform, "SettingsPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000));
            var scrim = panelRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.62f);
            ts.settingsPanel = panelRoot.gameObject;
            var panelGo = MakeUI(panelRoot, "Box", new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(470, 210));
            var panelImg = panelGo.gameObject.AddComponent<UnityEngine.UI.Image>();
            panelImg.color = new Color(0.04f, 0.045f, 0.06f, 1f);
            ts.bgmSlider = MakeSlider(panelGo, font, "BGM 音量", new Vector2(0, 55));
            ts.sfxSlider = MakeSlider(panelGo, font, "効果音 音量", new Vector2(0, -15));
            var closeGo = MakeUI(panelGo, "Close", new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(160, 40));
            var closeBtn = closeGo.gameObject.AddComponent<UnityEngine.UI.Button>();
            var closeImg = closeGo.gameObject.AddComponent<UnityEngine.UI.Image>();
            closeImg.color = new Color(1f, 1f, 1f, 0.06f);
            closeBtn.targetGraphic = closeImg;
            var closeTxtGo = MakeUI(closeGo, "L", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 40));
            var closeTxt = closeTxtGo.gameObject.AddComponent<TextMeshProUGUI>();
            closeTxt.font = font; closeTxt.fontSize = 20f; closeTxt.color = Color.white;
            closeTxt.alignment = TMPro.TextAlignmentOptions.Center; closeTxt.text = "とじる";
            UnityEditor.Events.UnityEventTools.AddPersistentListener(closeBtn.onClick, ts.ToggleSettings);

            // ヒント(なまえステップの下)
            var hintGo = MakeUI(nameStepGo, "Hint", new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(600, 40));
            var hintTxt = hintGo.gameObject.AddComponent<TextMeshProUGUI>();
            hintTxt.font = font; hintTxt.fontSize = 20f; hintTxt.color = new Color(1f, 0.6f, 0.5f, 0f);
            hintTxt.alignment = TMPro.TextAlignmentOptions.Center;
            ts.hint = hintTxt;

            nameStepGo.gameObject.SetActive(false);   // 初期は非表示(PLAY で出る)

            // 起動フェード(黒→透明)。Canvas の最前面。
            var fadeGo = MakeUI(canvasGo.transform, "BootFade", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000));
            var fadeImg = fadeGo.gameObject.AddComponent<UnityEngine.UI.Image>();
            fadeImg.color = Color.black;
            fadeGo.SetAsLastSibling();
            ts.fadeOverlay = fadeImg;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TitleScenePath);
            Log("saved scene " + TitleScenePath);
        }

        // 親いっぱいに広がる子(左右上下の内側マージン付き)
        static RectTransform FillRect(Transform parent, string name, float padX, float padY)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
            return rt;
        }

        static RectTransform MakeUI(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static UnityEngine.UI.Slider MakeSlider(Transform parent, TMP_FontAsset font, string label, Vector2 pos)
        {
            var row = MakeUI(parent, label, new Vector2(0.5f, 0.5f), pos, new Vector2(400, 44));
            var lblGo = MakeUI(row, "L", new Vector2(0f, 0.5f), new Vector2(0, 0), new Vector2(160, 40), new Vector2(0, 0.5f));
            var lbl = lblGo.gameObject.AddComponent<TextMeshProUGUI>();
            lbl.font = font; lbl.fontSize = 18f; lbl.color = new Color(0.75f, 0.8f, 0.9f);
            lbl.alignment = TMPro.TextAlignmentOptions.Left; lbl.text = label;

            var sldGo = MakeUI(row, "S", new Vector2(1f, 0.5f), new Vector2(-10, 0), new Vector2(210, 20), new Vector2(1f, 0.5f));
            var slider = sldGo.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var bg = MakeUI(sldGo, "BG", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210, 6));
            bg.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.15f);
            var fillArea = MakeUI(sldGo, "Fill Area", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210, 6));
            var fill = MakeUI(fillArea, "Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0, 6), new Vector2(0, 0.5f));
            var fillImg = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = new Color(0.5f, 0.7f, 1f, 0.7f);
            var handle = MakeUI(sldGo, "Handle", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 20));
            handle.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.gameObject.GetComponent<UnityEngine.UI.Image>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            return slider;
        }

        static Sprite EnsureClockSprite()
        {
            const string path = "Assets/Art/clock.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                var px = new Color32[64 * 64];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 40);
                tex.SetPixels32(px); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Bilinear;
            imp.spritePixelsPerUnit = 16;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite EnsureLogoSprite()
        {
            if (!File.Exists(LogoPngPath))
            {
                // 仮ロゴ(scripts/make_logo.py で本物を差し替える)
                var tex = new Texture2D(160, 40, TextureFormat.RGBA32, false);
                var px = new Color32[160 * 40];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
                tex.SetPixels32(px); tex.Apply();
                File.WriteAllBytes(LogoPngPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(LogoPngPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(LogoPngPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.spritePixelsPerUnit = 16;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(LogoPngPath);
        }

        // ---------------------------------------------------------------
        static void EnsureDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void EnsureLayer(string name)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (el.stringValue == name) return;
            }
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    Log("added layer " + name + " at " + i);
                    return;
                }
            }
            Debug.LogWarning("[Bootstrap] 空きレイヤースロットが無い: " + name);
        }

        static void Log(string msg) => Debug.Log("[Bootstrap] " + msg);

        static string Trunc(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n);
        }
    }
}
