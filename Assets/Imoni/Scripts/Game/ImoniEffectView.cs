using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Imoni.Core.Effects;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using BigInteger = System.Numerics.BigInteger;

namespace Imoni.Game.Effects
{
    /// <summary>Presentation only. The round is already settled before this view begins.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ImoniEffectView : MonoBehaviour
    {
        public UIDocument document;
        public TextAsset gameConfig;
        public VisualTreeAsset visualTree;
        public StyleSheet themeStyle;
        public StyleSheet effectStyle;
        public Texture2D[] textures;
        public bool ManualTime { get; set; }
        public ImoniEffectTimeline Timeline { get; private set; }
        public ImoniEffectConfig Config { get; private set; }
        public bool SoundEnabled { get; private set; }
        public event Action<ImoniEffectEvent> EffectEvent;
        public event Action<bool> SoundChanged;
        public event Action Replayed;
        public event Action Hidden;

        private static readonly string[] IngredientNames = { "taro", "negi", "mushroom", "beef", "konnyaku" };
        private static readonly string[] StarNames = { "yellow", "pink", "blue", "green", "purple", "orange" };
        private readonly Dictionary<string, Texture2D> artwork = new Dictionary<string, Texture2D>();
        private readonly Dictionary<int, VisualElement> ingredients = new Dictionary<int, VisualElement>();
        private readonly List<VisualElement> starElements = new List<VisualElement>();
        private readonly List<VisualElement> ringElements = new List<VisualElement>();
        private readonly List<Line> lines = new List<Line>();
        private readonly List<Spark> sparks = new List<Spark>();
        private JObject layout;
        private VisualElement screen, world, rainbow, sunburst, pillar, glow, pot, broth, flash, gray, dark;
        private VisualElement ingredientsLayer, starsLayer, ringsLayer, banner, finale, actions, gaugeFill;
        private Label scoreLabel, levelLabel, bannerText, soundLabel, finaleHeading, finaleOne, finaleTwo, missingLabel;
        private Label bannerGlow;
        private VisualElement soundIcon;
        private Button resultButton;
        private Action onResult;
        private BigInteger targetScore, referenceScore;
        private bool failed, active;
        private string missing;
        private int seed, emittedEvents;
        private double startedAt;

        private sealed class Line
        {
            public VisualElement Element;
            public double Base, Delay;
        }
        private sealed class Spark
        {
            public VisualElement Element;
            public double Y, Duration, FloatDuration, Delay;
        }

        public void Begin(BigInteger finalScore, BigInteger successReferenceScore, bool isFailed,
            string missingMaterials, Action result, int effectSeed = 7)
        {
            targetScore = finalScore; referenceScore = successReferenceScore; failed = isFailed;
            missing = missingMaterials ?? string.Empty; onResult = result; seed = effectSeed;
            if (!document) document = GetComponent<UIDocument>();
            if (!gameConfig || !visualTree || !themeStyle || !effectStyle)
                throw new InvalidOperationException("Run Imoni > Setup Project to bind the effect resources.");
            Config = ImoniEffectConfig.FromJson(gameConfig.text);
            layout = (JObject)Config.Data["layout"] ?? throw new InvalidOperationException("imoniEffect.layout is missing.");
            SoundEnabled = PlayerPrefs.GetInt("Imoni.SoundEnabled", 1) != 0;
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
            artwork.Clear();
            foreach (var texture in textures ?? Array.Empty<Texture2D>()) if (texture) artwork[texture.name] = texture;
            var root = document.rootVisualElement;
            root.Clear();
            if (!root.styleSheets.Contains(themeStyle)) root.styleSheets.Add(themeStyle);
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
            ingredients.Clear(); starElements.Clear(); ringElements.Clear();
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
                PlayerPrefs.SetInt("Imoni.SoundEnabled", SoundEnabled ? 1 : 0); PlayerPrefs.Save();
                UpdateSound(); SoundChanged?.Invoke(SoundEnabled);
            };
            soundIcon.style.width = (float)N("soundIconSize"); soundIcon.style.height = (float)N("soundIconSize");
            soundIcon.style.marginRight = (float)N("soundIconGap");
            var skip = screen.Q<Button>("SkipButton"); Rect(skip, "skip"); Round(skip, N("pillRadius"));
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
            finaleHeading.text = failed ? "あれ…?" : "大成功!!";
            finaleTwo.text = failed ? "未完成…" : "完成!!";
            Outline(finaleHeading, failed); Outline(finaleOne, failed); Outline(finaleTwo, failed);
            if (!failed)
            {
                AddGlowBefore(finaleOne, "finaleLineOne", N("finaleFont"));
                AddGlowBefore(finaleTwo, "finaleLineTwo", N("finaleFont"));
            }
            Rect(missingLabel, "missing"); Round(missingLabel, N("missingRadius"));
            missingLabel.style.fontSize = (float)N("missingFont");
            missingLabel.text = "必須素材『" + missing + "』が足りなかった";
            missingLabel.style.display = failed ? DisplayStyle.Flex : DisplayStyle.None;
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
            var rng = new ImoniEffectRandom((int)N("visualSeed"));
            var colors = layout["palette"].Values<string>().Select(ParseColor).ToArray();
            var streakLayer = screen.Q("Streaks");
            for (var i = 0; i < Config.StreakCounts.Max(); i++)
            {
                var element = Art(streakLayer);
                var x = Math.Round(Range(rng, "streakX")); var width = Range(rng, "streakWidth");
                var height = Math.Round(Range(rng, "streakHeight"));
                var color = rng.Next() < N("streakWhiteChance") ? colors[0] : colors[1 + (int)(rng.Next() * (colors.Length - 1))];
                element.style.backgroundColor = color; element.style.opacity = (float)Range(rng, "streakOpacity");
                Round(element, N("streakRadius")); Rect(element, x, 0, width, height);
                lines.Add(new Line { Element = element, Base = Range(rng, "streakSeconds"), Delay = Range(rng, "streakDelay") });
            }
            var sparkleLayer = screen.Q("Sparkles");
            for (var i = 0; i < Config.SparkleCounts.Max(); i++)
            {
                var element = Art(sparkleLayer, "fx_sparkle");
                var side = rng.Next() < .5 ? -1 : 1;
                var x = Math.Round(Config.V("centerX") + side * Range(rng, "sparkleDistance") + N("sparkleOffsetX"));
                var y = Math.Round(Range(rng, "sparkleY")); var size = Math.Round(Range(rng, "sparkleSize"));
                element.style.unityBackgroundImageTintColor = colors[(int)(rng.Next() * colors.Length)];
                Rect(element, x, y, size, size);
                sparks.Add(new Spark { Element = element, Y = y, Duration = Range(rng, "sparkleSeconds"),
                    FloatDuration = Range(rng, "sparkleFloatSeconds"), Delay = Range(rng, "sparkleDelay") });
            }
        }

