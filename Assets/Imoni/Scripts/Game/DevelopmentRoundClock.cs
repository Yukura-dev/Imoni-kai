using System;
using System.Linq;
using Imoni.Game.UI;
using Newtonsoft.Json.Linq;

namespace Imoni.Game
{
    /// <summary>Only development sessions can accelerate the server clock; effects keep real time.</summary>
    public static class DevelopmentRoundClock
    {
        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.SessionState.GetBool("Imoni.ShortRounds", false);
#elif DEVELOPMENT_BUILD
                return enabled;
#else
                return false;
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.SessionState.SetBool("Imoni.ShortRounds", value);
#elif DEVELOPMENT_BUILD
                enabled = value;
#endif
            }
        }
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        private static bool enabled = Environment.GetCommandLineArgs().Contains("--imoni-short-rounds", StringComparer.Ordinal);
#endif
        public static float Scale => Enabled ? UiTheme.Data["round"]["mockTimeScale"].Value<float>() : 1f;
    }
}
