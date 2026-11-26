using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Imoni.Core;
using Imoni.Core.Effects;
using Imoni.Game.UI;
using Imoni.Game.UI.Components;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using BigInteger = System.Numerics.BigInteger;

namespace Imoni.Game.Effects
{
    /// <summary>Presentation only. The round is already settled before this view begins.</summary>
    [DisallowMultipleComponent]
    public sealed class ImoniEffectView : MonoBehaviour
    {
        public UIDocument document;
        public TextAsset gameConfig;
        public VisualTreeAsset visualTree;
        public StyleSheet themeStyle;
        public StyleSheet effectStyle;
        public Texture2D[] textures;
        /// <summary>Router-owned mount. Legacy standalone previews can still supply a UIDocument.</summary>
        public VisualElement Host { get; set; }
        public bool ManualTime { get; set; }
        public ImoniEffectTimeline Timeline { get; private set; }
        public ImoniEffectConfig Config { get; private set; }
        public bool SoundEnabled { get; private set; }
        public int IngredientPoolCount => ingredientElements.Count;
        public int BurstPoolCount => burstElements.Count;
        public int RingPoolCount => ringElements.Count;
        public int LinePoolCount => lines.Count;
        public int SparklePoolCount => sparks.Count;
        public event Action<ImoniEffectEvent> EffectEvent;
        public event Action<bool> SoundChanged;
        public event Action Replayed;
        public event Action Hidden;

        private static readonly string[] IngredientNames = { "taro", "negi", "mushroom", "beef", "konnyaku" };
        private static readonly string[] StarNames = { "yellow", "pink", "blue", "green", "purple", "orange" };
        private readonly Dictionary<string, Texture2D> artwork = new Dictionary<string, Texture2D>();
        private readonly Dictionary<VisualElement, Texture2D> imageSources = new Dictionary<VisualElement, Texture2D>();
        private ImoniEffectGrayscale grayscale;
        private Color worldBackground;
        private bool worldBackgroundCaptured;
        public int GrayscaleTextureCount => grayscale?.TextureCount ?? 0;
        private readonly List<FallingItem> ingredientElements = new List<FallingItem>();
        private readonly List<StarBurst> burstElements = new List<StarBurst>();
        private readonly List<GlowRing> ringElements = new List<GlowRing>();
        private readonly List<Line> lines = new List<Line>();
        private readonly List<Spark> sparks = new List<Spark>();
        private JObject layout;
        private GameConfig displayConfig;
        private VisualElement screen, world, rainbow, sunburst, pillar, glow, pot, broth, flash, gray, dark;
        private VisualElement ingredientsLayer, starsLayer, ringsLayer, banner, finale, actions, gaugeFill;
        private Label scoreLabel, levelLabel, bannerText, soundLabel, finaleHeading, finaleOne, finaleTwo, missingLabel;
        private Label bannerGlow;
        private VisualElement soundIcon;
        private Button resultButton;
        private Action onResult;
        private BigInteger targetScore, referenceScore;
        private bool failed, active, externalSoundPreference;
        private string missing;
        private int seed, emittedEvents;
        private double startedAt;

        private sealed class Line
        {
            public VisualElement Element;
            public double Base, Delay;
            public Color Color;
        }
        private sealed class Spark
        {
            public VisualElement Element;
            public double Y, Duration, FloatDuration, Delay;
            public Color Tint;
        }

        public void Begin(BigInteger finalScore, BigInteger successReferenceScore, bool isFailed,
            string missingMaterials, Action result, int effectSeed = 7, bool? soundEnabled = null)
        {
            targetScore = finalScore; referenceScore = successReferenceScore; failed = isFailed;
            missing = missingMaterials ?? string.Empty; onResult = result; seed = effectSeed;
            if (!document) document = GetComponent<UIDocument>();
            if (!gameConfig || !visualTree || !themeStyle || !effectStyle)
                throw new InvalidOperationException("Run Imoni > Setup Project to bind the effect resources.");
            Config = ImoniEffectConfig.FromJson(gameConfig.text);
            displayConfig = GameConfig.FromJson(gameConfig.text);
            layout = (JObject)Config.Data["layout"] ?? throw new InvalidOperationException("imoniEffect.layout is missing.");
            externalSoundPreference = soundEnabled.HasValue;
            SoundEnabled = soundEnabled ?? (PlayerPrefs.GetInt("Imoni.SoundEnabled", 1) != 0);
            Build();
            Replay();
        }