        private void Render(ImoniEffectFrame frame)
        {
            var t = Timeline.NowMs; var sec = t / N("millisecondsPerSecond"); var level = Timeline.Level;
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
                Visible(line.Element, visible);
                if (visible) Move(line.Element, 0, N("streakTop") + N("streakDistance") * Fraction((sec + line.Delay) / (line.Base / (1 + N("streakSpeedPerLevel") * level))));
            }
            for (var i = 0; i < sparks.Count; i++)
            {
                var spark = sparks[i]; var visible = i < Config.SparkleCounts[level];
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
            var alive = new HashSet<int>();
            foreach (var item in Timeline.Items)
            {
                alive.Add(item.Id);
                if (!ingredients.TryGetValue(item.Id, out var element))
                {
                    element = Art(ingredientsLayer, "ingredient_" + IngredientNames[item.Kind] + "_glow");
                    Rect(element, 0, 0, N("ingredientTextureSize"), N("ingredientTextureSize"));
                    ingredients.Add(item.Id, element);
                }
                var pose = ImoniEffectMath.Ingredient(item, t, Config);
                Move(element, pose.X - N("ingredientTextureOffset"), pose.Y - N("ingredientTextureOffset"));
                Rotate(element, pose.Rotation); Scale(element, pose.Scale);
            }
            foreach (var id in ingredients.Keys.Where(id => !alive.Contains(id)).ToArray())
            {
                ingredients[id].RemoveFromHierarchy(); ingredients.Remove(id);
            }
        }

        private void RenderBursts(double t, int level)
        {
            var starIndex = 0; var ringIndex = 0;
            foreach (var burst in Timeline.Bursts)
            {
                if (burst.Big)
                {
                    var ring = Pooled(ringElements, ringIndex++, ringsLayer, "fx_ring_glow");
                    var diameter = ImoniEffectMath.RingDiameter(burst, t, Config) * N("ringTextureScale");
                    Rect(ring, burst.X - diameter / 2, Config.V("potY") - diameter / 2, diameter, diameter);
                    ring.style.opacity = (float)(1 - (t - burst.AtMs) / Config.V("bigBurstMs"));
                }
                var count = burst.Big ? (int)Config.V("bigStarCount") : ImoniEffectMath.SmallStarCount(level, Config);
                for (var i = 0; i < count; i++)
                {
                    var pose = ImoniEffectMath.Star(burst, i, level, t, Config);
                    var star = Pooled(starElements, starIndex++, starsLayer, "fx_star_" + StarNames[pose.ColorIndex]);
                    var size = pose.Size * N("starTextureScale");
                    Rect(star, 0, 0, size, size);
                    Move(star, pose.CenterX - size / 2, pose.CenterY - size / 2); Rotate(star, pose.Rotation);
                    star.style.opacity = (float)pose.Opacity;
                }
            }
            for (var i = starIndex; i < starElements.Count; i++) Visible(starElements[i], false);
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
            if (value.ToString(CultureInfo.InvariantCulture).Length <= N("scoreCompactAfterDigits")) return value.ToString("N0", CultureInfo.GetCultureInfo("ja-JP"));
            var units = new[] { "", "万", "億", "兆" }; var divisor = new BigInteger(10000);
            var segments = new List<string>(); var unit = 0;
            while (value > 0 && unit < units.Length - 1)
            {
                var part = value % divisor; if (part > 0) segments.Insert(0, part.ToString("N0", CultureInfo.InvariantCulture) + units[unit]);
                value /= divisor; unit++;
            }
            if (value > 0) segments.Insert(0, value.ToString("N0", CultureInfo.InvariantCulture) + units[unit]);
            return string.Concat(segments.Take(2));
        }
        private VisualElement Pooled(List<VisualElement> pool, int index, VisualElement parent, string asset)
        {
            if (index == pool.Count) pool.Add(Art(parent, asset));
            var element = pool[index]; Image(element, asset); Visible(element, true); return element;
        }
        private VisualElement Art(VisualElement parent, string asset = null)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList("effect-art"); if (asset != null) Image(element, asset); parent.Add(element); return element;
        }
        private Texture2D Texture(string name) => artwork.TryGetValue(name, out var value) ? value : throw new InvalidOperationException("Missing effect image: " + name);
        private void Image(VisualElement element, string name) => element.style.backgroundImage = new StyleBackground(Texture(name));
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
