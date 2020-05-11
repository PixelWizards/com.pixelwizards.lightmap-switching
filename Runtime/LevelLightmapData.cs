using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;
#endif

namespace PixelWizards.LightmapSwitcher
{
    [System.Serializable]
    public class SphericalHarmonics
    {
        public float[] coefficients = new float[27];
    }

    [System.Serializable]
    public class RendererInfo
    {
        public Renderer renderer;
        public int lightmapIndex;
        public Vector4 lightmapOffsetScale;
    }

    [System.Serializable]
    public class LightingScenarioData
    {
        public string sceneName;                // the original source lighting scene that was used to generate this lighting scenario
        public RendererInfo[] rendererInfos;
        public Texture2D[] lightmaps;
        public Texture2D[] lightmapsDir;
        public Texture2D[] shadowMasks;
        public LightmapsMode lightmapsMode;
        public SphericalHarmonics[] lightProbes;
        public bool hasRealtimeLights;
    }

    [ExecuteInEditMode]
    public class LevelLightmapData : MonoBehaviour
    {
        public bool latestBuildHasReltimeLights;
        public bool allowLoadingLightingScenes = true;
        [Tooltip("Enable this if you want to use different lightmap resolutions in your different lighting scenarios. In that case you'll have to disable Static Batching in the Player Settings. When disabled, Static Batching can be used but all your lighting scenarios need to use the same lightmap resolution.")]
        public bool applyLightmapScaleAndOffset = true;

        public LightingScenarioConfig config;

#if UNITY_EDITOR
        [SerializeField]
        public List<SceneAsset> lightingScenariosScenes = new List<SceneAsset>();
#endif
        [SerializeField]
        public String[] lightingScenesNames = new string[1];
        public int currentLightingScenario = -1;
        public int previousLightingScenario = -1;

        private Coroutine m_SwitchSceneCoroutine;

        [SerializeField]
        public int lightingScenariosCount;

        //TODO : enable logs only when verbose enabled
        public bool verbose = false;

        private List<SphericalHarmonicsL2[]> lightProbesRuntime = new List<SphericalHarmonicsL2[]>();

        public int GetLightingScenarioByName(string name)
        {
            for (var i = 0; i < lightingScenesNames.Length; i++)
            {
                if (lightingScenesNames[i] == name)
                    return i;
            }
            return -1;
        }

        public void LoadLightingScenario(int index)
        {
            if (index != currentLightingScenario)
            {
                previousLightingScenario = currentLightingScenario == -1 ? index : currentLightingScenario;

                currentLightingScenario = index;

                LightmapSettings.lightmapsMode = config.lightingScenariosData[index].lightmapsMode;

                if (allowLoadingLightingScenes)
                    m_SwitchSceneCoroutine = StartCoroutine(SwitchSceneCoroutine(lightingScenesNames[previousLightingScenario], lightingScenesNames[currentLightingScenario]));

                var newLightmaps = LoadLightmaps(index);

                if (applyLightmapScaleAndOffset)
                {
                    ApplyRendererInfo(config.lightingScenariosData[index].rendererInfos);
                }

                LightmapSettings.lightmaps = newLightmaps;

                LoadLightProbes(currentLightingScenario);
            }
        }

        private void Start()
        {
            PrepareLightProbeArrays();
        }

        private void PrepareLightProbeArrays()
        {
            for (int x = 0; x < lightingScenariosCount; x++)
            {
                lightProbesRuntime.Add(DeserializeLightProbes(x));
            }
        }

        private SphericalHarmonicsL2[] DeserializeLightProbes(int index)
        {
            var sphericalHarmonicsArray = new SphericalHarmonicsL2[config.lightingScenariosData[index].lightProbes.Length];

            for (int i = 0; i < config.lightingScenariosData[index].lightProbes.Length; i++)
            {
                var sphericalHarmonics = new SphericalHarmonicsL2();

                // j is coefficient
                for (int j = 0; j < 3; j++)
                {
                    //k is channel ( r g b )
                    for (int k = 0; k < 9; k++)
                    {
                        sphericalHarmonics[j, k] = config.lightingScenariosData[index].lightProbes[i].coefficients[j * 9 + k];
                    }
                }

                sphericalHarmonicsArray[i] = sphericalHarmonics;
            }
            return sphericalHarmonicsArray;
        }

