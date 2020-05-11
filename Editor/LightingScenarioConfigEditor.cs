using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelWizards.LightmapSwitcher
{

    [CustomEditor(typeof(LightingScenarioConfig))]
    public class LightingScenarioConfigEditor : Editor
    {

        public override void OnInspectorGUI()
        {
            var config = (LightingScenarioConfig)target;

            GUILayout.Space(5f);
            GUILayout.Label("Lightmap Switch Config", EditorStyles.boldLabel);

            GUILayout.Space(10f);

            GUILayout.Label("Load this lightmap data from a LevelLightMap component to switch lighting scenarios", EditorStyles.helpBox);

            GUILayout.Space(10f);

            var scenarioCount = config.lightingScenariosData.Count;
            GUILayout.Label("This config contains " + scenarioCount + " Lighting Scenarios");

            GUILayout.Space(10f);
            GUILayout.Label("Scenario list", EditorStyles.boldLabel);
            GUILayout.Space(5f);
            foreach ( var scenario in config.lightingScenariosData)
            {
                GUILayout.Label("Lighting Scene: " + scenario.sceneName);
            }
        }
    }
}