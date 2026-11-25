using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    public enum TitleChipSize { Compact, Regular, Large }
    public enum TitlePartSection { First, Second }
    public enum StatusBadgeState { Ready, Waiting, Host, Joined, Offline, Collecting }

    /// <summary>Display data only; ownership and selected identity are supplied by the screen presenter.</summary>
    public sealed class TitlePartViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool Owned { get; set; }
        public bool Selected { get; set; }
    }

    public sealed class BadgeViewModel
    {
        public string Text { get; set; } = string.Empty;
        public bool Acquired { get; set; }
    }

    public sealed class ProfileViewModel
    {
        public string First { get; set; } = "芋煮会の";
        public string Second { get; set; } = "初心者";
        public string Name { get; set; } = string.Empty;
        public string MemberNumber { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string BadgeSummary { get; set; } = "達成したバッジ";
        public IReadOnlyList<BadgeViewModel> Badges { get; set; } = Array.Empty<BadgeViewModel>();
    }

    public sealed class MemberRowViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string First { get; set; } = "芋煮会の";
        public string Second { get; set; } = "初心者";
        public string BandColor { get; set; } = "#8B2310";
        public string OutfitColor { get; set; } = "#FFF3D6";
        public string Photo { get; set; } = string.Empty;
        public StatusBadgeState State { get; set; }
        public bool Dimmed { get; set; }
    }

    internal static class ProfileElements
    {
        internal static VisualElement Box(string className)
        {
            var box = new VisualElement();
            box.AddToClassList(className);
            return box;
        }

        internal static Label Label(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        internal static void Apply(VisualElement element, string className)
        {
            element.AddToClassList(className);
            UiTheme.Apply(element, "UI/Components/Profile");
        }

        internal static void Display(VisualElement element, bool shown) =>
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
    }

    [UxmlElement]
    public partial class TitleChips : VisualElement
    {
        private readonly Label first = ProfileElements.Label("芋煮会の", "title-chips__first");
        private readonly Label second = ProfileElements.Label("初心者", "title-chips__second");
        private TitleChipSize size = TitleChipSize.Regular;

        [UxmlAttribute] public string First { get => first.text; set => first.text = value ?? string.Empty; }
        [UxmlAttribute] public string Second { get => second.text; set => second.text = value ?? string.Empty; }
        [UxmlAttribute]
        public TitleChipSize Size
        {
            get => size;
            set
            {
                size = value;
                EnableInClassList("title-chips--compact", value == TitleChipSize.Compact);
                EnableInClassList("title-chips--large", value == TitleChipSize.Large);
            }
        }

        public TitleChips()
        {
            ProfileElements.Apply(this, "title-chips");
            first.AddToClassList("chip"); first.AddToClassList("chip--title-first");
            second.AddToClassList("chip"); second.AddToClassList("chip--title-second");
            Add(first); Add(second);
        }
    }

    [UxmlElement]
    public partial class TitlePartPicker : VisualElement
    {
        private readonly Label summary;
        private readonly VisualElement firstParts;
        private readonly VisualElement secondParts;
        public event Action<TitlePartSection, string> Picked;

        public TitlePartPicker()
        {
            ProfileElements.Apply(this, "title-part-picker");
            var header = ProfileElements.Box("title-part-picker__header");
            header.Add(ProfileElements.Label("二つ名をくみあわせる", "title-part-picker__heading"));
            summary = ProfileElements.Label(string.Empty, "title-part-picker__summary");
            header.Add(summary); Add(header);
            Add(ProfileElements.Label("前半", "title-part-picker__label"));
            firstParts = ProfileElements.Box("title-part-picker__parts"); Add(firstParts);
            Add(ProfileElements.Label("後半", "title-part-picker__label"));
            secondParts = ProfileElements.Box("title-part-picker__parts"); Add(secondParts);
        }

        public void Bind(IEnumerable<TitlePartViewModel> first, IEnumerable<TitlePartViewModel> second,
            string collectionSummary = "")
        {
            summary.text = collectionSummary ?? string.Empty;
            BindSection(firstParts, first, TitlePartSection.First);
            BindSection(secondParts, second, TitlePartSection.Second);
        }

        private void BindSection(VisualElement container, IEnumerable<TitlePartViewModel> items, TitlePartSection section)
        {
            container.Clear();
            foreach (TitlePartViewModel item in items ?? Enumerable.Empty<TitlePartViewModel>())
            {
                if (item == null) continue;
                string id = item.Id;
                var button = new Button(() => Picked?.Invoke(section, id)) { text = item.Owned ? item.Text : "???" };
                button.AddToClassList("title-part-picker__part");
                button.EnableInClassList("title-part-picker__part--first", section == TitlePartSection.First);
                button.EnableInClassList("title-part-picker__part--second", section == TitlePartSection.Second);
                button.EnableInClassList("title-part-picker__part--selected", item.Selected && item.Owned);
                button.EnableInClassList("title-part-picker__part--locked", !item.Owned);
                button.SetEnabled(item.Owned);
                container.Add(button);
            }
        }
    }

    [UxmlElement]
    public partial class Avatar : VisualElement
    {
        private readonly VisualElement outfit;
        private readonly VisualElement face;
        private readonly VisualElement band;
        private readonly VisualElement lines;
        private readonly VisualElement photoImage;
        private float size = 48;
        private string bandColor = "#8B2310";
        private string outfitColor = "#FFF3D6";
        private string photo = string.Empty;
        private bool dimmed;

        [UxmlAttribute]
        public float Size { get => size; set { size = Mathf.Max(0, value); style.width = size; style.height = size; } }
        [UxmlAttribute]
        public string BandColor { get => bandColor; set { bandColor = value; band.style.unityBackgroundImageTintColor = UiTheme.Color(value); } }
        [UxmlAttribute]
        public string OutfitColor { get => outfitColor; set { outfitColor = value; outfit.style.unityBackgroundImageTintColor = UiTheme.Color(value); } }
        [UxmlAttribute]
        public string Photo { get => photo; set { photo = value ?? string.Empty; UpdatePhoto(); } }
        [UxmlAttribute]
        public bool Dimmed { get => dimmed; set { dimmed = value; EnableInClassList("avatar--dimmed", value); } }

        public Avatar()
        {
            ProfileElements.Apply(this, "avatar");
            outfit = Layer("avatar_1_outfit_white");
            face = Layer("avatar_2_base");
            band = Layer("avatar_3_band_white");
            lines = Layer("avatar_4_band_line");
            photoImage = ProfileElements.Box("avatar__photo");
            photoImage.pickingMode = PickingMode.Ignore; Add(photoImage);
            Size = size; BandColor = bandColor; OutfitColor = outfitColor; UpdatePhoto();
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                outfit.style.backgroundImage = UiTheme.Texture("avatar_1_outfit_white");
                face.style.backgroundImage = UiTheme.Texture("avatar_2_base");
                band.style.backgroundImage = UiTheme.Texture("avatar_3_band_white");
                lines.style.backgroundImage = UiTheme.Texture("avatar_4_band_line");
                UpdatePhoto();
            });
        }

        private VisualElement Layer(string key)
        {
            var element = ProfileElements.Box("avatar__layer");
            element.style.backgroundImage = UiTheme.Texture(key);
            element.pickingMode = PickingMode.Ignore;
            Add(element);
            return element;
        }

        private void UpdatePhoto()
        {
            Texture2D texture = string.IsNullOrEmpty(photo) ? null : UiTheme.Texture(photo);
            bool hasPhoto = texture != null;
            photoImage.style.backgroundImage = texture;
            ProfileElements.Display(photoImage, hasPhoto);
            foreach (VisualElement layer in new[] { outfit, face, band, lines }) ProfileElements.Display(layer, !hasPhoto);
        }
    }

    [UxmlElement]
    public partial class ColorSwatch : Button
    {
        private readonly VisualElement creamRing;
        private readonly VisualElement greenRing;
        private string color = "#8B2310";
        private bool selected;

        [UxmlAttribute]
        public string Color { get => color; set { color = value; style.backgroundColor = UiTheme.Color(value); tooltip = value; } }
        [UxmlAttribute]
        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                ProfileElements.Display(creamRing, value);
                ProfileElements.Display(greenRing, value);
            }
        }

        public ColorSwatch()
        {
            ProfileElements.Apply(this, "color-swatch");
            creamRing = ProfileElements.Box("color-swatch__cream-ring");
            greenRing = ProfileElements.Box("color-swatch__green-ring");
            creamRing.pickingMode = PickingMode.Ignore; greenRing.pickingMode = PickingMode.Ignore;
            Add(creamRing); Add(greenRing); Color = color; Selected = false;
        }
    }

    [UxmlElement]
    public partial class AvatarEditor : VisualElement
    {
        private readonly Avatar preview;
        private readonly List<ColorSwatch> bands = new List<ColorSwatch>();
        private readonly List<ColorSwatch> outfits = new List<ColorSwatch>();
        private string bandColor = "#8B2310";
        private string outfitColor = "#FFF3D6";
        public event Action<string> BandColorChanged;
        public event Action<string> OutfitColorChanged;

        [UxmlAttribute]
        public string BandColor
        {
            get => bandColor;
            set { bandColor = value; preview.BandColor = value; Select(bands, value); }
        }
        [UxmlAttribute]
        public string OutfitColor
        {
            get => outfitColor;
            set { outfitColor = value; preview.OutfitColor = value; Select(outfits, value); }
        }

        public AvatarEditor()
        {
            ProfileElements.Apply(this, "avatar-editor");
            preview = new Avatar { Size = 110 };
            preview.style.height = 120;
            preview.AddToClassList("avatar-editor__preview"); Add(preview);
            var fields = ProfileElements.Box("avatar-editor__fields");
            fields.Add(ProfileElements.Label("自分のキャラの編集", "avatar-editor__heading"));
            fields.Add(ProfileElements.Label("はちまき", "avatar-editor__label"));
            AddSwatches(fields, bands, new[] { "#8B2310", "#34421C", "#2F5D8A", "#FFFFFF" }, true);
            fields.Add(ProfileElements.Label("ふく", "avatar-editor__label"));
            AddSwatches(fields, outfits, new[] { "#FFF3D6", "#6B9B3A", "#C9A27A", "#8FAFC4" }, false);
            Add(fields);
            BandColor = bandColor; OutfitColor = outfitColor;
        }

        private void AddSwatches(VisualElement fields, List<ColorSwatch> list, IEnumerable<string> palette, bool isBand)
        {
            var row = ProfileElements.Box("avatar-editor__swatches");
            foreach (string color in palette)
            {
                var swatch = new ColorSwatch { Color = color };
                swatch.EnableInClassList("color-swatch--spaced", list.Count != 0);
                swatch.clicked += () =>
                {
                    if (isBand) { BandColor = color; BandColorChanged?.Invoke(color); }
                    else { OutfitColor = color; OutfitColorChanged?.Invoke(color); }
                };
                list.Add(swatch); row.Add(swatch);
            }
            fields.Add(row);
        }

        private static void Select(IEnumerable<ColorSwatch> swatches, string color)
        {
            foreach (ColorSwatch swatch in swatches) swatch.Selected = string.Equals(swatch.Color, color, StringComparison.OrdinalIgnoreCase);
        }
    }

    [UxmlElement]
    public partial class BadgeGrid : VisualElement
    {
        public BadgeGrid() { ProfileElements.Apply(this, "badge-grid"); }

        public void Bind(IEnumerable<BadgeViewModel> items)
        {
            Clear();
            VisualElement row = null;
            int column = 0;
            foreach (BadgeViewModel badge in items ?? Enumerable.Empty<BadgeViewModel>())
            {
                if (badge == null) continue;
                if (column == 0)
                {
                    row = ProfileElements.Box("badge-grid__row");
                    row.EnableInClassList("badge-grid__row--spaced", childCount != 0);
                    Add(row);
                }
                var cell = ProfileElements.Box("badge-grid__badge");
                cell.EnableInClassList("badge-grid__badge--locked", !badge.Acquired);
                cell.EnableInClassList("badge-grid__badge--spaced", column != 0);
                var icon = ProfileElements.Box("badge-grid__icon"); icon.style.backgroundImage = UiTheme.Texture("icon_badge");
                cell.Add(icon); cell.Add(ProfileElements.Label(badge.Acquired ? badge.Text : "???", "badge-grid__text"));
                row.Add(cell);
                column = (column + 1) % 3;
            }
            if (row != null && column != 0)
            {
                for (; column < 3; column++)
                {
                    var empty = ProfileElements.Box("badge-grid__badge");
                    empty.AddToClassList("badge-grid__badge--spaced");
                    empty.style.visibility = Visibility.Hidden;
                    row.Add(empty);
                }
            }
        }
    }

    [UxmlElement]
    public partial class MemberCard : VisualElement
    {
        private readonly Label heading;
        private readonly Label hint;
        private readonly VisualElement front;
        private readonly BadgeGrid badges;
        private readonly Button photoButton;
        private readonly VisualElement photoIcon;
        private readonly TitleChips titles;
        private readonly Label titleCaption;
        private readonly Label nameCaption;
        private readonly Label playerName;
        private readonly TextField nameInput;
        private readonly Label detail;
        private readonly Label memberNumber;
        private bool backFacing;
        private bool editableName;
        private string badgeSummary = "達成したバッジ";
        public event Action<bool> Flipped;
        public event Action<string> NameChanged;
        public event Action PhotoRequested;

        [UxmlAttribute]
        public bool BackFacing { get => backFacing; set { backFacing = value; RefreshPresentation(); } }
        [UxmlAttribute]
        public bool EditableName { get => editableName; set { editableName = value; RefreshPresentation(); } }

        public MemberCard()
        {
            ProfileElements.Apply(this, "member-card");
            focusable = true;
            var header = ProfileElements.Box("member-card__header");
            heading = ProfileElements.Label("リモート芋煮会 会員証", "member-card__heading");
            hint = ProfileElements.Label("タップでバッジを見る", "member-card__hint");
            header.Add(heading); header.Add(hint); Add(header);
            front = ProfileElements.Box("member-card__front");
            photoButton = new Button(() => { if (editableName) PhotoRequested?.Invoke(); });
            photoButton.AddToClassList("member-card__photo"); photoButton.tooltip = "写真を選ぶ";
            photoIcon = ProfileElements.Box("member-card__camera"); photoIcon.style.backgroundImage = UiTheme.Texture("icon_camera");
            photoButton.Add(photoIcon); front.Add(photoButton);
            var info = ProfileElements.Box("member-card__info");
            titleCaption = ProfileElements.Label("二つ名", "member-card__caption"); info.Add(titleCaption);
            titles = new TitleChips(); info.Add(titles);
            nameCaption = ProfileElements.Label("なまえ", "member-card__caption"); info.Add(nameCaption);
            playerName = ProfileElements.Label(string.Empty, "member-card__name"); info.Add(playerName);
            nameInput = new TextField(); nameInput.AddToClassList("member-card__name-input");
            nameInput.RegisterValueChangedCallback(evt => NameChanged?.Invoke(evt.newValue)); info.Add(nameInput);
            detail = ProfileElements.Label(string.Empty, "member-card__detail"); info.Add(detail);
            memberNumber = ProfileElements.Label(string.Empty, "member-card__number"); info.Add(memberNumber);
            front.Add(info); Add(front);
            badges = new BadgeGrid(); Add(badges);
            RegisterCallback<ClickEvent>(evt =>
            {
                if (editableName) return;
                Flip();
            });
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!editableName && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.Space))
                { Flip(); evt.StopPropagation(); }
            });
            RefreshPresentation();
        }

        public void Bind(ProfileViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            titles.First = model.First; titles.Second = model.Second;
            playerName.text = model.Name; nameInput.SetValueWithoutNotify(model.Name ?? string.Empty);
            detail.text = model.Detail; memberNumber.text = model.MemberNumber;
            photoButton.style.backgroundImage = string.IsNullOrEmpty(model.Photo) ? null : UiTheme.Texture(model.Photo);
            badgeSummary = model.BadgeSummary ?? "達成したバッジ";
            badges.Bind(model.Badges);
            RefreshPresentation();
        }

        public void Flip() { BackFacing = !BackFacing; Flipped?.Invoke(BackFacing); }

        private void RefreshPresentation()
        {
            EnableInClassList("member-card--registration", editableName);
            ProfileElements.Display(front, !backFacing);
            ProfileElements.Display(badges, backFacing);
            ProfileElements.Display(titleCaption, editableName);
            ProfileElements.Display(nameCaption, editableName);
            ProfileElements.Display(nameInput, editableName);
            ProfileElements.Display(playerName, !editableName);
            ProfileElements.Display(detail, !editableName);
            ProfileElements.Display(photoIcon, editableName);
            photoButton.focusable = editableName;
            titles.Size = editableName ? TitleChipSize.Large : TitleChipSize.Regular;
            heading.text = backFacing ? badgeSummary : "リモート芋煮会 会員証";
            hint.text = editableName ? "MEMBER" : backFacing ? "タップで会員証へ" : "タップでバッジを見る";
        }
    }

    [UxmlElement]
    public partial class StatusBadge : Label
    {
        private StatusBadgeState state;
        [UxmlAttribute]
        public StatusBadgeState State
        {
            get => state;
            set
            {
                state = value;
                foreach (StatusBadgeState candidate in Enum.GetValues(typeof(StatusBadgeState)))
                    EnableInClassList("status-badge--" + candidate.ToString().ToLowerInvariant(), candidate == value);
                switch (value)
                {
                    case StatusBadgeState.Ready: text = "準備OK"; break;
                    case StatusBadgeState.Waiting: text = "準備中"; break;
                    case StatusBadgeState.Host: text = "ホスト"; break;
                    case StatusBadgeState.Joined: text = "参加!"; break;
                    case StatusBadgeState.Offline: text = "切断中"; break;
                    case StatusBadgeState.Collecting: text = "採集中"; break;
                }
            }
        }

        public StatusBadge() { ProfileElements.Apply(this, "status-badge"); State = StatusBadgeState.Ready; }
    }

    [UxmlElement]
    public partial class MemberRow : VisualElement
    {
        private readonly Avatar avatar;
        private readonly TitleChips titles;
        private readonly Label playerName;
        private readonly StatusBadge status;

        public MemberRow()
        {
            ProfileElements.Apply(this, "member-row");
            avatar = new Avatar { Size = 44 }; Add(avatar);
            var info = ProfileElements.Box("member-row__info");
            titles = new TitleChips { Size = TitleChipSize.Compact }; info.Add(titles);
            playerName = ProfileElements.Label(string.Empty, "member-row__name"); info.Add(playerName); Add(info);
            status = new StatusBadge(); Add(status);
        }

        public void Bind(MemberRowViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            avatar.BandColor = model.BandColor; avatar.OutfitColor = model.OutfitColor;
            avatar.Photo = model.Photo; avatar.Dimmed = model.Dimmed;
            titles.First = model.First; titles.Second = model.Second;
            playerName.text = model.Name; status.State = model.State;
        }
    }
}