        public void Replay()
        {
            if (Config == null) return;
            Timeline = new ImoniEffectTimeline(Config, targetScore, referenceScore, failed, seed);
            startedAt = Time.unscaledTimeAsDouble;
            emittedEvents = 0; active = true;
            screen.style.display = DisplayStyle.Flex;
            Replayed?.Invoke();
            SampleAt(0);
        }

        public void Hide()
        {
            var wasActive = active;
            active = false;
            if (screen != null) screen.style.display = DisplayStyle.None;
            if (wasActive) Hidden?.Invoke();
        }

        private void OnDisable() => Hide();
        private void OnDestroy() { grayscale?.Dispose(); grayscale = null; }

        private void Update()
        {
            if (active && !ManualTime) SampleAt((Time.unscaledTimeAsDouble - startedAt) * N("millisecondsPerSecond"));
        }

        /// <summary>The recorder and runtime use precisely the same rendering path.</summary>
        public void SampleAt(double elapsedMs)
        {
            if (Timeline == null) return;
            if (elapsedMs < Timeline.NowMs)
            {
                Timeline = new ImoniEffectTimeline(Config, targetScore, referenceScore, failed, seed);
                emittedEvents = 0;
                Replayed?.Invoke();
            }
            Timeline.AdvanceTo(elapsedMs);
            Render(Timeline.Evaluate());
            while (emittedEvents < Timeline.Events.Count)
            {
                var effectEvent = Timeline.Events[emittedEvents++];
                EffectEvent?.Invoke(effectEvent);
            }
        }

        private void Build()
        {
            grayscale?.Dispose();
            grayscale = new ImoniEffectGrayscale(UiTheme.Assets ? UiTheme.Assets.imoniEffectGrayscaleShader : null);
            worldBackgroundCaptured = false;
            imageSources.Clear();
            artwork.Clear();
            foreach (var texture in textures ?? Array.Empty<Texture2D>()) if (texture) artwork[texture.name] = texture;
            var root = Host ?? (document ? document.rootVisualElement : null);
            if (root == null) throw new InvalidOperationException("The effect requires a router host or preview document.");
            root.Clear();
            if (Host != null) UiTheme.Apply(root);
            else if (!HasStyleInAncestors(root, themeStyle)) root.styleSheets.Add(themeStyle);
            if (!root.styleSheets.Contains(effectStyle)) root.styleSheets.Add(effectStyle);
            visualTree.CloneTree(root);
            screen = root.Q("EffectScreen"); Rect(screen, "screen");
            world = screen.Q("World"); rainbow = screen.Q("Rainbow"); sunburst = screen.Q("Sunburst");
            pillar = screen.Q("Pillar"); glow = screen.Q("RainbowGlow");
            pot = screen.Q("PotGroup"); broth = screen.Q("BrothGlow");
            flash = screen.Q("Flash"); gray = screen.Q("GrayOverlay"); dark = screen.Q("DarkOverlay");
            ingredientsLayer = screen.Q("Ingredients"); starsLayer = screen.Q("Stars"); ringsLayer = screen.Q("Rings");
            banner = screen.Q("LevelBanner"); finale = screen.Q("Finale"); actions = screen.Q("Actions");
            scoreLabel = screen.Q<Label>("ScoreLabel"); levelLabel = screen.Q<Label>("LevelLabel");
            bannerText = screen.Q<Label>("BannerText"); soundLabel = screen.Q<Label>("SoundLabel");
            finaleHeading = screen.Q<Label>("FinaleHeading"); finaleOne = screen.Q<Label>("FinaleLineOne");
            finaleTwo = screen.Q<Label>("FinaleLineTwo"); missingLabel = screen.Q<Label>("MissingLabel");
            soundIcon = screen.Q("SoundIcon"); resultButton = screen.Q<Button>("ResultButton");
            Image(rainbow, "fx_rainbow_scroll"); Rect(rainbow, "rainbow");
            Image(sunburst, "fx_sunburst");
            var size = N("sunburstSize");
            Rect(sunburst, Config.V("centerX") - size / 2, Config.V("potY") - size / 2, size, size);
            Image(pillar, "fx_pillar"); Image(glow, "fx_rainbow_glow");
            Rect(pot, "pot");
            var origin = A("potOriginPercent");
            pot.style.transformOrigin = new TransformOrigin(Length.Percent((float)origin[0]), Length.Percent((float)origin[1]));
            Image(broth, "fx_broth_glow"); Rect(broth, "broth");
            var potImage = screen.Q("Pot"); Image(potImage, "pot_effect");
            var potRect = A("pot"); Rect(potImage, 0, 0, potRect[2], potRect[3]);
            Image(flash, "fx_flash");
            SetupHud(); SetupTitles(); SetupActions(); SetupPools();
            UpdateSound();
        }

