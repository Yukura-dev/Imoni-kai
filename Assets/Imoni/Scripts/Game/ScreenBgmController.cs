using System;
using System.Linq;
using Imoni.Core.Audio;
using Imoni.Game.UI;
using Newtonsoft.Json.Linq;

namespace Imoni.Game.Audio
{
    /// <summary>Routes the Core music policy to imported clips; overlays retain their caller's track.</summary>
    public sealed class ScreenBgmController : IDisposable
    {
        private readonly ScreenRouter router;
        private readonly AudioService service;
        private readonly UIRuntimeAssets assets;
        private readonly BgmPlan plan;
        public string StyleOverride { get; private set; }
        public ScreenBgmController(ScreenRouter router, AudioService service, UIRuntimeAssets assets, JObject config)
        {
            this.router = router; this.service = service; this.assets = assets;
            plan = new BgmPlan(config);
            router.ScreenChanged += Refresh;
            router.OverlayChanged += Refresh;
        }
        public void SetStyle(string value)
        {
            // Validate before changing the current policy.
            plan.Select(router.VisibleScreen, router.OverlaySourceScreen, value);
            StyleOverride = value;
            Refresh(router.VisibleScreen);
        }
        private void Refresh(string visibleScreen)
        {
            BgmSelection selection = plan.Select(visibleScreen, router.OverlaySourceScreen, StyleOverride);
            NamedBgmTrack track = selection.TrackKey == null ? null : assets.backgroundMusicTracks
                .FirstOrDefault(item => item.key == selection.TrackKey);
            if (selection.TrackKey != null && track == null)
                throw new InvalidOperationException("Missing BGM catalog track: " + selection.TrackKey);
            string context = router.HasOverlay ? visibleScreen + " from " + router.OverlaySourceScreen : visibleScreen;
            service.CrossFadeBgm(track?.key, track?.loop, selection.FadeSeconds, context, track?.gain ?? 1, track?.intro);
        }
        public void Dispose()
        {
            router.ScreenChanged -= Refresh; router.OverlayChanged -= Refresh;
        }
    }
}
