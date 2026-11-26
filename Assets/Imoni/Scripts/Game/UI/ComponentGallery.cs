using System;
using System.Collections.Generic;
using Imoni.Core;
using Imoni.Game.UI.Components;
using UnityEngine.UIElements;
using BigInteger = System.Numerics.BigInteger;
using ProgressBar = Imoni.Game.UI.Components.ProgressBar;

namespace Imoni.Game.UI
{
    /// <summary>Development-only fixtures. Examples send intents; they never change a game session.</summary>
    [UxmlElement]
    public partial class ComponentGallery : VisualElement
    {
        private readonly ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
        private readonly Dictionary<string, VisualElement> sections = new Dictionary<string, VisualElement>();
        private readonly Label feedback = UiTheme.Label("部品を選んで確認できます", "gallery-feedback");
        private PopupField<string> jump;
        public IReadOnlyDictionary<string, VisualElement> Sections => sections;

        public ComponentGallery()
        {
            name = "ComponentGallery";
            AddToClassList("component-gallery");
            UiTheme.Apply(this, "UI/GalleryLayout");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Add(UiTheme.Label("Component Gallery", "gallery-title"));
            Add(feedback);
            scroll.name = "gallery-scroll";
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("gallery-scroll");
            Add(scroll);
            Navigation(); Profile(); Data(); Effects();
            jump = new PopupField<string>(new List<string>(sections.Keys), 0) { name = "gallery-jump" };
            jump.RegisterValueChangedCallback(evt => ScrollToSection(evt.newValue));
            jump.AddToClassList("gallery-jump"); Insert(1, jump);
#endif
        }

        public bool ScrollToSection(string section, float pageOffset = 0)
        {
            if (!sections.TryGetValue(section, out VisualElement target)) return false;
            scroll.scrollOffset = new UnityEngine.Vector2(0, target.layout.y + pageOffset);
            jump?.SetValueWithoutNotify(section); return true;
        }

        private VisualElement Section(string title)
        {
            var section = new VisualElement { name = title };
            section.AddToClassList("gallery-section");
            section.Add(UiTheme.Label(title, "gallery-section-title"));
            sections.Add(title, section); scroll.Add(section); return section;
        }

        private void Sample(VisualElement section, string caption, VisualElement element)
        {
            var sample = new VisualElement(); sample.AddToClassList("gallery-sample");
            sample.Add(UiTheme.Label(caption, "gallery-caption"));
            bool cream = element is InventoryRow || element is ShopRow || element is UpgradeRow || element is RankingRow ||
                element is QualityChip || element is ChecklistStep || element is BonusTierChips || element is TitlePartPicker ||
                element is MemberRow || element is StatusBadge || element is ProgressBar || element is TitleChips || element is BadgeGrid ||
                (element is ImoniButton button && (button.Variant == ImoniButtonVariant.Yellow || button.Variant == ImoniButtonVariant.Outline || button.Variant == ImoniButtonVariant.Small || button.Variant == ImoniButtonVariant.Icon || button.Variant == ImoniButtonVariant.Action));
            if (cream)
            {
                var surface = new Card { Variant = CardVariant.Cream }; surface.AddToClassList("gallery-preview-card");
                surface.Add(element); sample.Add(surface);
            }
            else sample.Add(element);
            section.Add(sample);
        }

        private void Intent(string text) => feedback.text = text;
        private ImoniButton Action(string text, Action clicked)
        {
            var button = new ImoniButton { text = text, Variant = ImoniButtonVariant.Outline };
            button.clicked += clicked; return button;
        }

