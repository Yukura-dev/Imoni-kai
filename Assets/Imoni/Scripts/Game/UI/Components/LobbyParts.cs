using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    internal static class LobbyMotion
    {
        internal static float Number(string key) => UiTheme.Data?["outGame"]?["lobby"]?[key]?.Value<float>() ?? 0;
        internal static void Pop(VisualElement element, double appeared)
        {
            float duration = Number("slotPopSeconds");
            float t = duration <= 0 ? 1 : Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble - appeared) / duration);
            float peakAt = Number("slotPopOvershootPoint");
            float scale = t >= 1 ? 1 : t < peakAt
                ? Mathf.Lerp(Number("slotPopInitialScale"), Number("slotPopOvershootScale"), t / peakAt)
                : Mathf.Lerp(Number("slotPopOvershootScale"), 1, (t - peakAt) / (1 - peakAt));
            element.style.scale = new Scale(new Vector3(scale, scale, 1)); element.style.opacity = t >= peakAt ? 1 : t / peakAt;
        }
    }
    /// <summary>One waiting-room slot, composed from the shared member row.</summary>
    [UxmlElement]
    public partial class WaitingMemberSlot : VisualElement
    {
        private readonly MemberRow member = new MemberRow();
        private readonly VisualElement empty = new VisualElement();
        private readonly VisualElement pulse = new VisualElement();
        private readonly Label caption = UiTheme.Label("参加を待っています.", "waiting-slot__caption");
        private IVisualElementScheduledItem animation;
        private bool filled;
        private double appeared;
        [UxmlAttribute] public bool Filled
        {
            get => filled;
            set
            {
                if (value && !filled) appeared = Time.realtimeSinceStartupAsDouble;
                filled = value;
                member.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                empty.style.display = value ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
        [UxmlAttribute] public string Dots { get => caption.text.Substring("参加を待っています".Length); set => caption.text = "参加を待っています" + value; }
        public WaitingMemberSlot()
        {
            AddToClassList("waiting-slot"); UiTheme.Apply(this, "UI/Components/LobbyParts");
            member.Q<Avatar>().Size = 50;
            empty.AddToClassList("waiting-slot__empty");
            var border = new LobbyDashedOutline(false); border.AddToClassList("waiting-slot__outline"); empty.Add(border);
            var icon = new VisualElement(); icon.AddToClassList("waiting-slot__icon");
            pulse.AddToClassList("waiting-slot__pulse"); icon.Add(pulse);
            var circle = new LobbyDashedOutline(true); circle.AddToClassList("waiting-slot__circle"); icon.Add(circle);
            empty.Add(icon); empty.Add(caption); Add(member); Add(empty); Filled = false;
            RegisterCallback<AttachToPanelEvent>(_ => { animation?.Pause(); animation = schedule.Execute(Animate).Every(Math.Max(1, (long)UiTheme.Number("animationFrameMs"))); });
            RegisterCallback<DetachFromPanelEvent>(_ => animation?.Pause());
        }
        public void Bind(MemberRowViewModel view) => member.Bind(view);
        private void Animate()
        {
            if (filled)
            {
                LobbyMotion.Pop(member, appeared);
            }
            else
            {
                float duration = LobbyMotion.Number("slotPulseSeconds");
                float phase = duration <= 0 ? 0 : (float)(Time.realtimeSinceStartupAsDouble % duration) / duration;
                float spread = phase * LobbyMotion.Number("slotPulseSpread");
                pulse.style.left = -spread; pulse.style.right = -spread; pulse.style.top = -spread; pulse.style.bottom = -spread;
                pulse.style.opacity = (1 - phase) * LobbyMotion.Number("slotPulseAlpha");
            }
        }
    }

    /// <summary>Avatar, name and shared status badge for loading synchronization.</summary>
    [UxmlElement]
    public partial class LoadingMember : VisualElement
    {
        private readonly Avatar avatar = new Avatar { Size = 50 };
        private readonly Label caption = UiTheme.Label(string.Empty, "loading-member__name");
        private readonly StatusBadge status = new StatusBadge();
        private bool ready;
        [UxmlAttribute] public string PlayerName { get => caption.text; set => caption.text = value ?? string.Empty; }
        [UxmlAttribute] public string BandColor { get => avatar.BandColor; set => avatar.BandColor = value; }
        [UxmlAttribute] public string OutfitColor { get => avatar.OutfitColor; set => avatar.OutfitColor = value; }
        [UxmlAttribute] public bool Ready
        {
            get => ready;
            set { ready = value; avatar.style.opacity = value ? 1 : .5f; status.State = value ? StatusBadgeState.Ready : StatusBadgeState.Waiting; status.text = value ? "OK" : "読込中"; }
        }
        public LoadingMember()
        { AddToClassList("loading-member"); UiTheme.Apply(this, "UI/Components/LobbyParts"); Add(avatar); Add(caption); Add(status); Ready = false; }
    }

    /// <summary>Small vector artwork replacing CSS dashed outlines (which USS does not support).</summary>
    internal sealed class LobbyDashedOutline : VisualElement
    {
        internal LobbyDashedOutline(bool circle, float cornerRadius = 14, float strokeWidth = 2, string color = "#8C6A2E", float dashLength = 4, float gapLength = 4)
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += context =>
            {
                var painter = context.painter2D;
                painter.strokeColor = string.IsNullOrEmpty(color) ? resolvedStyle.color : UiTheme.Color(color); painter.lineWidth = circle ? 3 : strokeWidth;
                float inset = painter.lineWidth / 2;
                Rect rect = new Rect(inset, inset, contentRect.width - inset * 2, contentRect.height - inset * 2);
                if (rect.width <= 0 || rect.height <= 0) return;
                var points = new List<Vector2>();
                if (circle)
                {
                    for (int segment = 0; segment <= 64; segment++)
                    {
                        float angle = segment * Mathf.PI * 2 / 64;
                        points.Add(rect.center + new Vector2(Mathf.Cos(angle) * rect.width / 2, Mathf.Sin(angle) * rect.height / 2));
                    }
                }
                else
                {
                    float radius = Mathf.Min(cornerRadius - inset, Mathf.Min(rect.width, rect.height) / 2);
                    Vector2[] centers = { new Vector2(rect.xMax - radius, rect.yMin + radius), new Vector2(rect.xMax - radius, rect.yMax - radius), new Vector2(rect.xMin + radius, rect.yMax - radius), new Vector2(rect.xMin + radius, rect.yMin + radius) };
                    for (int corner = 0; corner < centers.Length; corner++)
                        for (int segment = 0; segment <= 12; segment++)
                        {
                            float angle = (corner * 90 - 90 + segment * 90f / 12) * Mathf.Deg2Rad;
                            points.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                        }
                    points.Add(points[0]);
                }
                float phase = 0;
                for (int index = 1; index < points.Count; index++)
                {
                    Vector2 from = points[index - 1], to = points[index]; float length = Vector2.Distance(from, to), walked = 0;
                    while (walked < length)
                    {
                        float cycle = dashLength + gapLength;
                        float offset = phase % cycle;
                        bool on = offset < dashLength;
                        float amount = Math.Min((on ? dashLength : cycle) - offset, length - walked);
                        if (amount <= .0001f) amount = Math.Min(.0001f, length - walked);
                        if (on)
                        { painter.BeginPath(); painter.MoveTo(Vector2.Lerp(from, to, walked / length)); painter.LineTo(Vector2.Lerp(from, to, (walked + amount) / length)); painter.Stroke(); }
                        walked += amount; phase += amount;
                    }
                }
            };
        }
    }
}
