using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelWizards.LightmapSwitcher
{

    [CreateAssetMenu(fileName = "LightingScenarioConfig", menuName = "Lighting/LightingScenarioConfig", order = 1)]
    public class LightingScenarioConfig : ScriptableObject
    {
        [SerializeField]
        public List<LightingScenarioData> lightingScenariosData = new List<LightingScenarioData>();
    }
}