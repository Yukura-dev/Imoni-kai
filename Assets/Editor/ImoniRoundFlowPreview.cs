using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Imoni.UI.Editor
{
    public static class ImoniRoundFlowPreview
    {
        [MenuItem("Imoni/Round Flow/Capture Result Previews")]
        public static void Capture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RoundResultScreen.prefab");
            if (prefab && prefab.GetComponent<RoundResultController>()?.effectView)
            {
                // Camera.Render does not capture a screen-space UIDocument. Record the actual Game view instead.
                Debug.Log("IMONI_EFFECT_PREVIEW_MOVED: Use Imoni > Effect > Record Success and Failure for the UI Toolkit effect and the reference-video comparison. No legacy cooking PNGs were written.");
                return;
            }
            using var state = ImoniRoundFlowValidation.PreserveSessionState();
            Sample(true);
            Render("round_cooking", 3.5f, false);
            Render("round_cooking_success", 7f, false);
            Render("round_success", 7f, true);
            Sample(false);
            Render("round_cooking_failure", 7f, false);
            Render("round_failure", 7f, true);
            Debug.Log("IMONI_ROUND_PREVIEWS_SAVED: Library/MockV2Preview/round_*.png");
        }

        private static void Sample(bool success)
        {
            ImoniRoundSession.BeginNewChallenge();
            var round = ImoniRoundSession.ConsumeRoundStart();
            var quantities = new int[12];
            quantities[0] = success ? 70 : 12;
            quantities[3] = success ? 24 : 6;
            quantities[6] = success ? 18 : 3;
            quantities[9] = success ? 48 : 4;
            ImoniRoundSession.CompleteRound(round, quantities, 700, new[] { 1, 0, 0, 0 });
        }

        private static void Render(string name, float seconds, bool showResult)
        {
            ImoniMockV2Preview.Render("RoundResultScreen", name, root =>
            {
                var c = root.GetComponent<RoundResultController>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(RoundResultController).GetMethod("Start", flags).Invoke(c, null);
                typeof(RoundResultController).GetMethod("AdvanceCooking", flags).Invoke(c, new object[] { seconds });
                if (showResult) c.ShowResult();
            });
        }
    }
}