        IEnumerator SwitchSceneCoroutine(string sceneToUnload, string sceneToLoad)
        {
            AsyncOperation unloadop = null;
            AsyncOperation loadop = null;

            if (sceneToUnload != null && sceneToUnload != string.Empty && sceneToUnload != sceneToLoad)
            {
                if (Application.isPlaying)
                {
                    unloadop = SceneManager.UnloadSceneAsync(sceneToUnload);
                    while (!unloadop.isDone)
                    {
                        yield return new WaitForEndOfFrame();
                    }
                }
#if UNITY_EDITOR
                else
                {
                    var scene = EditorSceneManager.GetSceneByName(sceneToUnload);
                    if (scene.isLoaded)
                    {
                        var path = scene.path;
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
#endif
            }

            if (sceneToLoad != null && sceneToLoad != string.Empty && sceneToLoad != "")
            {
                if (Application.isPlaying)
                {
                    loadop = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
                    while ((!loadop.isDone || loadop == null))
                    {
                        yield return new WaitForEndOfFrame();
                    }
                }
#if UNITY_EDITOR
                else
                {

                    string lightingSceneGUID = AssetDatabase.FindAssets(sceneToLoad)[0];
                    string lightingScenePath = AssetDatabase.GUIDToAssetPath(lightingSceneGUID);
                    if (!lightingScenePath.EndsWith(".unity"))
                        lightingScenePath = lightingScenePath + ".unity";

                    EditorSceneManager.OpenScene(lightingScenePath, OpenSceneMode.Additive);

                    Scene lightingScene = SceneManager.GetSceneByName(sceneToLoad);
                    EditorSceneManager.SetActiveScene(lightingScene);
                }
#endif

                SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneToLoad));
            }
            LoadLightProbes(currentLightingScenario);
        }

        LightmapData[] LoadLightmaps(int index)
        {
            if (config.lightingScenariosData[index].lightmaps == null
                    || config.lightingScenariosData[index].lightmaps.Length == 0)
            {
                Debug.LogWarning("No lightmaps stored in scenario " + index);
                return null;
            }

            var newLightmaps = new LightmapData[config.lightingScenariosData[index].lightmaps.Length];

            for (int i = 0; i < newLightmaps.Length; i++)
            {
                newLightmaps[i] = new LightmapData();
                newLightmaps[i].lightmapColor = config.lightingScenariosData[index].lightmaps[i];

                if (config.lightingScenariosData[index].lightmapsMode != LightmapsMode.NonDirectional)
                {
                    newLightmaps[i].lightmapDir = config.lightingScenariosData[index].lightmapsDir[i];
                }
                if (config.lightingScenariosData[index].shadowMasks.Length > 0)
                {
                    newLightmaps[i].shadowMask = config.lightingScenariosData[index].shadowMasks[i];
                }
            }

            return newLightmaps;
        }

        public void ApplyRendererInfo(RendererInfo[] infos)
        {
            try
            {
                Terrain terrain = FindObjectOfType<Terrain>();
                int i = 0;
                if (terrain != null)
                {
                    terrain.lightmapIndex = infos[i].lightmapIndex;
                    terrain.lightmapScaleOffset = infos[i].lightmapOffsetScale;
                    i++;
                }

                for (int j = i; j < infos.Length; j++)
                {
                    RendererInfo info = infos[j];
                    info.renderer.lightmapIndex = infos[j].lightmapIndex;
                    if (!info.renderer.isPartOfStaticBatch)
                    {
                        info.renderer.lightmapScaleOffset = infos[j].lightmapOffsetScale;
                    }
                    if (info.renderer.isPartOfStaticBatch && verbose == true && Application.isEditor)
                    {
                        Debug.Log("Object " + info.renderer.gameObject.name + " is part of static batch, skipping lightmap offset and scale.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error in ApplyRendererInfo:" + e.GetType().ToString());
            }
        }

        public void LoadLightProbes(int index)
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                PrepareLightProbeArrays();
            }

            try
            {
                LightmapSettings.lightProbes.bakedProbes = lightProbesRuntime[index];
            }
            catch { Debug.LogWarning("Warning, error when trying to load lightprobes for scenario " + index); }
        }

