using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class LightingSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ManagerName = "Lighting Manager";
    private const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";

    [MenuItem("Tools/Setup Lighting")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[LightingSetup] Dừng Play mode trước khi chạy Setup.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            if (!string.IsNullOrEmpty(activeScene.path) || activeScene.rootCount > 0)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Light sun = EnsureSun();
        Volume volume = EnsureVolume();
        LightingManager manager = EnsureManager(sun, volume);

        LightingPreset dayPreset = manager.GetPreset(LightingState.Day);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = dayPreset.ambientSky;
        RenderSettings.ambientEquatorColor = dayPreset.ambientEquator;
        RenderSettings.ambientGroundColor = dayPreset.ambientGround;
        RenderSettings.ambientIntensity = dayPreset.ambientIntensity;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = dayPreset.fogColor;
        RenderSettings.fogDensity = dayPreset.fogDensity;
        RenderSettings.sun = sun;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[LightingSetup] Đã lưu SampleScene và SampleSceneProfile.");

        Debug.Log($"[LightingSetup] Hoàn tất. Sun='{sun.name}', Volume='{volume.name}', Manager='{manager.name}'. Vào Play để kiểm tra.");
    }

    private static Light EnsureSun()
    {
        Light sun = null;
        foreach (Light candidate in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (candidate.type != LightType.Directional) continue;
            sun = candidate;
            break;
        }

        if (sun == null)
        {
            GameObject go = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(go, "Create Directional Light");
            sun = Undo.AddComponent<Light>(go);
            sun.type = LightType.Directional;
        }

        Undo.RecordObject(sun, "Setup Directional Light");
        sun.enabled = true;
        sun.type = LightType.Directional;
        sun.useColorTemperature = false;
        sun.color = Color.white;
        sun.intensity = 1.2f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.lightmapBakeType = LightmapBakeType.Realtime;
        EditorUtility.SetDirty(sun);
        return sun;
    }

    private static Volume EnsureVolume()
    {
        Volume volume = Object.FindFirstObjectByType<Volume>();
        if (volume == null)
        {
            GameObject go = new GameObject("Global Volume");
            Undo.RegisterCreatedObjectUndo(go, "Create Global Volume");
            volume = Undo.AddComponent<Volume>(go);
        }

        Undo.RecordObject(volume, "Setup Global Volume");
        volume.isGlobal = true;

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[LightingSetup] Không tìm thấy '{ProfilePath}', giữ nguyên profile hiện tại.");
            return volume;
        }

        volume.sharedProfile = profile;
        EnsureColorAdjustments(profile);
        return volume;
    }

    private static void EnsureColorAdjustments(VolumeProfile profile)
    {
        if (profile.TryGet(out ColorAdjustments _)) return;

        Undo.RecordObject(profile, "Add Color Adjustments");

        ColorAdjustments added = profile.Add<ColorAdjustments>(true);
        added.active = true;

        // VolumeProfile.Add<T>() khong gan local fileID, component se serialize thanh {fileID: 0}.
        AssetDatabase.AddObjectToAsset(added, profile);
        EditorUtility.SetDirty(added);
        EditorUtility.SetDirty(profile);
    }

    private static LightingManager EnsureManager(Light sun, Volume volume)
    {
        LightingManager manager = Object.FindFirstObjectByType<LightingManager>();
        bool created = false;

        if (manager == null)
        {
            GameObject go = new GameObject(ManagerName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + ManagerName);
            manager = Undo.AddComponent<LightingManager>(go);
            created = true;
        }

        if (created)
        {
            manager.ResetPresetsToRecommended();
        }

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("sunLight").objectReferenceValue = sun;
        so.FindProperty("globalVolume").objectReferenceValue = volume;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        return manager;
    }
}
