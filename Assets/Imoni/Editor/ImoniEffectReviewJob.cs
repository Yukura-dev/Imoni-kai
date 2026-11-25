using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Imoni.Editor
{
    // Narrow local editor job interface for reproducible setup/capture without UI coordinates.
    [InitializeOnLoad]
    public static class ImoniEffectReviewJob
    {
        private const string Request = "Library/ImoniEffectReview.request";
        static ImoniEffectReviewJob() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                if (command == "setup") ImoniSetup.SetupProject();
                else if (command == "record") ImoniSetup.RecordBoth();
                else if (command == "game") EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
                else if (command == "validate") global::Imoni.UI.Editor.ImoniRoundFlowValidation.Validate();
                else throw new InvalidOperationException("Unknown effect review command.");
                File.WriteAllText("Library/ImoniEffectReview.result", command + ": complete");
            }
            catch (Exception exception)
            {
                File.WriteAllText("Library/ImoniEffectReview.result", command + ": " + exception);
                Debug.LogException(exception);
            }
        }
    }
}
