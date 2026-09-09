using UnityEditor;
using UnityEditor.SceneManagement;

namespace L8000.EditorTools
{
    /// <summary>
    /// エディタで Play を押したとき、どのシーンを開いていても必ず Title 画面から始める。
    /// (playModeStartScene を Title.unity に固定。Stop すると元のシーンに戻る)
    ///
    /// メニュー  L8000 / Play From Title  で ON/OFF。
    /// Main を直接デバッグしたいときは OFF にする。設定は EditorPrefs に保存。
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromTitle
    {
        const string TitlePath = "Assets/Scenes/Title.unity";
        const string MenuPath  = "L8000/Play From Title";
        const string PrefKey   = "l8000_play_from_title";

        static PlayFromTitle()
        {
            // ドメインリロード後に反映(AssetDatabase が使えるタイミングで)
            EditorApplication.delayCall += Apply;
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static void Apply()
        {
            if (Enabled)
            {
                var titleScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitlePath);
                EditorSceneManager.playModeStartScene = titleScene;
            }
            else if (EditorSceneManager.playModeStartScene != null &&
                     AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == TitlePath)
            {
                EditorSceneManager.playModeStartScene = null;
            }
            Menu.SetChecked(MenuPath, Enabled);
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
