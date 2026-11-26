using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    /// <summary>Standalone screen copy, distinct from labels encapsulated by richer controls.</summary>
    [UxmlElement]
    public partial class ImoniText : Label
    {
        public ImoniText() { AddToClassList("imoni-text"); UiTheme.Apply(this, "UI/Components/ScreenPrimitives"); }
    }
    [UxmlElement]
    public partial class Artwork : VisualElement
    {
        private string source;
        [UxmlAttribute] public string Source { get => source; set { source = value; style.backgroundImage = UiTheme.Texture(value); } }
        public Artwork() { pickingMode = PickingMode.Ignore; AddToClassList("artwork"); UiTheme.Apply(this, "UI/Components/ScreenPrimitives"); RegisterCallback<AttachToPanelEvent>(_ => Source = source); }
    }
    [UxmlElement]
    public partial class InputEntry : TextField
    {
        [UxmlAttribute] public string Placeholder { get => textEdition.placeholder; set => textEdition.placeholder = value; }
        public InputEntry() { AddToClassList("input"); AddToClassList("input-entry"); UiTheme.Apply(this, "UI/Components/ScreenPrimitives"); }
    }
}
