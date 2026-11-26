using Imoni.Game;
using UnityEditor;

namespace Imoni.Editor
{
    public static class ImoniDevelopmentMenus
    {
        private const string Path = "Imoni/Development/Short Rounds (GameConfig)";

        [MenuItem(Path)]
        private static void ToggleShortRounds()
        {
            DevelopmentRoundClock.Enabled = !DevelopmentRoundClock.Enabled;
            Menu.SetChecked(Path, DevelopmentRoundClock.Enabled);
        }

        [MenuItem(Path, true)]
        private static bool ValidateShortRounds()
        {
            Menu.SetChecked(Path, DevelopmentRoundClock.Enabled);
            return true;
        }
    }
}