        /// <summary>
        /// Deprecated - lighting data without knowing which scene it belongs to. 
        /// 
        /// Use StoreLightmapInfos(int index, string sceneName) instead!
        /// </summary>
        /// <param name="index"></param>
        public void StoreLightmapInfos(int index)
        {
            Debug.LogWarning("Warning: Storing lightmap scenario without scene name? - use StoreLightmapInfos(int index, string sceneName) instead!");
            StoreLightmapInfos(index, "unknown");
        }

        /// <summary>
        /// Store the lightmap data into our config
        /// </summary>
        /// <param name="index">which index this lightmap data belongs to</param>
        /// <param name="sceneName">which scene was used to generate the lighting data</param>
        public void StoreLightmapInfos(int index, string sceneName)
        {
            var newLightingScenarioData = new LightingScenarioData();
            newLightingScenarioData.sceneName = sceneName;

            var newRendererInfos = new List<RendererInfo>();
            var newLightmapsTextures = new List<Texture2D>();
            var newLightmapsTexturesDir = new List<Texture2D>();
            var newLightmapsMode = new LightmapsMode();
            var newSphericalHarmonicsList = new List<SphericalHarmonics>();
            var newLightmapsShadowMasks = new List<Texture2D>();

            newLightmapsMode = LightmapSettings.lightmapsMode;

            GenerateLightmapInfo(gameObject, newRendererInfos, newLightmapsTextures, newLightmapsTexturesDir, newLightmapsShadowMasks, newLightmapsMode);

            newLightingScenarioData.lightmapsMode = newLightmapsMode;

            newLightingScenarioData.lightmaps = newLightmapsTextures.ToArray();

            if (newLightmapsMode != LightmapsMode.NonDirectional)
            {
                newLightingScenarioData.lightmapsDir = newLightmapsTexturesDir.ToArray();
            }

            //Mixed or realtime support
            newLightingScenarioData.hasRealtimeLights = latestBuildHasReltimeLights;

            newLightingScenarioData.shadowMasks = newLightmapsShadowMasks.ToArray();

            newLightingScenarioData.rendererInfos = newRendererInfos.ToArray();

            var scene_LightProbes = new SphericalHarmonicsL2[LightmapSettings.lightProbes.bakedProbes.Length];
            scene_LightProbes = LightmapSettings.lightProbes.bakedProbes;

            for (int i = 0; i < scene_LightProbes.Length; i++)
            {
                var SHCoeff = new SphericalHarmonics();

                // j is coefficient
                for (int j = 0; j < 3; j++)
                {
                    //k is channel ( r g b )
                    for (int k = 0; k < 9; k++)
                    {
                        SHCoeff.coefficients[j * 9 + k] = scene_LightProbes[i][j, k];
                    }
                }

                newSphericalHarmonicsList.Add(SHCoeff);
            }

            newLightingScenarioData.lightProbes = newSphericalHarmonicsList.ToArray();

            if (config.lightingScenariosData.Count < index + 1)
            {
                config.lightingScenariosData.Insert(index, newLightingScenarioData);
            }
            else
            {
                config.lightingScenariosData[index] = newLightingScenarioData;
            }

            lightingScenariosCount = config.lightingScenariosData.Count;

            if (lightingScenesNames == null || lightingScenesNames.Length < lightingScenariosCount)
            {
                lightingScenesNames = new string[lightingScenariosCount];
            }
        }

