using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Imoni.UI.Editor
{
    public static class ImoniRoundFlowMigration
    {
        [MenuItem("Imoni/Round Flow/Build and Verify")]
        public static void ApplyAndVerify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before updating the round flow.");
            ImoniRoundResultBuilder.Build();
            ImoniGameBuilder.UpdateRoundEnd();
            ImoniRoundFlowValidation.Validate();
            ImoniMockV2Validation.Validate();
            ImoniRoundFlowPreview.Capture();
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("IMONI_ROUND_FLOW_COMPLETE: cooking, success/failure results, continuation and retry saved and validated.");
        }

        // Editor-only integration aids: the normal Update timer invokes EndRound on the next frame.
        [MenuItem("Imoni/Round Flow/Finish Current Round (Success)")]
        public static void FinishSuccess() => Finish(true);

        [MenuItem("Imoni/Round Flow/Finish Current Round (Failure)")]
        public static void FinishFailure() => Finish(false);

        [MenuItem("Imoni/Round Flow/Finish Current Round (Success)", true)]
        [MenuItem("Imoni/Round Flow/Finish Current Round (Failure)", true)]
        public static bool CanFinish() => EditorApplication.isPlaying &&
            UnityEngine.Object.FindFirstObjectByType<GameScreenController>() != null;

        private static void Finish(bool success)
        {
            if (!CanFinish()) return;
            var controller = UnityEngine.Object.FindFirstObjectByType<GameScreenController>();
            var type = typeof(GameScreenController);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var inventory = (int[])type.GetField("quantities", flags).GetValue(controller);
            Array.Clear(inventory, 0, inventory.Length);
            inventory[0] = success ? 70 : 12;
            inventory[3] = success ? 24 : 6;
            inventory[6] = success ? 18 : 3;
            inventory[9] = success ? 48 : 4;
            type.GetField("remainingTime", flags).SetValue(controller, 0f);
        }
    }
}
