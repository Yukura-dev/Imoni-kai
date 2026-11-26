using System;
using System.Collections.Generic;
using Imoni.Core;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    [UxmlElement]
    public partial class AccountPicker : VisualElement
    {
        private readonly ScrollView accounts = new ScrollView(ScrollViewMode.Vertical) { name = "saved-accounts" };
        private readonly InputEntry entry = new InputEntry { name = "account-name", Placeholder = "新しい会員の名前" };
        private readonly ImoniButton create = new ImoniButton { name = "account-create", Variant = ImoniButtonVariant.Yellow, text = "新しい会員ではじめる" };
        public event Action<string> SwitchRequested;
        public event Action<string> CreateRequested;
        public event Action BackRequested;
        public AccountPicker()
        {
            AddToClassList("account-picker"); UiTheme.Apply(this, "UI/Components/AudioSettings");
            accounts.AddToClassList("account-picker__list"); entry.AddToClassList("account-picker__entry");
            create.AddToClassList("settings-dialog__action");
            var back = new ImoniButton { name = "account-back", Variant = ImoniButtonVariant.Outline, text = "音の設定に戻る" };
            back.AddToClassList("settings-dialog__action");
            Add(accounts); Add(entry); Add(create); Add(back);
            create.SetEnabled(false);
            entry.RegisterValueChangedCallback(evt => create.SetEnabled(!string.IsNullOrWhiteSpace(evt.newValue)));
            create.clicked += () => CreateRequested?.Invoke(entry.value.Trim());
            back.clicked += () => BackRequested?.Invoke();
        }
        public void Bind(IEnumerable<string> ids, string active, Func<string, ProfileData> profile, int maximumNameLength)
        {
            accounts.Clear(); entry.maxLength = maximumNameLength; entry.value = string.Empty;
            foreach (string id in ids)
            {
                ProfileData saved = profile(id);
                var button = new ImoniButton { name = "account-" + id, Variant = ImoniButtonVariant.Outline,
                    text = saved.Name + (id == active ? "（使用中）" : "に切り替える") };
                button.AddToClassList("account-picker__account"); button.SetEnabled(id != active);
                button.clicked += () => SwitchRequested?.Invoke(id); accounts.Add(button);
            }
        }
    }
}
