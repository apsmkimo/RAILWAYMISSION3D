using UnityEngine;

namespace RailwayMission.Core
{
    public enum Language { ZhTW, En }

    /// <summary>簡易雙語系統（繁體中文／英文）。</summary>
    public static class Loc
    {
        const string PrefKey = "rm_language";
        static Language? _current;

        public static Language Current
        {
            get
            {
                if (!_current.HasValue) _current = (Language)PlayerPrefs.GetInt(PrefKey, DefaultLanguage());
                return _current.Value;
            }
            set
            {
                _current = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        static int DefaultLanguage()
        {
            var sys = Application.systemLanguage;
            bool zh = sys == SystemLanguage.Chinese || sys == SystemLanguage.ChineseTraditional || sys == SystemLanguage.ChineseSimplified;
            return zh ? (int)Language.ZhTW : (int)Language.En;
        }

        public static string T(string zh, string en) => Current == Language.ZhTW ? zh : en;

        public static void Toggle() => Current = Current == Language.ZhTW ? Language.En : Language.ZhTW;
    }
}
