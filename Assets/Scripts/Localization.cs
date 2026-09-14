using System;
using System.Collections.Generic;
using UnityEngine;

namespace L8000
{
    public enum Lang { JA, EN }

    /// <summary>
    /// ゲーム全体の言語。タイトル画面右上のボタンで切り替え、PlayerPrefs に保存して
    /// シーンをまたいで保持する。表示中のテキストは OnChanged を購読して即座に更新する。
    /// </summary>
    public static class Localization
    {
        const string Key = "l8000_lang";

        public static event Action OnChanged;

        static Lang _current;
        static bool _loaded;

        public static Lang Current
        {
            get { EnsureLoaded(); return _current; }
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _current = PlayerPrefs.GetInt(Key, 0) == 1 ? Lang.EN : Lang.JA;
        }

        public static void Set(Lang lang)
        {
            EnsureLoaded();
            if (_current == lang) return;
            _current = lang;
            PlayerPrefs.SetInt(Key, lang == Lang.EN ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public static void Toggle() => Set(Current == Lang.JA ? Lang.EN : Lang.JA);

        /// <summary>キーに対応する現在言語の文字列。キーが無ければキー自体を返す。</summary>
        public static string T(string key)
        {
            EnsureLoaded();
            return _table.TryGetValue(key, out var pair) ? (_current == Lang.JA ? pair.ja : pair.en) : key;
        }

        static readonly Dictionary<string, (string ja, string en)> _table = new Dictionary<string, (string ja, string en)>
        {
            ["hud_remaining"] = ("残り {0}", "Remaining {0}"),

            ["title_name_hint"] = ("なまえ を いれてください", "Please enter a name"),
            ["title_settings"] = ("設定", "Settings"),

            ["flow_esc_confirm"] = ("もう一度 Esc で タイトルへ", "Press Esc again for Title"),
            ["flow_rewind_stop"] = ("― 巻き戻しが 止まる ―", "― The rewind stops ―"),
            ["flow_descending"] = ("▼ もどっている", "▼ Descending"),
            ["flow_bottom_prompt"] = ("↓ Enter で この線を 抜ける", "↓ Enter to break through this line"),

            ["weapon_restored"] = ("― もどした ―", "― Restored ―"),

            ["sab_bgm"] = ("音が 歪む", "The music warps"),
            ["sab_bgm_restore"] = ("音", "Music"),
            ["sab_sfx"] = ("効果音", "Sound effects"),
            ["sab_physics"] = ("物理", "Physics"),
            ["sab_graphics"] = ("描画", "Rendering"),
            ["sab_localization"] = ("翻訳", "Localization"),
            ["sab_ui"] = ("UI", "UI"),
            ["sab_lighting"] = ("照明", "Lighting"),
            ["sab_camera"] = ("画面の水平", "The camera's level"),
            ["sab_writing"] = ("言葉", "The words"),
            ["sab_producer"] = ("進行管理", "Production"),
            ["sab_verb_lost"] = ("消失", "is lost"),
            ["sab_verb_back"] = ("もどる", "returns"),

            ["finale_played_by"] = ("操作した人", "PLAYED BY"),
            ["finale_default_name"] = ("名も無き登坂者", "A nameless climber"),
            ["finale_bad_zero"] = ("指が止まった。ここまでだった。", "The fingers stopped. This is as far as it went."),
            ["finale_bad_some"] = ("動けなくなって、名前ごと沈んだ。", "Unable to move, it sank along with the names."),
            ["finale_good_few"] = ("いくつかの名前は、もう戻らない。", "A few names never came back."),
            ["finale_good_many"] = ("多くの名前が、無くなった。", "Many names are gone now."),
            ["finale_good_most"] = ("ほとんど、何も残らなかった。それでも、戻ってきた。", "Almost nothing remained. And yet, it came back."),
        };
    }
}
