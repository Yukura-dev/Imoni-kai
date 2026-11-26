namespace Imoni.Game.UI.Screens
{
    public static class ScreenCatalog
    {
        public static void Register(ScreenRouter router, ScreenContext app)
        {
            router.Register("Main", () => new MainScreen(app));
            router.Register("Endless", () => new EndlessScreen(app));
            router.Register("Tutorial1", () => new Tutorial1Screen(app));
            router.Register("Tutorial2", () => new Tutorial2Screen(app));
            router.Register("Tutorial3", () => new Tutorial3Screen(app));
            router.Register("Home", () => new HomeScreen(app));
            router.Register("Help", () => new HelpScreen(app));
            router.Register("Lobby", () => new LobbyScreen(app));
            router.Register("Waiting", () => new WaitingScreen(app));
            router.Register("Loading", () => new LoadingScreen(app));
            router.Register("Member", () => new MemberScreen(app));
            router.Register("Ranking", () => new RankingScreen(app));
            router.Register("Game", () => new GameScreen(app));
            router.Register("Imoni", () => new ImoniEffectScreen(app));
            router.Register("ImoniFail", () => new ImoniEffectScreen(app));
            router.Register("Result", () => new ResultScreen(app));
            router.Register("Clear", () => new ResultScreen(app));
            router.Register("GameOver", () => new GameOverScreen(app));
        }
    }
}
