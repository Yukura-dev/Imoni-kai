using System;
using System.Collections;
using System.IO;
using System.Numerics;
using Imoni.Core.Effects;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Imoni.Game.Effects
{
    /// <summary>Isolated preview input; does not read or change a player's settled round.</summary>
    public sealed class ImoniEffectPreview : MonoBehaviour
    {
        public ImoniEffectView view;
        public bool failed;

        private void Start()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("ImoniEffect.Capture", false))
            {
                UnityEditor.SessionState.SetBool("ImoniEffect.Capture", false);
                StartCoroutine(CaptureBoth());
                return;
            }
#endif
            BeginPreview(failed);
        }

        private JObject Review => (JObject)JObject.Parse(view.gameConfig.text)["effectReview"];
        private void BeginPreview(bool failure)
        {
            var review = Review;
            view.Begin(BigInteger.Parse((string)review[failure ? "failureScore" : "successScore"]),
                BigInteger.Parse((string)review["successScore"]), failure,
                (string)review["failureMissing"], () => view.Hide(), (int)review["seed"]);
        }

#if UNITY_EDITOR
        private IEnumerator CaptureBoth()
        {
            var review = Review;
            int width = (int)review["width"], height = (int)review["height"], fps = (int)review["frameRate"];
            int count = (int)Math.Ceiling((double)review["durationMs"] * fps / 1000);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../design/videos/unity"));
            Directory.CreateDirectory(output);
            float previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / fps;
            view.ManualTime = true;
            try
            {
                foreach (bool failure in new[] { false, true })
                {
                    string name = failure ? "imoni_fail" : "imoni_success";
                    string frames = Path.Combine(output, name + "_frames");
                    Directory.CreateDirectory(frames);
                    BeginPreview(failure);
                    // Allow panel layout and font atlas population to settle before time zero.
                    for (int warmup = 0; warmup < 3; warmup++)
                    {
                        view.SampleAt(0);
                        yield return new WaitForEndOfFrame();
                    }
                    if (Screen.width != width || Screen.height != height)
                        throw new InvalidOperationException($"Game view must be {width}x{height}; got {Screen.width}x{Screen.height}.");
                    var telemetry = new JArray();
                    for (int frame = 0; frame < count; frame++)
                    {
                        double elapsedMs = frame * 1000.0 / fps;
                        view.SampleAt(elapsedMs);
                        yield return new WaitForEndOfFrame();
                        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
                        try
                        {
                            if (capture.width != width || capture.height != height)
                                throw new InvalidOperationException("Captured Game view dimensions changed.");
                            File.WriteAllBytes(Path.Combine(frames, frame.ToString("D5") + ".png"), capture.EncodeToPNG());
                        }
                        finally { Object.Destroy(capture); }
                        var timeline = view.Timeline;
                        telemetry.Add(new JObject { ["frame"] = frame, ["timeMs"] = elapsedMs,
                            ["level"] = timeline.Level, ["score"] = timeline.Score.ToString(),
                            ["finaleAtMs"] = timeline.FinaleAtMs.HasValue ? new JValue(timeline.FinaleAtMs.Value) : JValue.CreateNull(),
                            ["resultVisible"] = timeline.ResultVisible });
                    }
                    File.WriteAllText(Path.Combine(output, name + "_telemetry.json"), telemetry.ToString());
                    Debug.Log("IMONI_EFFECT_CAPTURED: " + name + ", " + count + " Game-view frames at " + width + "x" + height);
                }
                File.WriteAllText(Path.Combine(output, "capture-complete.txt"), DateTime.UtcNow.ToString("O"));
            }
            finally
            {
                Time.captureDeltaTime = previousCaptureDelta;
                UnityEditor.EditorApplication.isPlaying = false;
            }
        }
#endif
    }
}