        private void SetupHud()
        {
            var sound = screen.Q<Button>("SoundButton"); Rect(sound, "sound");
            sound.style.minHeight = 0; sound.style.fontSize = (float)N("smallFont");
            Round(sound, N("pillRadius")); Border(sound, N("borderWidth"));
            sound.clicked += () =>
            {
                SoundEnabled = !SoundEnabled;
                if (!externalSoundPreference)
                { PlayerPrefs.SetInt("Imoni.SoundEnabled", SoundEnabled ? 1 : 0); PlayerPrefs.Save(); }
                UpdateSound(); SoundChanged?.Invoke(SoundEnabled);
            };
            soundIcon.style.width = (float)N("soundIconSize"); soundIcon.style.height = (float)N("soundIconSize");
            soundIcon.style.marginRight = (float)N("soundIconGap");
            var skip = screen.Q<Button>("SkipButton"); Rect(skip, "skip"); Round(skip, N("pillRadius"));
            Border(skip, 0);
            skip.style.minHeight = 0; skip.style.fontSize = (float)N("smallFont");
            skip.clicked += Finish;
            var card = screen.Q("ScoreCard"); Rect(card, "scoreCard"); Border(card, N("borderWidth")); Round(card, N("scoreCardRadius"));
            Rect(screen.Q("ScoreCaption"), "scoreCaption"); screen.Q("ScoreCaption").style.fontSize = (float)N("smallFont");
            Rect(levelLabel, "levelLabel"); levelLabel.style.fontSize = (float)N("smallFont");
            Rect(scoreLabel, "scoreLabel"); scoreLabel.style.fontSize = (float)N("scoreFont");
            var gauge = screen.Q("Gauge"); Rect(gauge, "gauge"); Round(gauge, N("gaugeRadius"));
            gaugeFill = screen.Q("GaugeFill"); Round(gaugeFill, N("gaugeRadius"));
            // Reuse the supplied vertical light texture, cropping one rainbow and rotating it into the gauge.
            var gaugeRect = A("gauge");
            var gradient = new UnityEngine.UIElements.Image { image = Texture("fx_rainbow_scroll"), scaleMode = ScaleMode.StretchToFill,
                uv = new UnityEngine.Rect(0, (float)(1 - N("gaugeTextureHalf")), 1, (float)N("gaugeTextureHalf")), pickingMode = PickingMode.Ignore };
            gradient.style.position = Position.Absolute;
            Rect(gradient, (gaugeRect[2] - gaugeRect[3]) / 2, (gaugeRect[3] - gaugeRect[2]) / 2, gaugeRect[3], gaugeRect[2]);
            Rotate(gradient, -N("degreesPerTurn") / 4); gaugeFill.Add(gradient);
        }

