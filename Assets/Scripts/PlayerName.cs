using UnityEngine;

namespace L8000
{
    /// <summary>タイトルで入力したプレイヤー名。シーンをまたいで保持。</summary>
    public static class PlayerName
    {
        const string Key = "l8000_player_name";
        static string _cached;

        public static string Value
        {
            get
            {
                if (string.IsNullOrEmpty(_cached))
                    _cached = PlayerPrefs.GetString(Key, "");
                return _cached;
            }
            set
            {
                _cached = value ?? "";
                PlayerPrefs.SetString(Key, _cached);
                PlayerPrefs.Save();
            }
        }

        /// <summary>表示用。空なら控えめなプレースホルダ。</summary>
        public static string Display => string.IsNullOrWhiteSpace(Value) ? Localization.T("finale_default_name") : Value.Trim();
    }
}
