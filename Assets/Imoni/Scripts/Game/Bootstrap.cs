using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using Imoni.Game.UI;
using Imoni.Game.UI.Screens;
using Imoni.Game.Audio;

namespace Imoni.Game
{
    /// <summary>Boot references, shared style scope, and out-of-game screen lifetime.</summary>
    [DisallowMultipleComponent]
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Grid worldGrid;
        [SerializeField] private Tilemap worldTilemap;
        [SerializeField] private bool showComponentGallery;
        private ScreenRouter router;
        private SafeAreaAdapter safeArea;

        public UIDocument UiDocument => uiDocument;
        public Camera WorldCamera => worldCamera;
        public Grid WorldGrid => worldGrid;
        public Tilemap WorldTilemap => worldTilemap;
        public ScreenRouter Router => router;
        public SafeAreaAdapter SafeArea => safeArea;
        public ScreenContext Context { get; private set; }

        private void OnEnable()
        {
            if (!uiDocument) return;
            VisualElement root = uiDocument.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            var host = new VisualElement { name = "ScreenHost" };
            UiTheme.InstallRoot(host);
            // Separate opaque edge strips leave only the actual map aperture transparent.
            var covers = new VisualElement { name = "SafeAreaCovers", pickingMode = PickingMode.Ignore };
            covers.AddToClassList("safe-area-covers");
            foreach (string edge in new[] { "Top", "Bottom", "Left", "Right" })
            {
                var cover = new VisualElement { name = "Safe" + edge + "Cover", pickingMode = PickingMode.Ignore };
                cover.AddToClassList("safe-area-cover"); cover.AddToClassList("safe-area-cover--" + edge.ToLowerInvariant());
                covers.Add(cover);
            }
            host.Add(covers);
            var safe = new VisualElement { name = "SafeArea" };
            safe.style.flexGrow = 1; safe.style.minHeight = 0;
            var content = new VisualElement { name = "ScreenContent" };
            content.style.flexGrow = 1; content.style.minHeight = 0;
            safe.Add(content); host.Add(safe); root.Add(host);
            safeArea = new SafeAreaAdapter(safe, covers);
            router = new ScreenRouter(content);
            router.ScreenChanged += screen => host.EnableInClassList("imoni-app--game", screen == "Game");
            string profilePath = null;
#if UNITY_EDITOR
            profilePath = UnityEditor.SessionState.GetString("Imoni.ReviewSavePath", string.Empty);
            UnityEditor.SessionState.EraseString("Imoni.ReviewSavePath");
            if (string.IsNullOrEmpty(profilePath)) profilePath = null;
#endif
            AudioService.Ensure(gameObject, UiTheme.Assets, UiTheme.Data);
            Context = new ScreenContext(router, path: profilePath, bootstrap: this);
            ScreenCatalog.Register(router, Context);
            string start = "Main";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            router.RegisterResource("ComponentGallery", "UI/ComponentGallery");
            if (showComponentGallery) start = "ComponentGallery";
#endif
#if UNITY_EDITOR
            start = UnityEditor.SessionState.GetString("Imoni.StartScreen", start);
            UnityEditor.SessionState.EraseString("Imoni.StartScreen");
#endif
            router.Show(start);
        }

        private void Update()
        {
            // One owner advances authoritative time; world rendering never calls Tick.
            if (router?.CurrentView is GameScreen game) game.Advance(Time.unscaledDeltaTime * DevelopmentRoundClock.Scale);
        }

        private void OnDisable()
        {
            safeArea?.Dispose(); safeArea = null;
            router?.Dispose(); router = null;
            AudioService.Instance?.StopAll();
            Context = null;
        }

        public void ShowComponentGallery()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            router?.Show("ComponentGallery");
#endif
        }

#if UNITY_EDITOR
        public void SetSceneReferences(UIDocument document, Camera camera, Grid grid, Tilemap tilemap)
        {
            uiDocument = document;
            worldCamera = camera;
            worldGrid = grid;
            worldTilemap = tilemap;
            showComponentGallery = false;
        }
#endif
    }
}
