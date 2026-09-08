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
        const string ScenePath     = "Assets/Scenes/Main.unity";
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
                var streamer0 = UnityEngine.Object.FindFirstObjectByType<RollStreamer>();
                var roll0 = streamer0 != null ? streamer0.Roll : null;
                float topY = roll0 != null ? roll0.TopY : 0f;
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
                        Log($"mid t{t:0.0}s: started={(follow != null && follow.Started)} camY={(cam != null ? cam.transform.position.y : 0):0.0} (中央=~{topY * 0.5f:0.0})");
                    }
                }
                else
                {
                    Log($"stage0 done. started={(follow != null && follow.Started)}");
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
            var streamer = UnityEngine.Object.FindFirstObjectByType<RollStreamer>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            int liveLines = UnityEngine.Object.FindObjectsByType<CreditLineBuilder>(FindObjectsSortMode.None)
                .Count(b => b.Active);
            int liveBoxes = UnityEngine.Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Length;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("--- L8000 diagnostics ---");
            if (streamer != null)
                sb.AppendLine($"roll lines={streamer.Roll?.Lines.Count} bottomY={streamer.BottomY:0.0} topY={streamer.TopY:0.0}");
            sb.AppendLine($"live credit lines={liveLines}  total BoxCollider2D={liveBoxes}");
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

                BuildScene(layout, font, white, credits);

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
            if (layout.gapUnit <= 0f) { layout.gapUnit = 0.6f; fixedAny = true; }
            if (layout.cellHeight <= 0f) { layout.cellHeight = 1f; fixedAny = true; }
            if (layout.colliderHeightRatio <= 0f) { layout.colliderHeightRatio = 0.9f; fixedAny = true; }
            if (fixedAny) { EditorUtility.SetDirty(layout); AssetDatabase.SaveAssets(); Log("layout: 既定値を補正"); }
            return layout;
        }

        static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) { Log("font asset exists"); return existing; }

            var srcFont = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
            if (srcFont == null) throw new Exception("マルモニカ TTF が見つからない: " + FontTtfPath);

            // ピクセルフォントなので SDF ではなく RASTER。滲ませない。
            var fontAsset = TMP_FontAsset.CreateFontAsset(
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
            SetAtlasPointFilter(fontAsset);

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);
            Log("created " + FontAssetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
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

            // --- Roll ----------------------------------------------------
            var rollGo = new GameObject("CreditsRoll");
            var streamer = rollGo.AddComponent<RollStreamer>();
            streamer.layout = layout;
            streamer.font = font;
            streamer.creditsFile = credits;
            streamer.viewCamera = cam;
            streamer.groundLayerName = "Ground";

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
            outline.transform.localScale = new Vector3((PlayerW + 0.12f) / visSprite, (PlayerH + 0.12f) / visSprite, 1f);
            var osr = outline.AddComponent<SpriteRenderer>();
            osr.sprite = white;
            osr.color = Color.black;
            osr.sortingOrder = 9;

            var rb = playerGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = playerGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(PlayerW - 0.05f, PlayerH - 0.05f);
            col.edgeRadius = 0.025f;
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
            weapon.bulletColliderRadius = 0.16f;
            weapon.bulletVisualHeight = 0.42f;
            weapon.bulletSpeed = 9f;
            weapon.fireCooldown = 0.22f;
            weapon.muzzleOffsetY = PlayerH * 0.5f + 0.1f;

            streamer.player = playerGo.transform;

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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Log("saved scene " + ScenePath);

            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath))
            {
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
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