        private void SetupTitles()
        {
            Rect(banner, "banner"); Rotate(banner, N("bannerRotation"));
            var heading = screen.Q<Label>("BannerHeading"); Rect(heading, "bannerHeading");
            heading.style.fontSize = (float)N("bannerHeadingFont"); Outline(heading, false);
            Rect(bannerText, "bannerText"); bannerText.style.fontSize = (float)N("bannerTextFont"); Outline(bannerText, false);
            bannerGlow = AddGlowBefore(bannerText, "bannerText", N("bannerTextFont"));
            Rect(finale, "finale"); finale.EnableInClassList("effect-finale--fail", failed);
            Rect(finaleHeading, failed ? "failHeading" : "finaleHeading");
            Rect(finaleOne, failed ? "failLineOne" : "finaleLineOne");
            Rect(finaleTwo, failed ? "failLineTwo" : "finaleLineTwo");
            finaleHeading.style.fontSize = (float)N(failed ? "failHeadingFont" : "finaleHeadingFont");
            finaleOne.style.fontSize = finaleTwo.style.fontSize = (float)N(failed ? "failFont" : "finaleFont");
            // The mock's two-line h1 uses browser default bold; keep the Dela face and spec size.
            finaleOne.style.unityFontStyleAndWeight = finaleTwo.style.unityFontStyleAndWeight = FontStyle.Bold;
            finaleHeading.text = failed ? "あれ…?" : "大成功!!";
            finaleTwo.text = failed ? "未完成…" : "完成!!";
            Outline(finaleHeading, failed); Outline(finaleOne, failed); Outline(finaleTwo, failed);
            // Browser faux-bold expands the fill differently from TextCore. Keep the font/coordinates
            // and outer outline intact, adding only the configurable same-color inner stroke.
            SetupFinaleFill("FinaleFillOne", finaleOne.text, failed ? "failLineOne" : "finaleLineOne");
            SetupFinaleFill("FinaleFillTwo", finaleTwo.text, failed ? "failLineTwo" : "finaleLineTwo");
            if (!failed)
            {
                AddGlowBefore(finaleOne, "finaleLineOne", N("finaleFont"));
                AddGlowBefore(finaleTwo, "finaleLineTwo", N("finaleFont"));
            }
            Rect(missingLabel, "missing"); Round(missingLabel, N("missingRadius"));
            missingLabel.style.fontSize = (float)N("missingFont");
            missingLabel.text = "必須素材「" + missing + "」が足りなかった";
            missingLabel.style.display = failed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetupFinaleFill(string name, string text, string rectangle)
        {
            Label fill = screen.Q<Label>(name); Rect(fill, rectangle); fill.text = text;
            fill.style.fontSize = (float)N(failed ? "failFont" : "finaleFont");
            // Dela is already a heavy display face; TextCore's synthetic Bold closes its counters.
            fill.style.unityFontStyleAndWeight = FontStyle.Normal;
            fill.style.unityTextOutlineWidth = (float)N("finaleFillStrokePx");
        }

        private void SetupActions()
        {
            Rect(actions, "actions"); Rect(resultButton, "result"); Round(resultButton, N("resultRadius"));
            resultButton.style.fontSize = (float)N("resultFont");
            resultButton.EnableInClassList("effect-result--fail", failed); resultButton.clicked += Finish;
            var shadow = screen.Q("ResultShadow"); Rect(shadow, "resultShadow"); Round(shadow, N("resultRadius"));
            shadow.EnableInClassList("effect-result-shadow--fail", failed);
            var replay = screen.Q<Button>("ReplayButton"); Rect(replay, "replay"); Round(replay, N("replayRadius"));
            Border(replay, N("borderWidth")); replay.style.fontSize = (float)N("replayFont"); replay.clicked += Replay;
        }

        private void SetupPools()
        {
            lines.Clear(); sparks.Clear();
            ingredientElements.Clear(); burstElements.Clear(); ringElements.Clear();
            var capacity = ImoniEffectPoolCapacity.From(Config);
            for (var i = 0; i < capacity.Ingredients; i++)
            {
                var item = new FallingItem(); ingredientsLayer.Add(item); ingredientElements.Add(item);
                Rect(item, 0, 0, N("ingredientTextureSize"), N("ingredientTextureSize")); Visible(item, false);
            }
            var starTextures = StarNames.Select(name => Texture("fx_star_" + name)).ToArray();
            for (var i = 0; i < capacity.Bursts; i++)
            {
                var burst = new StarBurst(); burst.Prepare(capacity.StarsPerBurst, starTextures, grayscale.Background);
                starsLayer.Add(burst); burstElements.Add(burst); Visible(burst, false);
            }
            for (var i = 0; i < capacity.Rings; i++)
            {
                var ring = new GlowRing(); Image(ring, "fx_ring_glow");
                ringsLayer.Add(ring); ringElements.Add(ring); Visible(ring, false);
            }
            var rng = new ImoniEffectRandom((int)N("visualSeed"));
            var colors = layout["palette"].Values<string>().Select(ParseColor).ToArray();
            var streakLayer = screen.Q("Streaks");
            for (var i = 0; i < Config.StreakCounts.Max(); i++)
            {
                var element = new StreakLine(); streakLayer.Add(element);
                var x = Math.Round(Range(rng, "streakX")); var width = Range(rng, "streakWidth");
                var height = Math.Round(Range(rng, "streakHeight"));
                var color = rng.Next() < N("streakWhiteChance") ? colors[0] : colors[1 + (int)(rng.Next() * (colors.Length - 1))];
                element.style.backgroundColor = color; element.style.opacity = (float)Range(rng, "streakOpacity");
                Round(element, N("streakRadius")); Rect(element, x, 0, width, height);
                lines.Add(new Line { Element = element, Color = color, Base = Range(rng, "streakSeconds"), Delay = Range(rng, "streakDelay") });
            }
            var sparkleLayer = screen.Q("Sparkles");
            for (var i = 0; i < Config.SparkleCounts.Max(); i++)
            {
                var element = new Sparkle(); Image(element, "fx_sparkle"); sparkleLayer.Add(element);
                var side = rng.Next() < .5 ? -1 : 1;
                var x = Math.Round(Config.V("centerX") + side * Range(rng, "sparkleDistance") + N("sparkleOffsetX"));
                var y = Math.Round(Range(rng, "sparkleY")); var size = Math.Round(Range(rng, "sparkleSize"));
                var tint = colors[(int)(rng.Next() * colors.Length)];
                element.style.unityBackgroundImageTintColor = tint;
                Rect(element, x, y, size, size);
                sparks.Add(new Spark { Element = element, Y = y, Tint = tint, Duration = Range(rng, "sparkleSeconds"),
                    FloatDuration = Range(rng, "sparkleFloatSeconds"), Delay = Range(rng, "sparkleDelay") });
            }
        }

        private void Render(ImoniEffectFrame frame)
        {
            var t = Timeline.NowMs; var sec = t / N("millisecondsPerSecond"); var level = Timeline.Level;
            bool grayChanged = grayscale.SetAmount((float)frame.Gray);
            // CSS may resolve one panel update after Build. Capture the authored color once it is available.
            if (!worldBackgroundCaptured && screen.resolvedStyle.backgroundColor.a > 0)
            {
                worldBackground = screen.resolvedStyle.backgroundColor; worldBackgroundCaptured = true; grayChanged = true;
            }
            if (grayChanged)
            {
                if (worldBackgroundCaptured) screen.style.backgroundColor = grayscale.Color(worldBackground);
                foreach (var entry in imageSources)
                    if (world.Contains(entry.Key)) entry.Key.style.backgroundImage = grayscale.Background(entry.Value);
            }
            Move(world, frame.ShakeX, frame.ShakeY);
            // Absolute elapsed-time phases reproduce CSS animation duration changes without frame drift.
            Move(rainbow, 0, -Fraction(sec / frame.RainbowDurationSec) * N("rainbowPeriodY"));
            Rotate(sunburst, sec / frame.SpinDurationSec * N("degreesPerTurn")); sunburst.style.opacity = (float)frame.BurstOpacity;
            var pw = frame.PillarWidth / N("pillarCoreFraction");
            Rect(pillar, Config.V("centerX") - pw / 2, N("pillarTop"), pw, N("pillarHeight"));
            var gw = frame.PillarWidth * N("rainbowGlowWidthMultiplier") / N("rainbowGlowContentFraction");
            Rect(glow, Config.V("centerX") - gw / 2, N("pillarTop"), gw, N("rainbowGlowHeight"));
            glow.style.opacity = (float)frame.GlowOpacity;
            Scale(pot, frame.PotScale); broth.style.opacity = (float)frame.BrothGlow;
            flash.style.opacity = (float)frame.Flash;
            gray.style.opacity = (float)(frame.Gray * N("grayOpacity")); dark.style.opacity = (float)(frame.Gray * N("darkOpacity"));
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i]; var visible = i < Config.StreakCounts[level];
                line.Element.style.backgroundColor = grayscale.Color(line.Color);
                Visible(line.Element, visible);
                if (visible) Move(line.Element, 0, N("streakTop") + N("streakDistance") * Fraction((sec + line.Delay) / (line.Base / (1 + N("streakSpeedPerLevel") * level))));
            }
            for (var i = 0; i < sparks.Count; i++)
            {
                var spark = sparks[i]; var visible = i < Config.SparkleCounts[level];
                spark.Element.style.unityBackgroundImageTintColor = grayscale.Color(spark.Tint);
                Visible(spark.Element, visible); if (!visible) continue;
                var twinkle = (1 - Math.Cos(Fraction((sec + spark.Delay) / spark.Duration) * Math.PI * 2)) / 2;
                Scale(spark.Element, N("sparkleMinimumScale") + (1 - N("sparkleMinimumScale")) * twinkle);
                spark.Element.style.opacity = (float)twinkle; Rotate(spark.Element, N("sparkleRotation") * twinkle);
                Move(spark.Element, 0, N("sparkleFloatY") * (1 - Math.Cos(sec / spark.FloatDuration * Math.PI)) / 2);
            }
            RenderIngredients(t); RenderBursts(t, level);
            scoreLabel.text = ScoreText(Timeline.DisplayScore);
            scoreLabel.style.fontSize = (float)N(scoreLabel.text.Length > N("scoreCompactAfterDigits") ? "scoreLargeFont" : "scoreFont");
            levelLabel.text = "Lv." + level + " " + Config.LevelNames[level];
            gaugeFill.style.width = Length.Percent((float)frame.Gauge);
            Visible(banner, frame.ShowBanner); Scale(banner, frame.BannerScale); banner.style.opacity = (float)frame.BannerOpacity;
            bannerText.text = Config.LevelNames[level];
            bannerGlow.text = bannerText.text;
            Visible(finale, frame.Finale || frame.FailFinale);
            Scale(finale, frame.FailFinale ? 1 : frame.FinaleScale); Move(finale, 0, frame.FailFinale ? frame.FailDrop : 0);
            Visible(actions, frame.ResultVisible);
        }

