using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace PixelWizards.LightmapSwitcher
{
    public class LightmapSwitchAsset : PlayableAsset, ITimelineClipAsset
	{
        private ExposedReference<LevelLightmapData> lightMapData;
        public int index = 0;

        public ClipCaps clipCaps
		{
			get { return ClipCaps.None; }
		}

		public override Playable CreatePlayable (PlayableGraph graph, GameObject owner)
		{
            var playable = ScriptPlayable<LightmapSwitchBehaviour>.Create(graph);

            var behaviour = playable.GetBehaviour();
            behaviour.BindLightmapData(lightMapData.Resolve(graph.GetResolver()));
            behaviour.index = index;
            
            return playable;
        }
	}
}