        private void Navigation()
        {
            var section = Section("ImoniButton");
            foreach (ImoniButtonVariant variant in Enum.GetValues(typeof(ImoniButtonVariant)))
            {
                foreach (bool disabled in new[] { false, true })
                {
                    var button = new ImoniButton { Variant = variant, text = variant == ImoniButtonVariant.Icon ? "" : "みんなで芋煮会!", Disabled = disabled,
                        Icon = variant == ImoniButtonVariant.Icon ? "icon_settings" : variant == ImoniButtonVariant.Action ? "icon_warehouse" : "" };
                    button.clicked += () => Intent(variant + " が押されました");
                    Sample(section, variant + (disabled ? " / disabled" : " / enabled"), button);
                }
            }
            Sample(section, "Action / selected", new ImoniButton { Variant = ImoniButtonVariant.Action, text = "倉庫", Icon = "icon_warehouse", Selected = true });
            section = Section("Card");
            foreach (CardVariant variant in Enum.GetValues(typeof(CardVariant)))
            {
                var card = new Card { Variant = variant, Header = "メンバー" };
                card.Add(UiTheme.Label("みんなで素材を集めよう!")); Sample(section, variant.ToString(), card);
            }
            Sample(section, "Cream / no header", new Card { Variant = CardVariant.Cream });
            section = Section("ScreenHeader");
            var header = new ScreenHeader { Title = "ロビー" }; header.BackRequested += () => Intent("戻る");
            Sample(section, "Lobby", header);
            header = new ScreenHeader { BackText = "キャンセル" }; header.RightContent.Add(new StatusBadge { State = StatusBadgeState.Waiting });
            Sample(section, "Waiting / right slot", header);
            section = Section("StepTitle");
            Sample(section, "Tutorial1 / inline", new StepTitle { Step = 1, Count = 3, Title = "会員登録しよう!" });
            Sample(section, "Tutorial2 / stacked", new StepTitle { Step = 2, Count = 3, Title = "みんなで芋煮会!", Stacked = true });
            Sample(section, "Tutorial3 / stacked", new StepTitle { Step = 3, Count = 3, Title = "素材の品質", Stacked = true });
            section = Section("PagerDots");
            for (int index = 0; index < 3; index++) Sample(section, "index " + index, new PagerDots { Count = 3, Index = index });
            section = Section("TabBar");
            foreach (TabSelection selected in Enum.GetValues(typeof(TabSelection)))
            {
                var tabs = new TabBar { Selected = selected };
                tabs.NavigateRequested += destination => { tabs.Selected = (TabSelection)Enum.Parse(typeof(TabSelection), destination); Intent("遷移要求: " + destination); };
                Sample(section, selected.ToString(), tabs);
            }
            section = Section("SegmentedControl");
            foreach (SegmentedVariant variant in Enum.GetValues(typeof(SegmentedVariant)))
                for (int index = 0; index < 2; index++)
                {
                    var segment = new SegmentedControl { Variant = variant, Options = variant == SegmentedVariant.Seed ? "共通シード|自由シード" : variant == SegmentedVariant.Tabs ? "スコア|到達ラウンド" : "通常|エンドレス", Selected = index };
                    segment.Changed += value => Intent("選択: " + value); Sample(section, variant + " / " + index, segment);
                }
            section = Section("InviteCodeCard");
            foreach (InviteCodeVariant variant in Enum.GetValues(typeof(InviteCodeVariant)))
            {
                var invite = new InviteCodeCard { Variant = variant, Code = "482715", TeamName = "いもにクラブ" };
                invite.CopyRequested += () => { invite.ShowCopied(); Intent("コピー表示のプレビュー"); };
                invite.ShareRequested += () => Intent("共有要求"); Sample(section, variant.ToString(), invite);
            }
            section = Section("Toast");
            var toast = new Toast { Text = "いもたろうが薪を3個集めた" }; Sample(section, "履歴", toast);
            section.Add(Action("トーストを表示", () => toast.Show("いもたろうが薪を3個集めた", UiTheme.Number("toastSeconds"))));
            section = Section("ErrorMessage"); Sample(section, "Lobby / invalid code", new ErrorMessage { Text = "コードが見つかりません。もう一度確認してください。" });
        }