        private void RenderIngredients(double t)
        {
            var index = 0;
            foreach (var item in Timeline.Items)
            {
                var element = ingredientElements[index++]; Visible(element, true);
                Image(element, "ingredient_" + IngredientNames[item.Kind] + "_glow");
                var pose = ImoniEffectMath.Ingredient(item, t, Config);
                Move(element, pose.X - N("ingredientTextureOffset"), pose.Y - N("ingredientTextureOffset"));
                Rotate(element, pose.Rotation); Scale(element, pose.Scale);
            }
            for (; index < ingredientElements.Count; index++) Visible(ingredientElements[index], false);
        }

        private void RenderBursts(double t, int level)
        {
            var burstIndex = 0; var ringIndex = 0;
            foreach (var burst in Timeline.Bursts)
            {
                if (burst.Big)
                {
                    var ring = ringElements[ringIndex++]; Visible(ring, true);
                    var diameter = ImoniEffectMath.RingDiameter(burst, t, Config) * N("ringTextureScale");
                    Rect(ring, burst.X - diameter / 2, Config.V("potY") - diameter / 2, diameter, diameter);
                    ring.style.opacity = (float)(1 - (t - burst.AtMs) / Config.V("bigBurstMs"));
                }
                var count = burst.Big ? (int)Config.V("bigStarCount") : ImoniEffectMath.SmallStarCount(level, Config);
                var group = burstElements[burstIndex++]; Visible(group, true);
                for (var i = 0; i < count; i++)
                {
                    var pose = ImoniEffectMath.Star(burst, i, level, t, Config);
                    group.SetStar(i, pose, N("starTextureScale"));
                }
                group.HideFrom(count);
            }
            for (var i = burstIndex; i < burstElements.Count; i++) Visible(burstElements[i], false);
            for (var i = ringIndex; i < ringElements.Count; i++) Visible(ringElements[i], false);
        }

