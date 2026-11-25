using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Imoni.Core;
using Imoni.Game.UI;
using Imoni.Game.UI.Components;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Imoni.Editor
{
    /// <summary>Validates generated resources, UXML registration, gallery coverage, and UI intent wiring.</summary>
    public static class ImoniM2AValidation
    {
        private const string ComponentNamespace = "Imoni.Game.UI.Components.";
        private static readonly string[] Catalog =
        {
            "ImoniButton", "Card", "ScreenHeader", "StepTitle", "PagerDots", "TabBar", "SegmentedControl",
            "TitleChips", "TitlePartPicker", "Avatar", "AvatarEditor", "ColorSwatch", "MemberCard", "BadgeGrid",
            "MemberRow", "StatusBadge", "InviteCodeCard", "StatCard", "ProgressBar", "ChecklistStep", "QualityChip",
            "MaterialGoalChip", "BonusTierChips", "BigNumberLabel", "FundsPill", "BottomSheet", "InventoryRow",
            "ShopRow", "UpgradeRow", "RankingRow", "Toast", "ErrorMessage", "AnimatedPot", "PatternBackground", "ResultBanner"
        };
        private static readonly string[] FontNames =
        {
            "DelaGothicOne SDF", "ZenMaruGothic-Bold SDF", "ZenMaruGothic-Medium SDF", "ZenMaruGothic-Black SDF"
        };

        [MenuItem("Imoni/Validate Component Gallery")]
        public static void Validate()
        {
            var report = new JObject
            {
                ["passed"] = false,
                ["validatedAtUtc"] = DateTime.UtcNow.ToString("O"),
                ["unityVersion"] = Application.unityVersion,
                ["mode"] = EditorApplication.isPlaying ? "Play Mode" : "Edit Mode"
            };
            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/M2AValidation.json"));
            try
            {
                report["resources"] = ValidateResources();
                VisualTreeAsset galleryAsset = Resources.Load<VisualTreeAsset>("UI/ComponentGallery");
                Require(galleryAsset, "Resources/UI/ComponentGallery.uxml is missing. Run Imoni > Setup Project first.");
                // Intentionally unattached: validation must not start animations or alter the running scene.
                TemplateContainer gallery = galleryAsset.CloneTree();
                List<VisualElement> elements = Descendants(gallery).ToList();
                report["types"] = ValidateTypes(elements);
                report["variants"] = ValidateVariants(elements);
                report["basicBehaviors"] = ValidateBasicBehaviors();
                report["passed"] = true;
            }
            catch (Exception exception)
            {
                report["failure"] = exception.ToString();
                throw;
            }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, report.ToString(Formatting.Indented));
            }
        }

        private static JObject ValidateResources()
        {
            UIRuntimeAssets assets = Resources.Load<UIRuntimeAssets>("UI/UIRuntimeAssets");
            Require(assets, "UIRuntimeAssets is missing. Run Imoni > Setup Project before validating M2a.");
            Require(assets.config, "UIRuntimeAssets.config is missing. Run Imoni > Setup Project again.");
            Require(assets.theme, "UIRuntimeAssets.theme is missing. Run Imoni > Setup Project again.");
            Require(assets.theme == AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Imoni/UI/Theme.uss"),
                "UIRuntimeAssets.theme must reference Assets/Imoni/UI/Theme.uss.");
            Require(GameConfig.FromJson(assets.config.text) != null, "GameConfig could not be read.");
            Require(UiTheme.Config != null, "UiTheme.Config must be available after Setup Project.");
            Require(assets.textures != null && assets.textures.Length > 0, "The runtime texture registry is empty.");
            foreach (NamedTexture texture in assets.textures)
                Require(texture != null && !string.IsNullOrEmpty(texture.key) && texture.texture,
                    "The runtime texture registry contains a missing key or texture reference.");

            var fonts = new JArray();
            foreach (string name in FontNames)
            {
                string path = "Assets/Imoni/Fonts/Generated/" + name + ".asset";
                FontAsset font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                Require(font && font.sourceFontFile && font.material && font.atlasTextures != null &&
                    font.atlasTextures.Length > 0 && font.atlasTextures.All(texture => texture),
                    "Generated font, source, material, or atlas is missing: " + path);
                fonts.Add(path);
            }

            var icons = new JArray();
            foreach (string path in Directory.GetFiles("Assets/Imoni/Art/Sprites/Icons", "*.png").OrderBy(path => path, StringComparer.Ordinal))
            {
                string key = Path.GetFileNameWithoutExtension(path);
                Texture2D expected = AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
                Require(expected && UiTheme.Texture(key) == expected, "The runtime icon reference is missing or mismatched: " + key);
                icons.Add(key);
            }
            Require(icons.Count > 0, "No supplied PNG icons were found.");

            var styles = new JArray();
            Require(Resources.Load<StyleSheet>("UI/GalleryLayout") ==
                AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Imoni/Resources/UI/GalleryLayout.uss"),
                "GalleryLayout must resolve to its standalone USS, not a UXML inline StyleSheet.");
            foreach (string name in new[] { "Navigation", "Profile", "Data", "Effects" })
            {
                string path = "UI/Components/" + name;
                Require(Resources.Load<StyleSheet>(path), "Component style resource is missing: " + path);
                styles.Add(path);
            }
            return new JObject
            {
                ["runtimeAssets"] = AssetDatabase.GetAssetPath(assets),
                ["theme"] = AssetDatabase.GetAssetPath(assets.theme),
                ["config"] = AssetDatabase.GetAssetPath(assets.config),
                ["fonts"] = fonts,
                ["textureCount"] = assets.textures.Length,
                ["icons"] = icons,
                ["componentStyles"] = styles
            };
        }

        private static JArray ValidateTypes(IReadOnlyList<VisualElement> elements)
        {
            var result = new JArray();
            foreach (string name in Catalog)
            {
                Type type = ComponentType(name);
                Require(type.IsDefined(typeof(UxmlElementAttribute), false), name + " has no [UxmlElement] attribute.");
                Type serializedData = type.GetNestedType("UxmlSerializedData", BindingFlags.Public | BindingFlags.NonPublic);
                Require(serializedData != null && typeof(UxmlSerializedData).IsAssignableFrom(serializedData),
                    name + " has no generated UxmlSerializedData. Recompile the project in Unity.");
                int count = elements.Count(element => element.GetType() == type);
                Require(count > 0, "ComponentGallery does not contain " + name + ".");
                result.Add(new JObject { ["name"] = name, ["uxmlRegistered"] = true, ["galleryInstances"] = count });
            }
            return result;
        }

        private static JArray ValidateVariants(IReadOnlyList<VisualElement> elements)
        {
            var result = new JArray();
            foreach (string name in Catalog)
            {
                Type type = ComponentType(name);
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(property => property.PropertyType.IsEnum && property.IsDefined(typeof(UxmlAttributeAttribute), false)))
                {
                    foreach (object variant in Enum.GetValues(property.PropertyType))
                    {
                        int count = elements.Count(element => element.GetType() == type && Equals(property.GetValue(element), variant));
                        Require(count > 0, "ComponentGallery is missing " + name + "." + property.Name + " = " + variant + ".");
                        result.Add(new JObject { ["component"] = name, ["property"] = property.Name, ["value"] = variant.ToString(), ["galleryInstances"] = count });
                    }
                }
            }
            return result;
        }

        private static JObject ValidateBasicBehaviors()
        {
            var tabBar = new TabBar { Selected = TabSelection.Home };
            List<Button> tabs = tabBar.Query<Button>(className: "tabbar__item").ToList();
            Require(tabs.Count == Enum.GetValues(typeof(TabSelection)).Length && tabs[0].ClassListContains("tabbar__item--first") &&
                tabs.Skip(1).All(tab => !tab.ClassListContains("tabbar__item--first")), "TabBar's first-tab class is incorrect.");
            string requestedRoute = null;
            int navigationEvents = 0;
            tabBar.NavigateRequested += route =>
            {
                requestedRoute = route;
                navigationEvents++;
                tabBar.Selected = (TabSelection)Enum.Parse(typeof(TabSelection), route);
            };
            InvokeClick(tabBar.Q<Button>("Ranking"));
            Require(requestedRoute == "Ranking" && navigationEvents == 1 && tabBar.Selected == TabSelection.Ranking &&
                tabBar.Q<Button>("Ranking").ClassListContains("tabbar__item--on") && !tabs[0].ClassListContains("tabbar__item--on"),
                "TabBar navigation intent or selected presentation did not change correctly.");

            var segmented = new SegmentedControl { Options = "通常|エンドレス", Selected = 0 };
            int selectedIndex = -1;
            int changeEvents = 0;
            segmented.Changed += index => { selectedIndex = index; changeEvents++; };
            InvokeClick(segmented[1] as Button);
            Require(selectedIndex == 1 && changeEvents == 1 && segmented.Selected == 1 &&
                segmented[1].ClassListContains("segmented__option--selected") && !segmented[0].ClassListContains("segmented__option--selected"),
                "SegmentedControl did not emit and display the selected option.");

            var disabled = new ImoniButton { Disabled = true };
            Require(disabled.Disabled && !disabled.enabledSelf && !disabled.enabledInHierarchy, "ImoniButton disabled state was not forwarded to UI Toolkit.");
            disabled.Disabled = false;
            Require(disabled.enabledSelf && !disabled.Disabled, "ImoniButton could not be enabled again.");

            var card = new Card { Header = "見出し" };
            var content = new Label("内容");
            card.Add(content);
            Require(card.contentContainer != card && content.hierarchy.parent == card.contentContainer &&
                card.Q<Label>(className: "card-header")?.text == "見出し", "Card.Add did not preserve the heading and target the content slot.");

            var host = new VisualElement();
            var firstView = new DisposableView();
            var secondView = new DisposableView();
            var router = new ScreenRouter(host);
            using (router)
            {
                int routeEvents = 0;
                router.ScreenChanged += _ => routeEvents++;
                router.Register("First", () => firstView);
                router.Register("Second", () => secondView);
                Require(router.Show("First") && router.CurrentScreen == "First", "ScreenRouter could not show a registered route.");
                VisualElement first = router.CurrentView;
                Require(!router.Show("Unknown") && !router.Show(null) && router.CurrentView == first &&
                    router.CurrentScreen == "First" && host.childCount == 1 && host[0] == first && routeEvents == 1 &&
                    firstView.DisposeCount == 0 && secondView.DisposeCount == 0,
                    "An invalid route replaced or disposed the current screen, or emitted a change.");
                Require(router.Show("Second") && router.CurrentScreen == "Second" && router.CurrentView != first &&
                    host.childCount == 1 && host[0] == router.CurrentView && routeEvents == 2 &&
                    firstView.DisposeCount == 1 && secondView.DisposeCount == 0, "ScreenRouter did not dispose and replace the previous route exactly once.");
                Require(!router.Show("Unknown") && router.CurrentView == secondView && router.CurrentScreen == "Second" &&
                    host.childCount == 1 && host[0] == secondView && routeEvents == 2 &&
                    firstView.DisposeCount == 1 && secondView.DisposeCount == 0, "An unknown route changed the replacement screen's lifetime or state.");
            }
            Require(host.childCount == 0 && router.CurrentView == null && router.CurrentScreen == null &&
                firstView.DisposeCount == 1 && secondView.DisposeCount == 1, "Disposing ScreenRouter did not release its current view exactly once and clear its state.");
            router.Dispose();
            Require(firstView.DisposeCount == 1 && secondView.DisposeCount == 1, "Disposing ScreenRouter twice disposed a view again.");

            var safeRoot = new VisualElement();
            var safeArea = new SafeAreaAdapter(safeRoot);
            using (safeArea)
            {
                safeArea.ApplyInsets(12, 24, 36, 48);
                Require(Insets(safeRoot) == new Vector4(12, 24, 36, 48), "SafeAreaAdapter changed valid panel-unit insets.");
                safeArea.ApplyInsets(-12, 24, -36, 48);
                Require(Insets(safeRoot) == new Vector4(0, 24, 0, 48), "SafeAreaAdapter did not clamp left/right negative insets independently.");
                safeArea.ApplyInsets(12, -24, 36, -48);
                Require(Insets(safeRoot) == new Vector4(12, 0, 36, 0), "SafeAreaAdapter did not clamp top/bottom negative insets independently.");
            }
            safeArea.Dispose();
            Require(Insets(safeRoot) == new Vector4(12, 0, 36, 0), "Disposing SafeAreaAdapter unexpectedly changed applied padding.");
            // Check timer lifetime without attaching a panel or depending on a rendered frame.
            FieldInfo timerField = typeof(SafeAreaAdapter).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .SingleOrDefault(field => typeof(IVisualElementScheduledItem).IsAssignableFrom(field.FieldType));
            Require(timerField != null && timerField.GetValue(safeArea) is IVisualElementScheduledItem timer && !timer.isActive,
                "Disposing SafeAreaAdapter left its polling timer active.");
            return new JObject
            {
                ["tabBarFirstClassAndNavigationIntent"] = true,
                ["segmentedControlSelectionIntent"] = true,
                ["buttonEnabledState"] = true,
                ["cardContentSlot"] = true,
                ["routerValidAndInvalidRoutes"] = true,
                ["routerReplacementDisposesPreviousExactlyOnce"] = true,
                ["routerDispose"] = true,
                ["safeAreaInsetsClamp"] = true,
                ["safeAreaDisposeStopsTimer"] = true,
                ["scope"] = "Unattached UI intent smoke checks; pointer hit testing and rendered appearance require the Game view."
            };
        }

        private static Vector4 Insets(VisualElement element) => new Vector4(element.style.paddingLeft.value.value,
            element.style.paddingTop.value.value, element.style.paddingRight.value.value, element.style.paddingBottom.value.value);

        private sealed class DisposableView : VisualElement, IDisposable
        {
            public int DisposeCount { get; private set; }
            public void Dispose() => DisposeCount++;
        }

        private static void InvokeClick(Button button)
        {
            Require(button != null && button.clickable != null && button.enabledInHierarchy, "The expected enabled button was not found.");
            // Invoke Clickable's action path directly: a detached tree has no panel event dispatcher.
            MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(EventBase) }, null);
            Require(invoke != null, "The current Unity version does not expose Clickable.Invoke for intent validation.");
            using (ClickEvent click = ClickEvent.GetPooled())
            {
                click.target = button;
                invoke.Invoke(button.clickable, new object[] { click });
            }
        }

        private static Type ComponentType(string name)
        {
            Type type = typeof(ImoniButton).Assembly.GetType(ComponentNamespace + name);
            Require(type != null && typeof(VisualElement).IsAssignableFrom(type), "The catalog control is missing: " + name);
            return type;
        }

        private static IEnumerable<VisualElement> Descendants(VisualElement root)
        {
            yield return root;
            for (int index = 0; index < root.hierarchy.childCount; index++)
                foreach (VisualElement child in Descendants(root.hierarchy[index])) yield return child;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("M2a ComponentGallery validation: " + message);
        }
    }
}
