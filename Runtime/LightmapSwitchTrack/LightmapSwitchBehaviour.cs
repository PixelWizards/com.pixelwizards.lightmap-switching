using UnityEngine;
using UnityEngine.Playables;

namespace PixelWizards.LightmapSwitcher
{
    public class LightmapSwitchBehaviour : PlayableBehaviour
	{
        private LevelLightmapData lightMapData;
        public int index;

		public override void ProcessFrame(Playable playable, FrameData info, object playerData)
		{
            var lightMapData = playerData as LevelLightmapData;

            if(lightMapData != null)
            {
                lightMapData.LoadLightingScenario(index);
            }
		}

        public void BindLightmapData(LevelLightmapData data)
        {
            lightMapData = data;
        }
	}
}