        private void Finish() { Hide(); onResult?.Invoke(); }
        private void UpdateSound()
        {
            soundLabel.text = SoundEnabled ? "音あり" : "音なし";
            Image(soundIcon, SoundEnabled ? "icon_sound_on" : "icon_sound_off");
        }
        private void Outline(VisualElement element, bool isFail)
        {
            element.style.unityTextOutlineWidth = (float)N("outlineWidth");
            var color = ParseColor(isFail ? "#4A2E22" : "#8B2310");
            element.style.textShadow = new TextShadow { color = color, offset = new Vector2((float)N("shadowOffset"), (float)N("shadowOffset")), blurRadius = 0 };
        }
        private Label AddGlowBefore(Label foreground, string rectangle, double fontSize)
        {
            var label = new Label(foreground.text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("effect-outline"); Rect(label, rectangle);
            label.style.fontSize = (float)fontSize;
            label.style.color = new Color(1, 1, 1, 0);
            label.style.unityTextOutlineWidth = 0;
            label.style.textShadow = new TextShadow { color = new Color(1, 1, 1, (float)N("whiteGlowAlpha")),
                offset = Vector2.zero, blurRadius = (float)N("whiteGlowWidth") };
            foreground.parent.Insert(foreground.parent.IndexOf(foreground), label);
            return label;
        }
        private string ScoreText(BigInteger value)
        {
            return Format.Big(value, displayConfig);
        }
        private static bool HasStyleInAncestors(VisualElement element, StyleSheet sheet)
        {
            for (var current = element; current != null; current = current.hierarchy.parent)
                if (current.styleSheets.Contains(sheet)) return true;
            return false;
        }
        private Texture2D Texture(string name) => artwork.TryGetValue(name, out var value) ? value : throw new InvalidOperationException("Missing effect image: " + name);
        private void Image(VisualElement element, string name)
        {
            Texture2D texture = Texture(name); imageSources[element] = texture;
            element.style.backgroundImage = world != null && world.Contains(element)
                ? grayscale.Background(texture) : new StyleBackground(texture);
        }
        private double N(string key) => layout[key]?.Value<double>() ?? throw new InvalidOperationException("Missing effect layout number: " + key);
        private double[] A(string key) => layout[key]?.Values<double>().ToArray() ?? throw new InvalidOperationException("Missing effect layout array: " + key);
        private double Range(ImoniEffectRandom random, string key) { var range = A(key); return random.Range(range[0], range[1]); }
        private void Rect(VisualElement element, string key) { var r = A(key); Rect(element, r[0], r[1], r[2], r[3]); }
        private static void Rect(VisualElement element, double x, double y, double width, double height)
        {
            element.style.position = Position.Absolute; element.style.left = (float)x; element.style.top = (float)y;
            element.style.width = (float)width; element.style.height = (float)height;
        }
        private static void Move(VisualElement element, double x, double y) => element.style.translate = new Translate((float)x, (float)y);
        private static void Rotate(VisualElement element, double angle) => element.style.rotate = new Rotate(new Angle((float)angle));
        private static void Scale(VisualElement element, double scale) => element.style.scale = new Scale(new Vector3((float)scale, (float)scale, 1));
        private static void Visible(VisualElement element, bool show) => element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        private static void Round(VisualElement element, double radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = (float)radius;
        }
        private static void Border(VisualElement element, double width)
        {
            element.style.borderTopWidth = element.style.borderRightWidth = element.style.borderBottomWidth = element.style.borderLeftWidth = (float)width;
        }
        private static double Fraction(double value) => value - Math.Floor(value);
        private static Color ParseColor(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
    }
}
