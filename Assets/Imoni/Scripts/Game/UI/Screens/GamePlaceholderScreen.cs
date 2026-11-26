namespace Imoni.Game.UI.Screens
{
    /// <summary>M3 owns the playable world. This route intentionally contains only its empty surface.</summary>
    public sealed class GamePlaceholderScreen : ScreenView
    {
        public GamePlaceholderScreen(ScreenContext app) : base(app, "Game", "UI/ScreenLayout") { }
    }
}
