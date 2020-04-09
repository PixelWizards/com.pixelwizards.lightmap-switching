using UnityEngine.Timeline;

namespace PixelWizards.LightmapSwitcher
{
    [TrackClipType(typeof(LightmapSwitchAsset))]
	[TrackBindingType(typeof(LevelLightmapData))]
	public class LightmapSwitchTrack : TrackAsset {}
}