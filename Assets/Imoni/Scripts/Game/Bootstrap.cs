using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using Imoni.Game.UI;

namespace Imoni.Game
{
    /// <summary>Boot references and UI Toolkit lifetime. M2a exposes only the development gallery.</summary>
    [DisallowMultipleComponent]
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Grid worldGrid;
        [SerializeField] private Tilemap worldTilemap;
        [SerializeField] private bool showComponentGallery = true;
        private ScreenRouter router;
        private SafeAreaAdapter safeArea;

        public UIDocument UiDocument => uiDocument;
        public Camera WorldCamera => worldCamera;
        public Grid WorldGrid => worldGrid;
        public Tilemap WorldTilemap => worldTilemap;
        public ScreenRouter Router => router;

        private void OnEnable()
        {
            if (!uiDocument) return;
            VisualElement root = uiDocument.rootVisualElement;
            root.Clear();
            UiTheme.Apply(root, "UI/GalleryLayout");
            root.style.flexGrow = 1;
            root.AddToClassList("imoni-app");
            var safe = new VisualElement { name = "SafeArea" };
            safe.style.flexGrow = 1;
            var host = new VisualElement { name = "ScreenHost" };
            host.style.flexGrow = 1; host.style.minHeight = 0;
            safe.Add(host); root.Add(safe);
            safeArea = new SafeAreaAdapter(safe);
            router = new ScreenRouter(host);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            router.RegisterResource("ComponentGallery", "UI/ComponentGallery");
            if (showComponentGallery) router.Show("ComponentGallery");
#endif
        }

        private void OnDisable()
        {
            safeArea?.Dispose(); safeArea = null;
            router?.Dispose(); router = null;
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
        }
#endif
    }
}
