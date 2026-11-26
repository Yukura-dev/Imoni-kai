using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Imoni.UI.Editor
{
    public static class ImoniProgressBarRepair
    {
        [MenuItem("Imoni/Game/Fix Progress Bars")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");

            const string path = "Assets/Prefabs/UI/GameScreen.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            int count = 0;
            try
            {
                var controller = root.GetComponent<GameScreenController>();
                foreach (var view in controller.requirements)
                {
                    Configure(view.progress, "ProgressTrack");
                    count++;
                }
                foreach (var view in controller.resources)
                {
                    Configure(view.progressFill, "Fill");
                    count++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Refresh loaded instances too, including inactive harvest bars.
            foreach (var fill in Resources.FindObjectsOfTypeAll<RoundedProgressFill>())
                if (fill && fill.gameObject.scene.IsValid()) fill.Refresh();
            SceneView.RepaintAll();
            Debug.Log($"IMONI_PROGRESS_BARS_FIXED: {count} bars now use sliced rounded ends.");
        }

        private static void Configure(Image image, string trackName)
        {
            if (!image) throw new InvalidOperationException("Missing progress Image.");
            var track = image.transform.parent.Find(trackName) as RectTransform;
            if (!track) throw new InvalidOperationException($"Missing track for {image.name}.");
            var fill = image.GetComponent<RoundedProgressFill>();
            if (!fill) fill = image.gameObject.AddComponent<RoundedProgressFill>();
            fill.Configure(track);
        }
    }
}