        static void GenerateLightmapInfo(GameObject root, List<RendererInfo> newRendererInfos, List<Texture2D> newLightmapsLight, List<Texture2D> newLightmapsDir, List<Texture2D> newLightmapsShadow, LightmapsMode newLightmapsMode)
        {
            Terrain terrain = FindObjectOfType<Terrain>();
            if (terrain != null && terrain.lightmapIndex != -1 && terrain.lightmapIndex != 65534)
            {
                RendererInfo terrainRendererInfo = new RendererInfo();
                terrainRendererInfo.lightmapOffsetScale = terrain.lightmapScaleOffset;

                Texture2D lightmaplight = LightmapSettings.lightmaps[terrain.lightmapIndex].lightmapColor;
                terrainRendererInfo.lightmapIndex = newLightmapsLight.IndexOf(lightmaplight);
                if (terrainRendererInfo.lightmapIndex == -1)
                {
                    terrainRendererInfo.lightmapIndex = newLightmapsLight.Count;
                    newLightmapsLight.Add(lightmaplight);
                }

                if (newLightmapsMode != LightmapsMode.NonDirectional)
                {
                    Texture2D lightmapdir = LightmapSettings.lightmaps[terrain.lightmapIndex].lightmapDir;
                    terrainRendererInfo.lightmapIndex = newLightmapsDir.IndexOf(lightmapdir);
                    if (terrainRendererInfo.lightmapIndex == -1)
                    {
                        terrainRendererInfo.lightmapIndex = newLightmapsDir.Count;
                        newLightmapsDir.Add(lightmapdir);
                    }
                }
                if (LightmapSettings.lightmaps[terrain.lightmapIndex].shadowMask != null)
                {
                    Texture2D lightmapShadow = LightmapSettings.lightmaps[terrain.lightmapIndex].shadowMask;
                    terrainRendererInfo.lightmapIndex = newLightmapsShadow.IndexOf(lightmapShadow);
                    if (terrainRendererInfo.lightmapIndex == -1)
                    {
                        terrainRendererInfo.lightmapIndex = newLightmapsShadow.Count;
                        newLightmapsShadow.Add(lightmapShadow);
                    }
                }
                newRendererInfos.Add(terrainRendererInfo);

                if (Application.isEditor)
                    Debug.Log("Terrain lightmap stored in" + terrainRendererInfo.lightmapIndex.ToString());
            }

            var renderers = FindObjectsOfType(typeof(Renderer));

            if (Application.isEditor)
                Debug.Log("stored info for " + renderers.Length + " meshrenderers");

            foreach (Renderer renderer in renderers)
            {
                if (renderer.lightmapIndex != -1 && renderer.lightmapIndex != 65534)
                {
                    RendererInfo info = new RendererInfo();
                    info.renderer = renderer;
                    info.lightmapOffsetScale = renderer.lightmapScaleOffset;

                    Texture2D lightmaplight = LightmapSettings.lightmaps[renderer.lightmapIndex].lightmapColor;
                    info.lightmapIndex = newLightmapsLight.IndexOf(lightmaplight);
                    if (info.lightmapIndex == -1)
                    {
                        info.lightmapIndex = newLightmapsLight.Count;
                        newLightmapsLight.Add(lightmaplight);
                    }

                    if (newLightmapsMode != LightmapsMode.NonDirectional)
                    {
                        Texture2D lightmapdir = LightmapSettings.lightmaps[renderer.lightmapIndex].lightmapDir;
                        info.lightmapIndex = newLightmapsDir.IndexOf(lightmapdir);
                        if (info.lightmapIndex == -1)
                        {
                            info.lightmapIndex = newLightmapsDir.Count;
                            newLightmapsDir.Add(lightmapdir);
                        }
                    }
                    if (LightmapSettings.lightmaps[renderer.lightmapIndex].shadowMask != null)
                    {
                        Texture2D lightmapShadow = LightmapSettings.lightmaps[renderer.lightmapIndex].shadowMask;
                        info.lightmapIndex = newLightmapsShadow.IndexOf(lightmapShadow);
                        if (info.lightmapIndex == -1)
                        {
                            info.lightmapIndex = newLightmapsShadow.Count;
                            newLightmapsShadow.Add(lightmapShadow);
                        }
                    }
                    newRendererInfos.Add(info);
                }
            }
        }
    }

}