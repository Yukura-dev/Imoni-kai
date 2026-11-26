using System;
using System.Linq;
using Imoni.Core.Effects;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    /// <summary>Shared image-only presentation primitives. Their instances are warmed once and reused.</summary>
    public abstract class ImoniEffectArt : VisualElement
    {
        protected ImoniEffectArt()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("effect-art");
            style.position = Position.Absolute;
        }
    }

    [UxmlElement] public partial class FallingItem : ImoniEffectArt { }
    [UxmlElement] public partial class GlowRing : ImoniEffectArt { }
    [UxmlElement] public partial class StreakLine : ImoniEffectArt { }
    [UxmlElement] public partial class Sparkle : ImoniEffectArt { }

    [UxmlElement]
    public partial class ImoniEffectSound : ImoniButton
    {
        public ImoniEffectSound()
        {
            Variant = ImoniButtonVariant.Outline;
            AddToClassList("effect-sound");
            var icon = new VisualElement { name = "SoundIcon", pickingMode = PickingMode.Ignore };
            icon.AddToClassList("sound-icon"); Add(icon);
            Add(new Label("音あり") { name = "SoundLabel", pickingMode = PickingMode.Ignore });
        }
    }

    [UxmlElement]
    public partial class ImoniEffectScore : Card
    {
        public ImoniEffectScore()
        {
            AddToClassList("effect-score-card"); pickingMode = PickingMode.Ignore;
            // Match the direct child coordinate origin of the spec; the general Card body
            // may otherwise size itself from its flowing children (all effect children are absolute).
            contentContainer.style.position = Position.Absolute;
            contentContainer.style.left = contentContainer.style.top = 0;
            contentContainer.style.right = contentContainer.style.bottom = 0;
            Add(Text("ScoreCaption", "芋煮スコア", "effect-caption"));
            Add(Text("LevelLabel", string.Empty, "effect-level"));
            Label score = Text("ScoreLabel", "0", "effect-score"); score.AddToClassList("num-big"); Add(score);
            var gauge = new VisualElement { name = "Gauge", pickingMode = PickingMode.Ignore };
            gauge.AddToClassList("effect-gauge");
            var fill = new VisualElement { name = "GaugeFill", pickingMode = PickingMode.Ignore };
            fill.AddToClassList("effect-gauge-fill"); gauge.Add(fill); Add(gauge);
        }

        private static Label Text(string name, string text, string className)
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            label.AddToClassList(className); return label;
        }
    }

    /// <summary>Reuses ResultBanner's text/variant structure; its layout and animation clock are
    /// supplied by the screen-coordinate effect renderer instead of the gallery's flow layout.</summary>
    public abstract class ImoniEffectTitle : ResultBanner
    {
        protected void UseEffectLayout(bool levelUp)
        {
            Variant = levelUp ? ResultBannerVariant.LevelUp : ResultBannerVariant.Success;
            ReplaceClasses(this, "effect-art", levelUp ? "effect-banner" : "effect-finale");
            Rename(this.Q<Label>(className: "result-banner__lead"), levelUp ? "BannerHeading" : "FinaleHeading", "effect-white");
            Label[] lines = this.Query<Label>(className: "result-banner__title").ToList().ToArray();
            Rename(lines[0], levelUp ? "BannerText" : "FinaleLineOne", "effect-yellow");
            Rename(lines[1], levelUp ? "UnusedBannerLine" : "FinaleLineTwo", "effect-yellow");
            Label note = this.Q<Label>(className: "result-banner__note");
            note.name = levelUp ? "UnusedBannerNote" : "MissingLabel";
            ReplaceClasses(note, "pill", "pill--dark", "effect-missing");
            pickingMode = PickingMode.Ignore;
        }
        private static void Rename(Label label, string name, string color)
        { label.name = name; ReplaceClasses(label, "effect-outline", color); }
        private static void ReplaceClasses(VisualElement element, params string[] classes)
        {
            foreach (string name in element.GetClasses().ToArray()) element.RemoveFromClassList(name);
            foreach (string name in classes) element.AddToClassList(name);
        }
    }
    [UxmlElement]
    public partial class ImoniEffectBanner : ImoniEffectTitle
    { public ImoniEffectBanner() { UseEffectLayout(true); } }
    [UxmlElement]
    public partial class ImoniEffectFinale : ImoniEffectTitle
    { public ImoniEffectFinale() { UseEffectLayout(false); } }

    [UxmlElement]
    public partial class StarBurst : ImoniEffectArt
    {
        private VisualElement[] stars = Array.Empty<VisualElement>();
        private Texture2D[] colors = Array.Empty<Texture2D>();
        public int Capacity => stars.Length;

        public void Prepare(int capacity, Texture2D[] textures)
        {
            colors = textures ?? throw new ArgumentNullException(nameof(textures));
            if (stars.Length == capacity) return;
            Clear(); stars = new VisualElement[capacity];
            for (int i = 0; i < stars.Length; i++)
            {
                var star = new VisualElement { pickingMode = PickingMode.Ignore };
                star.AddToClassList("effect-art"); star.style.display = DisplayStyle.None;
                Add(star); stars[i] = star;
            }
        }

        public void SetStar(int index, ImoniStarPose pose, double textureScale)
        {
            VisualElement star = stars[index];
            float size = (float)(pose.Size * textureScale);
            star.style.display = DisplayStyle.Flex;
            star.style.backgroundImage = colors[pose.ColorIndex];
            star.style.left = star.style.top = 0;
            star.style.width = star.style.height = size;
            star.style.translate = new Translate((float)pose.CenterX - size / 2, (float)pose.CenterY - size / 2);
            star.style.rotate = new Rotate(new Angle((float)pose.Rotation));
            star.style.opacity = (float)pose.Opacity;
        }

        public void HideFrom(int index)
        {
            for (; index < stars.Length; index++) stars[index].style.display = DisplayStyle.None;
        }
    }
}
