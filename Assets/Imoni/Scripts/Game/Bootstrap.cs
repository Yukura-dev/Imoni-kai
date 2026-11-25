using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace Imoni.Game
{
    /// <summary>Boot scene references. Runtime startup is added in later milestones.</summary>
    [DisallowMultipleComponent]
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Grid worldGrid;
        [SerializeField] private Tilemap worldTilemap;

        public UIDocument UiDocument => uiDocument;
        public Camera WorldCamera => worldCamera;
        public Grid WorldGrid => worldGrid;
        public Tilemap WorldTilemap => worldTilemap;

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
