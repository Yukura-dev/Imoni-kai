using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    public enum PotVariant { Campfire, Small, Large }
    public enum PatternVariant { Yellow, Transparent }
    public enum ResultBannerVariant { LevelUp, Success, Fail }

    [UxmlElement]
    public partial class AnimatedPot : VisualElement
    {
        private readonly VisualElement body;
        private readonly VisualElement[] steam = new VisualElement[3];
        private IVisualElementScheduledItem animation;
        private PotVariant variant;
        private bool steamEnabled = true;
        private bool sway = true;
        [UxmlAttribute] public PotVariant Variant { get => variant; set { variant = value; Refresh(); } }
        [UxmlAttribute] public bool Steam { get => steamEnabled; set { steamEnabled = value; Refresh(); } }
        [UxmlAttribute] public bool Sway { get => sway; set { sway = value; if (!value) ResetPose(); } }
        public AnimatedPot()
        {
            AddToClassList("animated-pot");
            UiTheme.Apply(this, "UI/Components/Effects");
            body = new VisualElement { pickingMode = PickingMode.Ignore }; body.AddToClassList("animated-pot__body"); Add(body);
            for (int index = 0; index < steam.Length; index++)
            {
                steam[index] = new VisualElement { pickingMode = PickingMode.Ignore };
                steam[index].AddToClassList("animated-pot__steam"); steam[index].AddToClassList("animated-pot__steam--" + index); Add(steam[index]);
            }
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Refresh();
                animation = schedule.Execute(Animate).Every(Math.Max(1, (long)UiTheme.Number("animationFrameMs")));
            });
            RegisterCallback<DetachFromPanelEvent>(_ => animation?.Pause());
            RegisterCallback<GeometryChangedEvent>(_ => LayoutArtwork());
            Refresh();
        }
        private void Refresh()
        {
            if (body == null) return;
            UiTheme.SetVariant(this, "animated-pot--", variant);
            body.style.backgroundImage = UiTheme.Texture(variant == PotVariant.Campfire ? "home_pot_campfire" : "home_pot_plain");
            for (int index = 0; index < steam.Length; index++)
            {
                steam[index].style.backgroundImage = UiTheme.Texture("home_pot_steam_" + index);
                steam[index].style.display = steamEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            }
            LayoutArtwork();
        }
        private void LayoutArtwork()
        {
            if (contentRect.width <= 0 || contentRect.height <= 0) return;
            var art = UiTheme.Data?["setup"]?["potArtwork"];
            if (art == null) return;
            var crop = art[variant == PotVariant.Campfire ? "campfireCrop" : "plainCrop"];
            float sourceLeft = (float)crop[0], sourceTop = (float)art["steamTop"];
            float sourceWidth = (float)crop[2], sourceHeight = (float)crop[1] + (float)crop[3] - sourceTop;
            float scale = Mathf.Min(contentRect.width / sourceWidth, contentRect.height / sourceHeight);
            float left = (contentRect.width - sourceWidth * scale) / 2;
            float top = (contentRect.height - sourceHeight * scale) / 2;
            PlaceCrop(body, crop, sourceLeft, sourceTop, scale, left, top);
            for (int index = 0; index < steam.Length; index++)
                PlaceCrop(steam[index], art["steamCrops"][index], sourceLeft, sourceTop, scale, left, top);
        }
        private static void PlaceCrop(VisualElement target, JToken crop, float sourceLeft, float sourceTop,
            float scale, float left, float top)
        {
            target.style.left = left + ((float)crop[0] - sourceLeft) * scale;
            target.style.top = top + ((float)crop[1] - sourceTop) * scale;
            target.style.width = (float)crop[2] * scale;
            target.style.height = (float)crop[3] * scale;
        }
        private void ResetPose()
        { style.translate = new Translate(0, 0); style.rotate = new Rotate(0); }
        private void Animate()
        {
            if (UiTheme.Data == null) return;
            bool large = variant == PotVariant.Large;
            string size = large ? "large" : "small";
            double time = Time.realtimeSinceStartupAsDouble;
            if (sway && variant != PotVariant.Campfire)
            {
                double wave = (1 - Math.Cos(time / UiTheme.Number(size + "PotBobSeconds") * Math.PI * 2)) / 2;
                style.translate = new Translate(0, (float)(-wave * UiTheme.Number(size + "PotBobPx")));
                style.rotate = new Rotate((float)(large ? (wave * 2 - 1) * UiTheme.Number("largePotSwayDegrees") : 0));
            }
            else ResetPose();
            if (steamEnabled)
            {
                for (int index = 0; index < steam.Length; index++)
                {
                    double progress = ((time - index * UiTheme.Number(size + "SteamPhaseDelaySeconds")) / UiTheme.Number(size + "SteamSeconds") % 1 + 1) % 1;
                    steam[index].style.translate = new Translate(0, (float)(UiTheme.Number(size + "SteamStartPx") - progress * UiTheme.Number(size + "SteamTravelPx")));
                    steam[index].style.opacity = (float)Math.Sin(progress * Math.PI);
                }
            }
        }
    }

    [UxmlElement]
    public partial class PatternBackground : VisualElement
    {
        private PatternVariant variant;
        [UxmlAttribute] public PatternVariant Variant { get => variant; set { variant = value; Refresh(); } }
        public PatternBackground()
        {
            AddToClassList("pattern-background"); UiTheme.Apply(this, "UI/Components/Effects");
            RegisterCallback<AttachToPanelEvent>(_ => Refresh()); Refresh();
        }
        private void Refresh()
        {
            UiTheme.SetVariant(this, "pattern-background--", variant);
            style.backgroundImage = UiTheme.Texture(variant == PatternVariant.Yellow ? "imoni_tile_yellow_384x416" : "imoni_tile_transparent_384x416");
        }
    }

    [UxmlElement]
    public partial class ResultBanner : VisualElement
    {
        private readonly Label lead;
        private readonly Label title;
        private readonly Label secondLine;
        private readonly Label note;
        private ResultBannerVariant variant;
        private string subtitle = "伝説の芋煮!!";
        private string missingMaterial = "魚";
        private IVisualElementScheduledItem animation;
        [UxmlAttribute] public ResultBannerVariant Variant { get => variant; set { variant = value; Refresh(); } }
        [UxmlAttribute] public string Subtitle { get => subtitle; set { subtitle = value; Refresh(); } }
        [UxmlAttribute] public string MissingMaterial { get => missingMaterial; set { missingMaterial = value; Refresh(); } }
        public ResultBanner()
        {
            AddToClassList("result-banner"); UiTheme.Apply(this, "UI/Components/Effects");
            lead = UiTheme.Label(string.Empty, "result-banner__lead");
            title = UiTheme.Label(string.Empty, "result-banner__title");
            secondLine = UiTheme.Label(string.Empty, "result-banner__title");
            note = UiTheme.Label(string.Empty, "result-banner__note");
            Add(lead); Add(title); Add(secondLine); Add(note);
            RegisterCallback<DetachFromPanelEvent>(_ => animation?.Pause()); Refresh();
        }
        private void Refresh()
        {
            if (title == null) return;
            UiTheme.SetVariant(this, "result-banner--", variant);
            lead.text = variant == ResultBannerVariant.Success ? "大成功!!" : variant == ResultBannerVariant.Fail ? "あれ…?" : "LEVEL UP!";
            title.text = variant == ResultBannerVariant.LevelUp ? subtitle : "芋煮";
            secondLine.text = variant == ResultBannerVariant.Success ? "完成!!" : "未完成…";
            secondLine.style.display = variant == ResultBannerVariant.LevelUp ? DisplayStyle.None : DisplayStyle.Flex;
            note.text = "必須素材「" + missingMaterial + "」が足りなかった";
            note.style.display = variant == ResultBannerVariant.Fail ? DisplayStyle.Flex : DisplayStyle.None;
        }
        public void Play()
        {
            animation?.Pause();
            double start = Time.realtimeSinceStartupAsDouble;
            double duration = UiTheme.Number(variant == ResultBannerVariant.Fail ? "resultDropSeconds" : "resultPopSeconds");
            if (duration <= 0) return;
            animation = schedule.Execute(() =>
            {
                float progress = Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble - start) / duration));
                if (variant == ResultBannerVariant.Fail)
                    style.translate = new Translate(0, (float)(-UiTheme.Data["imoniEffect"]["visual"]["failDropY"].Value<double>()) * (1 - progress) * (1 - progress));
                else
                {
                    // A presentation-only overshoot; the effect's game score/timing remains in Core.
                    float rise = (float)UiTheme.Number("resultPopRiseFraction");
                    float peak = (float)UiTheme.Number("resultPopPeak");
                    float scale = progress < rise ? progress * peak / rise : Mathf.Lerp(peak, 1, (progress - rise) / (1 - rise));
                    style.scale = new Scale(new Vector3(scale, scale, 1));
                }
                if (progress >= 1) animation.Pause();
            }).Every(Math.Max(1, (long)UiTheme.Number("animationFrameMs")));
        }
    }
}
