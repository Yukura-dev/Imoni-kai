using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    /// <summary>Non-destructive presentation variant: clips the detached steam without resampling the lettering.</summary>
    [UxmlElement]
    public partial class TitleLogo : VisualElement
    {
        public TitleLogo()
        {
            pickingMode = PickingMode.Ignore;
            var clip = new VisualElement { pickingMode = PickingMode.Ignore };
            var artwork = new Artwork { Source = "title_logo@3x" };
            clip.style.overflow = Overflow.Hidden;
            clip.style.position = Position.Absolute; clip.style.left = 0; clip.style.top = 0; clip.style.bottom = 0;
            artwork.style.position = Position.Absolute; artwork.style.left = 0; artwork.style.top = 0; artwork.style.bottom = 0;
            clip.Add(artwork); Add(clip);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                float fraction = UiTheme.Data["ui"]["titleLogoVisibleWidthFraction"].Value<float>();
                clip.style.width = Length.Percent(fraction * 100);
                artwork.style.width = Length.Percent(100 / fraction);
            });
        }
    }
}