        private void Profile()
        {
            var section = Section("TitleChips");
            foreach (TitleChipSize size in Enum.GetValues(typeof(TitleChipSize)))
                Sample(section, size.ToString(), new TitleChips { First = "芋煮会の", Second = "初心者", Size = size });
            section = Section("TitlePartPicker");
            var picker = new TitlePartPicker();
            picker.Bind(new[] { new TitlePartViewModel { Id = "first", Text = "芋煮会の", Owned = true, Selected = true }, new TitlePartViewModel { Id = "forest", Text = "森の", Owned = true }, new TitlePartViewModel { Id = "locked" } },
                new[] { new TitlePartViewModel { Id = "beginner", Text = "初心者", Owned = true, Selected = true }, new TitlePartViewModel { Id = "expert", Text = "達人", Owned = true }, new TitlePartViewModel { Id = "locked" } }, "パーツをあつめて、自分だけの二つ名に!");
            picker.Picked += (part, id) => Intent(part + ": " + id); Sample(section, "owned / selected / locked", picker);
            section = Section("Avatar");
            var avatars = new VisualElement(); avatars.AddToClassList("gallery-row");
            foreach (string color in new[] { "#8B2310", "#34421C", "#1E4A5C", "#4E2E53" }) avatars.Add(new Avatar { Size = 64, BandColor = color });
            Sample(section, "band colors", avatars);
            Sample(section, "dimmed", new Avatar { Size = 64, Dimmed = true });
            Sample(section, "photo", new Avatar { Size = 64, Photo = "avatar_2_base" });
            section = Section("AvatarEditor"); Sample(section, "band / outfit color selection", new AvatarEditor());
            section = Section("ColorSwatch");
            var swatches = new VisualElement(); swatches.AddToClassList("gallery-row");
            swatches.Add(new ColorSwatch { Color = "#8B2310", Selected = true }); swatches.Add(new ColorSwatch { Color = "#34421C" });
            Sample(section, "selected / unselected", swatches);
            var badges = new[] { new BadgeViewModel { Text = "はじめの一歩", Acquired = true }, new BadgeViewModel { Text = "芋煮名人", Acquired = true }, new BadgeViewModel { Text = "???" }, new BadgeViewModel { Text = "???" } };
            var profile = new ProfileViewModel { Name = "いもたろう", MemberNumber = "000128", Detail = "みんなで芋煮会!", Badges = badges };
            section = Section("MemberCard");
            foreach (bool editable in new[] { false, true })
            {
                var card = new MemberCard { EditableName = editable }; card.Bind(profile);
                card.NameChanged += value => Intent("名前: " + value); Sample(section, editable ? "editable front" : "front / tap to flip", card);
            }
            var back = new MemberCard { BackFacing = true }; back.Bind(profile); Sample(section, "back", back);
            section = Section("BadgeGrid"); var grid = new BadgeGrid(); grid.Bind(badges); Sample(section, "acquired / locked", grid);
            section = Section("MemberRow");
            foreach (StatusBadgeState state in Enum.GetValues(typeof(StatusBadgeState)))
            {
                var row = new MemberRow(); row.Bind(new MemberRowViewModel { Name = "いもたろう", State = state, Dimmed = state == StatusBadgeState.Offline });
                Sample(section, state.ToString(), row);
            }
            section = Section("StatusBadge");
            foreach (StatusBadgeState state in Enum.GetValues(typeof(StatusBadgeState))) Sample(section, state.ToString(), new StatusBadge { State = state });
        }

