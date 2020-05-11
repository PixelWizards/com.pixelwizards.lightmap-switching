using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using PixelWizards.LightmapSwitcher;

namespace PixelWizards.LightmapSwitcher
{

    [CustomEditor(typeof(LevelLightmapData))]
    public class LevelLightmapDataEditor : Editor
    {
        public SerializedProperty lightingScenariosScenes;
        public SerializedProperty lightingScenesNames;
        public SerializedProperty allowLoadingLightingScenes;
        public SerializedProperty applyLightmapScaleAndOffset;

        GUIContent allowLoading = new GUIContent("Allow loading Lighting Scenes", "Allow the Level Lightmap Data script to load a lighting scene additively at runtime if the lighting scenario contains realtime lights.");

        public void OnEnable()
        {
            lightingScenariosScenes = serializedObject.FindProperty("lightingScenariosScenes");
            lightingScenesNames = serializedObject.FindProperty("lightingScenesNames");
            allowLoadingLightingScenes = serializedObject.FindProperty("allowLoadingLightingScenes");
            applyLightmapScaleAndOffset = serializedObject.FindProperty("applyLightmapScaleAndOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var lightmapData = (LevelLightmapData)target;
            GUILayout.Space(5f);
            GUILayout.Label("Lighting Data Config");
            GUILayout.Space(5f);
            lightmapData.config = EditorGUILayout.ObjectField(lightmapData.config, typeof(LightingScenarioConfig), false) as LightingScenarioConfig;
            if( lightmapData.config == null)
            {
                if(GUILayout.Button("Create Config"))
                {
                    var lsc = ScriptableObject.CreateInstance(typeof(LightingScenarioConfig));
                    lsc.name = "LightingScenario Config";
                    AssetDatabase.CreateAsset(lsc, "Assets/" + lsc.name.Replace(" ", "") + ".asset");
                    lightmapData.config = lsc as LightingScenarioConfig;
                    AssetDatabase.SaveAssets();
                    //EditorUtility.FocusProjectWindow();
                    //Selection.activeObject = lsc;
                }
            }
            GUILayout.Space(5f);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(lightingScenariosScenes, new GUIContent("Lighting Scenarios Scenes"), includeChildren: true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                lightingScenesNames.arraySize = lightingScenariosScenes.arraySize;

                for (int i = 0; i < lightingScenariosScenes.arraySize; i++)
                {
                    lightingScenesNames.GetArrayElementAtIndex(i).stringValue = lightingScenariosScenes.GetArrayElementAtIndex(i).objectReferenceValue == null ? "" : lightingScenariosScenes.GetArrayElementAtIndex(i).objectReferenceValue.name;
                }
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.PropertyField(allowLoadingLightingScenes, allowLoading);
            EditorGUILayout.PropertyField(applyLightmapScaleAndOffset);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10f);

            for (int i = 0; i < lightmapData.lightingScenariosScenes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                if (lightmapData.lightingScenariosScenes[i] != null)
                {
                    var sceneName = lightmapData.lightingScenariosScenes[i].name.ToString();
                    EditorGUILayout.LabelField(sceneName, EditorStyles.boldLabel);
                    if (GUILayout.Button("Build "))
                    {
                        if (UnityEditor.Lightmapping.giWorkflowMode != UnityEditor.Lightmapping.GIWorkflowMode.OnDemand)
                        {
                            Debug.LogError("ExtractLightmapData requires that you have baked you lightmaps and Auto mode is disabled.");
                        }
                        else
                            BuildLightingScenario(i, lightmapData);
                    }
                    if (GUILayout.Button("Store "))
                    {
                        lightmapData.StoreLightmapInfos(i, sceneName);
                    }
                    if (GUILayout.Button("Load "))
                    {
                        lightmapData.LoadLightingScenario(i);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(lightmapData.config);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        public void BuildLightingScenario(int ScenarioID, LevelLightmapData levelLightmapData)
        {
            //Remove reference to LightingDataAsset so that Unity doesn't delete the previous bake
            Lightmapping.lightingDataAsset = null;

            string currentBuildScenename = lightingScenariosScenes.GetArrayElementAtIndex(ScenarioID).objectReferenceValue.name;

            Debug.Log("Loading " + currentBuildScenename);

            string lightingSceneGUID = AssetDatabase.FindAssets(currentBuildScenename)[0];
            string lightingScenePath = AssetDatabase.GUIDToAssetPath(lightingSceneGUID);
            if (!lightingScenePath.EndsWith(".unity"))
                lightingScenePath = lightingScenePath + ".unity";

            EditorSceneManager.OpenScene(lightingScenePath, OpenSceneMode.Additive);

            Scene lightingScene = SceneManager.GetSceneByName(currentBuildScenename);
            EditorSceneManager.SetActiveScene(lightingScene);

            SearchLightsNeededRealtime(levelLightmapData);

            Debug.Log("Start baking");
            EditorCoroutineUtility.StartCoroutine(BuildLightingAsync(lightingScene), this);
        }

        private IEnumerator BuildLightingAsync(Scene lightingScene)
        {
            var newLightmapMode = new LightmapsMode();
            newLightmapMode = LightmapSettings.lightmapsMode;
            Lightmapping.BakeAsync();
            while (Lightmapping.isRunning) { yield return null; }
            //Lightmapping.lightingDataAsset = null;
            EditorSceneManager.SaveScene(lightingScene);
            EditorSceneManager.CloseScene(lightingScene, true);
            LightmapSettings.lightmapsMode = newLightmapMode;
        }

        public void SearchLightsNeededRealtime(LevelLightmapData levelLightmapData)
        {
            bool latestBuildHasRealtimeLights = false;

            var lights = FindObjectsOfType<Light>();
            var reflectionProbes = FindObjectsOfType<ReflectionProbe>();

            foreach (Light light in lights)
            {
                if (light.lightmapBakeType == LightmapBakeType.Mixed || light.lightmapBakeType == LightmapBakeType.Realtime)
                    latestBuildHasRealtimeLights = true;
            }
            if (reflectionProbes.Length > 0)
                latestBuildHasRealtimeLights = true;

            levelLightmapData.latestBuildHasReltimeLights = latestBuildHasRealtimeLights;
        }
    }
}