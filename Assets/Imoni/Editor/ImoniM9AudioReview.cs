using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Imoni.Core.Audio;
using Imoni.Core.Effects;
using Imoni.Game;
using Imoni.Game.Audio;
using Imoni.Game.UI;
using Imoni.Game.UI.Screens;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Imoni.Editor
{
    /// <summary>Real-time DSP observation. No simulated clock advances are used for this audio evidence.</summary>
    [InitializeOnLoad]
    public static class ImoniM9AudioReview
    {
        private const string RequestPath = "Library/M9Audio.request", ResultPath = "Library/M9Audio.result";
        private const string ProgressPath = "Library/M9Audio-progress.json", EvidencePath = "Library/M9-audio-events.json";
        private sealed class Step
        {
            internal string Name;
            internal Action Begin, Verify;
            internal Func<double> Wait;
        }
        private static readonly List<Step> steps = new List<Step>();
        private static readonly JArray observations = new JArray(), checks = new JArray();
        private static Bootstrap Boot => UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
        private static AudioService Audio => AudioService.Instance;
        private static JObject Settings => (JObject)UiTheme.Data["audioReview"];
        private static JObject AudioSettingsData => (JObject)UiTheme.Data["audio"];
        private static bool running, includeLoopRepeat, savedSound, savedManual, restoreNeeded;
        private static string savedStyle, gameKey;
        private static double savedBgm, savedEffects, startedAt, stepStartedAt, stepWait, nextSample;
        private static double firstGameStarted, firstGameLoop, pauseDsp;
        private static int pauseLoopSample, pauseIntroSample;
        private static float savedTimeScale;
        private static int currentStep;
        private static double Crossfade => AudioSettingsData["bgmCrossfadeSeconds"].Value<double>();
        private static double Settle => Settings["settleSeconds"].Value<double>();
        private static double MaxVolume => AudioSettingsData["maxVolume"].Value<double>();

        static ImoniM9AudioReview() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (File.Exists(RequestPath))
                {
                    JObject request;
                    try { request = JObject.Parse(File.ReadAllText(RequestPath)); File.Delete(RequestPath); }
                    catch (IOException) { return; }
                    string command = request["command"]?.Value<string>();
                    if (command == "run") Start(request);
                    else if (command == "status") WriteResult(running ? "running" : "idle");
                    else if (command == "cancel") { if (running) Finish("cancelled"); else WriteResult("idle"); }
                    else throw new ArgumentException("Use M9 audio command run, status, or cancel.");
                }
                if (!running) return;
                Require(EditorApplication.isPlaying && Boot?.Music != null && Audio, "Boot stopped during the real-time audio review.");
                double now = EditorApplication.timeSinceStartup;
                if (now >= nextSample)
                {
                    observations.Add(Describe("sample"));
                    nextSample = now + Settings["sampleIntervalSeconds"].Value<double>();
                    WriteProgress();
                }
                if (now - stepStartedAt < stepWait) return;
                Step completed = steps[currentStep];
                completed.Verify?.Invoke();
                checks.Add(new JObject { ["step"] = completed.Name, ["passed"] = true, ["elapsedSeconds"] = now - startedAt });
                observations.Add(Describe("verified"));
                currentStep++;
                if (currentStep == steps.Count) { Finish("complete"); return; }
                EnterStep();
            }
            catch (Exception exception)
            {
                Finish("failed", exception.ToString());
                Debug.LogError("M9 real-time audio review failed: " + exception);
            }
        }

        private static void Start(JObject request)
        {
            Require(!running, "A real-time audio review is already running.");
            Require(EditorApplication.isPlaying && Boot?.Router != null && Boot.Music != null && Audio, "Start Boot in Play Mode first.");
            string library = Path.GetFullPath("Library") + Path.DirectorySeparatorChar;
            Require(Path.GetFullPath(Boot.Context.SavePath).StartsWith(library, StringComparison.OrdinalIgnoreCase),
                "Use an isolated Library profile, for example M6Review open, before this routing fixture.");
            Require(!Audio.ApplicationPaused && !AudioListener.pause, "Resume application audio before starting review.");
            savedSound = Audio.SoundEnabled; savedBgm = Audio.BgmVolume; savedEffects = Audio.EffectsVolume;
            savedStyle = Boot.Music.StyleOverride; savedManual = Audio.BgmManualTime; savedTimeScale = Time.timeScale;
            restoreNeeded = true; Audio.BgmManualTime = false;
            Time.timeScale = 1; Audio.ClearBgmEventLog(); Audio.StopBgm();
            includeLoopRepeat = request["includeLoopRepeat"]?.Value<bool>() ?? true;
            observations.Clear(); checks.Clear(); steps.Clear(); currentStep = 0;
            BuildSteps(); running = true; startedAt = EditorApplication.timeSinceStartup; nextSample = startedAt;
            EnterStep(); WriteResult("running");
        }

        private static void BuildSteps()
        {
            Add("home-wa", () => { Show("Home"); Boot.Music.SetStyle("wa"); FullVolume(); }, () => Crossfade + Settle,
                () => RequirePlaying(HomeKey("wa"), "loop"));
            Add("home-band-crossfade", () => Boot.Music.SetStyle("band"), () => Crossfade + Settle,
                () => RequirePlaying(HomeKey("band"), "loop"));
            Add("home-wa-crossfade", () => Boot.Music.SetStyle("wa"), () => Crossfade + Settle,
                () => RequirePlaying(HomeKey("wa"), "loop"));
            Add("game-intro", () =>
            {
                PrepareGame();
                BgmVoiceState voice = Target(); gameKey = voice.TrackKey;
                Require(voice.IntroSamples > 0, "The formal game track must begin with its supplied intro.");
                firstGameStarted = voice.StartedAtDsp; firstGameLoop = voice.LoopAtDsp;
                double expected = (double)voice.IntroSamples / voice.SampleRate;
                Require(Math.Abs((firstGameLoop - firstGameStarted) - expected) <= Settings["dspToleranceSeconds"].Value<double>(),
                    "The loop was not scheduled at the exact intro sample boundary.");
            }, () => Crossfade + Settle, () => RequirePlaying(gameKey, "intro"));
            Add("background-pause-during-intro", () =>
            {
                SetApplicationPause(true); pauseDsp = AudioSettings.dspTime; pauseIntroSample = Target().IntroTimeSamples;
            }, () => Settings["pauseSeconds"].Value<double>(), () =>
            {
                Require(Audio.ApplicationPaused && AudioListener.pause, "Background state did not pause the intro.");
                Require(Math.Abs(AudioSettings.dspTime - pauseDsp) <= Settings["dspToleranceSeconds"].Value<double>(), "DSP advanced during the paused intro.");
                Require(Math.Abs(Target().IntroTimeSamples - pauseIntroSample) <= Settings["sampleTolerance"].Value<int>(), "Intro samples advanced while backgrounded.");
                Require(Target().Phase == "intro", "A pending loop started during background pause."); RequireSameGameStart();
            });
            Add("background-resume-during-intro", () => SetApplicationPause(false), () => Settle, () =>
            {
                Require(!Audio.ApplicationPaused && AudioSettings.dspTime > pauseDsp, "Intro DSP did not resume.");
                RequirePlaying(gameKey, "intro"); RequireSameGameStart();
            });
            Add("game-intro-to-loop", null, () => WaitUntilDsp(firstGameLoop + Settle), () =>
            {
                RequirePlaying(gameKey, "loop"); RequireSameGameStart();
                Require(Audio.BgmEventLog.Any(item => item.Kind == "intro-start" && item.ObservedTrackKey == gameKey), "No observed intro start.");
                Require(Audio.BgmEventLog.Any(item => item.Kind == "loop-start" && item.ObservedTrackKey == gameKey), "No observed loop start.");
            });
            if (includeLoopRepeat)
                Add("game-loop-repeat", null, () => WaitUntilDsp(firstGameLoop + LoopSeconds(Target()) + Settle), () =>
                {
                    RequirePlaying(gameKey, "loop"); RequireSameGameStart();
                    Require(Target().CompletedLoops >= 1 && Audio.BgmEventLog.Any(item => item.Kind == "loop-repeat" && item.ObservedTrackKey == gameKey),
                        "The main loop did not complete a real DSP cycle.");
                });
            Add("help-from-game-keeps-track", () => Boot.Context.OpenHelp(), () => Settle, () =>
            {
                Require(Boot.Router.OverlaySourceScreen == "Game" && Boot.Router.VisibleScreen == "Help", "Help is not a Game overlay.");
                RequirePlaying(gameKey, "loop"); RequireSameGameStart();
            });
            Add("help-return-keeps-track", () => Boot.Context.CloseHelp(), () => Settle, () =>
            { Require(Boot.Router.VisibleScreen == "Game", "Help did not return to Game."); RequirePlaying(gameKey, "loop"); RequireSameGameStart(); });
            Add("background-pause", () =>
            {
                SetApplicationPause(true); pauseDsp = AudioSettings.dspTime; pauseLoopSample = Target().LoopTimeSamples;
            }, () => Settings["pauseSeconds"].Value<double>(), () =>
            {
                Require(Audio.ApplicationPaused && AudioListener.pause, "Background state did not pause the listener.");
                Require(Math.Abs(AudioSettings.dspTime - pauseDsp) <= Settings["dspToleranceSeconds"].Value<double>(), "DSP advanced while backgrounded.");
                Require(Math.Abs(Target().LoopTimeSamples - pauseLoopSample) <= Settings["sampleTolerance"].Value<int>(), "Loop samples advanced while backgrounded.");
                Require(Target().EffectiveGain == 0, "Background snapshot reports audible BGM.");
            });
            Add("background-resume", () => SetApplicationPause(false), () => Settle, () =>
            {
                Require(!Audio.ApplicationPaused && !AudioListener.pause && AudioSettings.dspTime > pauseDsp, "DSP did not resume.");
                RequirePlaying(gameKey, "loop"); RequireSameGameStart();
            });
            Add("imoni-fade-stop", () =>
            {
                // Settle through the server. Both result routes are then opened solely to test their music policy.
                Boot.Context.Server.Tick(UiTheme.Config.DurationSeconds);
                Show("Imoni");
                if (Boot.Router.CurrentView is ImoniEffectScreen effect) effect.Effect.ManualTime = true;
            }, () => AudioSettingsData["bgmEffectFadeOutSeconds"].Value<double>() + Settle, RequireStopped);
            Add("result-home-restart", () => Show("Result"), () => Crossfade + Settle,
                () => RequirePlaying(HomeKey("wa"), "loop"));
            Add("next-game-new-intro", PrepareGame, () => Crossfade + Settle, () =>
            {
                RequirePlaying(gameKey, "intro");
                Require(Target().StartedAtDsp > firstGameLoop, "Returning from Result did not restart the game intro.");
            });
            Add("imoni-fail-fade-stop", () =>
            {
                Boot.Context.Server.Tick(UiTheme.Config.DurationSeconds);
                Show("ImoniFail");
                if (Boot.Router.CurrentView is ImoniEffectScreen effect) effect.Effect.ManualTime = true;
            }, () => AudioSettingsData["bgmEffectFadeOutSeconds"].Value<double>() + Settle, RequireStopped);
            Add("game-over-home-restart", () => Show("GameOver"), () => Crossfade + Settle,
                () => RequirePlaying(HomeKey("wa"), "loop"));
            Add("time-scale-zero-crossfade", () => { Time.timeScale = 0; Boot.Music.SetStyle("band"); }, () => Crossfade + Settle,
                () => { Require(Time.timeScale == 0, "Review time scale changed."); RequirePlaying(HomeKey("band"), "loop"); });
            Add("retarget-mid-crossfade", () => Boot.Music.SetStyle("wa"), () => Crossfade * AudioSettingsData["volumeStep"].Value<double>(), () =>
            {
                BgmVoiceState[] before = Audio.BgmSnapshot();
                Require(Audio.BgmIsTransitioning && before.Count(voice => voice.Weight > 0) > 1, "No overlapping crossfade to retarget.");
                Boot.Music.SetStyle("band");
                BgmVoiceState[] after = Audio.BgmSnapshot();
                foreach (BgmVoiceState voice in before)
                    Require(after.Single(item => item.TrackKey == voice.TrackKey).Gain == voice.Gain, "Retargeting changed a live source gain immediately.");
            });
            Add("retarget-completed", null, () => Crossfade + Settle, () => RequirePlaying(HomeKey("band"), "loop"));
            Add("bgm-volume-independent", () => Audio.SetPreferences(true, AudioSettingsData["volumeStep"].Value<double>(), MaxVolume), () => Settle, () =>
            {
                RequireMixer("bgmVolumeParameter", Audio.BgmVolume);
                RequireMixer("effectsVolumeParameter", MaxVolume);
                Require(Target().EffectiveGain == Target().Gain * Audio.BgmVolume && Target().EffectiveGain > 0, "Saved BGM amplitude was not applied once.");
            });
            Add("bgm-zero-keeps-effects", () => Audio.SetPreferences(true, 0, MaxVolume), () => Settle, () =>
            {
                Require(Audio.BgmSources.All(source => source.mute), "Zero BGM volume is not muted.");
                Require(Audio.Voices.All(source => !source.mute) && !Audio.DroneSource.mute, "BGM mute affected effects.");
                Require(Target().Started && Target().LoopIsPlaying && Target().EffectiveGain == 0, "BGM mute rewound or remained audible.");
                RequireMixer("effectsVolumeParameter", MaxVolume);
            });
            Add("effects-zero-keeps-bgm", () => Audio.SetPreferences(true, MaxVolume, 0), () => Settle, () =>
            {
                RequirePlaying(HomeKey("band"), "loop");
                Require(Audio.Voices.All(source => source.mute) && Audio.DroneSource.mute, "Zero effects volume was not applied.");
                RequireMixer("bgmVolumeParameter", MaxVolume);
            });
            Add("sound-off", () => Audio.SetPreferences(false, MaxVolume, MaxVolume), () => Settle, () =>
            {
                Require(Audio.BgmSources.All(source => source.mute) && Audio.Voices.All(source => source.mute) && Audio.DroneSource.mute,
                    "The global sound switch did not mute both buses.");
                Require(Target().EffectiveGain == 0, "The global sound switch left audible BGM.");
            });
            Add("sound-on", FullVolume, () => Settle, () => RequirePlaying(HomeKey("band"), "loop"));
        }

        private static void Add(string name, Action begin, Func<double> wait, Action verify) =>
            steps.Add(new Step { Name = name, Begin = begin, Wait = wait, Verify = verify });
        private static void EnterStep()
        {
            Step step = steps[currentStep]; step.Begin?.Invoke();
            stepStartedAt = EditorApplication.timeSinceStartup; stepWait = Math.Max(0, step.Wait());
            observations.Add(Describe("begin")); WriteProgress();
        }
        private static void Show(string screen) { Require(Boot.Router.Show(screen), "Missing route: " + screen); }
        private static void PrepareGame()
        {
            Boot.Context.PrepareGame(UiTheme.Data["effectReview"]["seed"].Value<int>());
            Show("Game"); ((GameScreen)Boot.Router.CurrentView).ReviewPaused = true; FullVolume();
        }
        private static void FullVolume() => Audio.SetPreferences(true, MaxVolume, MaxVolume);
        private static string HomeKey(string style) => new BgmPlan(UiTheme.Data).Select("Home", styleOverride: style).TrackKey;
        private static BgmVoiceState Target() => Audio.BgmSnapshot().Single(voice => voice.TrackKey == Audio.BgmTargetKey);
        private static double LoopSeconds(BgmVoiceState state) => (double)state.LoopSamples / state.SampleRate;
        private static double WaitUntilDsp(double time) => Math.Max(0, time - AudioSettings.dspTime);
        private static void RequireSameGameStart()
        { Require(Target().StartedAtDsp == firstGameStarted && Target().LoopAtDsp == firstGameLoop, "The current game track restarted unexpectedly."); }
        private static void RequirePlaying(string key, string phase)
        {
            Require(Audio.BgmTargetKey == key && !Audio.BgmIsTransitioning, "The expected BGM did not settle: " + key);
            BgmVoiceState voice = Target();
            Require(voice.Phase == phase && voice.Started && voice.Weight == 1 && !voice.Muted && voice.EffectiveGain > 0,
                "The expected track/phase is not audible: " + key + " / " + phase);
            Require(phase == "intro" ? voice.IntroIsPlaying : voice.LoopIsPlaying, "The selected AudioSource is not playing.");
            Require(Audio.BgmSnapshot().Where(item => item.TrackKey != key).All(item => !item.Started && item.Gain == 0),
                "An old track remained active after the fade.");
            Require(Audio.BgmSources.All(source => source.outputAudioMixerGroup == Audio.BgmGroup), "A BGM source bypasses its mixer bus.");
        }
        private static void RequireStopped()
        {
            Require(Audio.BgmTargetKey == null && !Audio.BgmIsTransitioning, "The effect did not finish its BGM fade-out.");
            Require(Audio.BgmSnapshot().All(item => !item.Started && item.Gain == 0) && Audio.BgmSources.All(source => !source.isPlaying),
                "An effect left BGM or its scheduled intro/loop playing.");
        }
        private static void RequireMixer(string parameterKey, double volume)
        {
            Require(Audio.Mixer.GetFloat(AudioSettingsData[parameterKey].Value<string>(), out float actual), "Missing mixer parameter.");
            float expected = (float)ImoniAudioPlan.MasterDecibels(UiTheme.Data, true, volume);
            Require(actual == expected, "Mixer volume differs from saved amplitude: " + parameterKey);
        }
        private static void SetApplicationPause(bool value) => typeof(AudioService)
            .GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Audio, new object[] { value });
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private static JObject Describe(string kind)
        {
            var data = new JObject { ["kind"] = kind, ["step"] = currentStep < steps.Count ? steps[currentStep].Name : null,
                ["elapsedSeconds"] = EditorApplication.timeSinceStartup - startedAt, ["dspTimeSeconds"] = AudioSettings.dspTime,
                ["screen"] = Boot?.Router?.VisibleScreen, ["sourceScreen"] = Boot?.Router?.OverlaySourceScreen,
                ["timeScale"] = Time.timeScale, ["applicationPaused"] = Audio ? Audio.ApplicationPaused : false,
                ["listenerPaused"] = AudioListener.pause, ["soundEnabled"] = Audio ? Audio.SoundEnabled : false,
                ["bgmVolume"] = Audio ? Audio.BgmVolume : 0, ["effectsVolume"] = Audio ? Audio.EffectsVolume : 0,
                ["targetKey"] = Audio?.BgmTargetKey, ["transitioning"] = Audio ? Audio.BgmIsTransitioning : false,
                ["voices"] = JArray.FromObject(Audio ? Audio.BgmSnapshot() : Array.Empty<BgmVoiceState>()) };
            if (Audio && Audio.Mixer)
            {
                var mixer = new JObject();
                foreach (string key in new[] { "masterVolumeParameter", "bgmVolumeParameter", "effectsVolumeParameter" })
                    if (Audio.Mixer.GetFloat(AudioSettingsData[key].Value<string>(), out float value)) mixer[key] = value;
                data["mixerDecibels"] = mixer;
            }
            return data;
        }
        private static void WriteProgress() => File.WriteAllText(ProgressPath, new JObject { ["status"] = running ? "running" : "idle",
            ["step"] = currentStep < steps.Count ? steps[currentStep].Name : null, ["stepIndex"] = currentStep,
            ["stepCount"] = steps.Count, ["elapsedSeconds"] = EditorApplication.timeSinceStartup - startedAt,
            ["remainingStepSeconds"] = Math.Max(0, stepWait - (EditorApplication.timeSinceStartup - stepStartedAt)) }.ToString());
        private static void WriteResult(string status, string failure = null) => File.WriteAllText(ResultPath, new JObject {
            ["status"] = status, ["step"] = currentStep < steps.Count ? steps[currentStep].Name : null,
            ["passedChecks"] = checks.Count, ["totalChecks"] = steps.Count,
            ["evidence"] = Path.GetFullPath(EvidencePath), ["failure"] = failure }.ToString());
        private static void Finish(string status, string failure = null)
        {
            running = false;
            File.WriteAllText(EvidencePath, new JObject { ["status"] = status, ["utc"] = DateTime.UtcNow.ToString("O"),
                ["clock"] = "Real Editor time plus AudioSettings.dspTime; no manual advancement or seeking.",
                ["scope"] = "Audio routing fixture on an isolated profile. Result/GameOver are opened over a server-settled round; this is not round-success gameplay proof.",
                ["backgroundCheck"] = "OnApplicationPause invoked on AudioService in Editor; physical Android lifecycle remains a device check.",
                ["includeLoopRepeat"] = includeLoopRepeat, ["elapsedSeconds"] = EditorApplication.timeSinceStartup - startedAt,
                ["checks"] = checks, ["observations"] = observations,
                ["events"] = JArray.FromObject(Audio ? Audio.BgmEventLog : Array.Empty<BgmPlaybackEvent>()), ["failure"] = failure }.ToString());
            if (restoreNeeded)
            {
                restoreNeeded = false; Time.timeScale = savedTimeScale;
                if (Audio)
                {
                    if (Audio.ApplicationPaused) SetApplicationPause(false);
                    Audio.BgmManualTime = savedManual; Audio.SetPreferences(savedSound, savedBgm, savedEffects);
                }
                if (Boot?.Music != null) Boot.Music.SetStyle(savedStyle);
            }
            WriteResult(status, failure); WriteProgress();
        }
    }
}