        private void Data()
        {
            var section = Section("StatCard");
            foreach (StatCardVariant variant in Enum.GetValues(typeof(StatCardVariant))) Sample(section, variant.ToString(), new StatCard { Variant = variant, Label = "最高到達", Value = "R3" });
            section = Section("ProgressBar");
            foreach (ProgressVariant variant in Enum.GetValues(typeof(ProgressVariant)))
                foreach (float value in new[] { 0f, .5f, 1f }) Sample(section, variant + " / " + value, new ProgressBar { Variant = variant, Value = value });
            Sample(section, "custom fill", new ProgressBar { Value = .7f, FillColor = "#8B2310" });
            section = Section("ChecklistStep");
            foreach (ChecklistState state in Enum.GetValues(typeof(ChecklistState))) Sample(section, state.ToString(), new ChecklistStep { State = state, Label = "ワールドを生成", Note = state == ChecklistState.Done ? "完了" : "" });
            section = Section("QualityChip");
            foreach (string id in new[] { "normal", "good", "rare", "legend", "myth", "transcend", "ultimate" })
            {
                Sample(section, id, new QualityChip { Quality = id }); Sample(section, id + " / multiplier", new QualityChip { Quality = id, ShowMultiplier = true });
            }
            section = Section("MaterialGoalChip");
            foreach (MaterialGoalVariant variant in Enum.GetValues(typeof(MaterialGoalVariant)))
                foreach (string amount in new[] { "10", "20" }) Sample(section, variant + " / " + amount, new MaterialGoalChip { Variant = variant, Material = "taro", Current = amount, Required = "20" });
            section = Section("BonusTierChips");
            foreach (string amount in new[] { "0", "100", "150", "200", "300" }) Sample(section, amount + "%", new BonusTierChips { Current = amount, Required = "100" });
            section = Section("BigNumberLabel");
            foreach (string amount in new[] { "18420", "100000000", "1000000000000000" }) Sample(section, amount, new BigNumberLabel { Value = amount });
            var counting = new BigNumberLabel { Value = "0" }; Sample(section, "count up", counting);
            section.Add(Action("カウントアップ", () => { counting.Bind(BigInteger.Zero); counting.CountTo(new BigInteger(18420), (float)UiTheme.Number("countUpSeconds")); }));
            section = Section("FundsPill"); Sample(section, "Game HUD", new FundsPill { Value = "1280" });
            section = Section("BottomSheet");
            var sheet = new BottomSheet { Title = "倉庫", FundsText = "1,280 G", Inline = true, IsOpen = true };
            sheet.Add(UiTheme.Label("中身のスロット")); sheet.CloseRequested += () => sheet.Close();
            Sample(section, "open / close / content slot", sheet); section.Add(Action("パネルを開く", sheet.Open));
            section = Section("InventoryRow");
            foreach (bool selected in new[] { false, true })
            {
                var row = new InventoryRow(); row.Bind(new InventoryRowView { Id = "wood", Name = "薪", QualityId = "good", OwnedText = "24", ProtectedText = "10", SellableText = "14", UnitPriceText = "2", Selected = selected });
                row.InventorySelectionRequested += id => { row.Selected = !row.Selected; Intent("選択: " + id); }; Sample(section, selected ? "selected" : "unselected", row);
            }
            section = Section("ShopRow");
            foreach (bool enabled in new[] { true, false })
            {
                var shop = new ShopRow(); shop.Bind(new ShopRowView { Name = "薪", UnitText = "1個 2G", Options = new[] {
                    new PurchaseOptionView { Id = "ten", QuantityText = "10", PriceText = "20 G" },
                    new PurchaseOptionView { Id = "fifty", QuantityText = "50", PriceText = "100 G", Enabled = enabled } } });
                shop.BuyRequested += id => Intent("購入要求: " + id); Sample(section, enabled ? "enabled purchase" : "disabled purchase", shop);
            }
            section = Section("UpgradeRow");
            foreach (bool enabled in new[] { true, false })
            {
                var row = new UpgradeRow(); row.Bind(new UpgradeRowView { Name = "採集速度", LevelText = "Lv.0", CurrentEffect = "1.00倍", NextEffect = "1.10倍", PriceText = "150 G", Enabled = enabled });
                row.UpgradeRequested += () => Intent("強化要求"); Sample(section, enabled ? "enabled" : "disabled", row);
            }
            section = Section("RankingRow");
            foreach (RankingRankStyle rank in Enum.GetValues(typeof(RankingRankStyle)))
                foreach (bool alternating in new[] { false, true })
                {
                    var row = new RankingRow(); row.Bind(new RankingRowView { RankText = rank == RankingRankStyle.First ? "1" : rank == RankingRankStyle.Podium ? "2" : "4", Style = rank, Team = "いもにクラブ", Detail = "最高到達 R3", ScoreText = Format.Big(BigInteger.Parse(UiTheme.Data.SelectToken("outGame.ranking.commonNormal[0].score").ToString()), UiTheme.Config), Alternating = alternating });
                    Sample(section, rank + " / alternate " + alternating, row);
                }
        }

        private void Effects()
        {
            var section = Section("AnimatedPot");
            foreach (PotVariant variant in Enum.GetValues(typeof(PotVariant)))
                foreach (bool steam in new[] { false, true })
                    foreach (bool sway in new[] { false, true }) Sample(section, variant + " / steam " + steam + " / sway " + sway, new AnimatedPot { Variant = variant, Steam = steam, Sway = sway });
            section = Section("PatternBackground");
            foreach (PatternVariant variant in Enum.GetValues(typeof(PatternVariant)))
            {
                var pattern = new PatternBackground { Variant = variant }; pattern.AddToClassList("gallery-pattern"); Sample(section, variant.ToString(), pattern);
            }
            section = Section("ResultBanner");
            foreach (ResultBannerVariant variant in Enum.GetValues(typeof(ResultBannerVariant)))
            {
                var banner = new ResultBanner { Variant = variant, MissingMaterial = "魚" };
                Sample(section, variant.ToString(), banner); section.Add(Action("再生: " + variant, banner.Play));
            }
        }
    }
}
