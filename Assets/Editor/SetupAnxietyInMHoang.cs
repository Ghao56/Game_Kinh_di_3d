using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// shortcut: setup cứng cho MHoang.unity, báo lỗi nếu scene không có Player/Radio, gỡ bỏ khi map ổn định
public static class SetupAnxietyInMHoang
{
    const string ScenePath = "Assets/Scenes/MHoang.unity";
    const string EnemyPrefabPath = "Assets/khang/Prefab/Enemy.prefab";

    [MenuItem("Tools/Setup Anxiety In MHoang")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 1. AnxietyManager (tránh chạy 2 lần tạo trùng)
        var existing = Object.FindFirstObjectByType<AnxietyManager>();
        GameObject managerGo;
        if (existing != null)
        {
            managerGo = existing.gameObject;
            Debug.Log("[AnxietySetup] AnxietyManager đã có sẵn, cập nhật lại cấu hình.");
        }
        else
        {
            managerGo = new GameObject("AnxietyManager");
            managerGo.AddComponent<AnxietyManager>();
            managerGo.AddComponent<DarknessAnxiety>();
        }

        var manager = managerGo.GetComponent<AnxietyManager>();

        // 2. Player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.ToLower().Contains("player")) { player = root; break; }
            }
        }
        if (player == null)
        {
            Debug.LogError("[AnxietySetup] Không tìm thấy Player trong scene, dừng lại.");
            return;
        }

        var so = new SerializedObject(manager);
        so.FindProperty("player").objectReferenceValue = player.transform;

        // 3. Threshold quái xuất hiện bằng Enemy.prefab
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (enemyPrefab == null)
        {
            Debug.LogError("[AnxietySetup] Không thấy " + EnemyPrefabPath);
            return;
        }
        var thresholds = so.FindProperty("thresholds");
        if (thresholds.arraySize == 0)
        {
            thresholds.arraySize = 2;
            var t0 = thresholds.GetArrayElementAtIndex(0);
            t0.FindPropertyRelative("name").stringValue = "Enemy co ban";
            t0.FindPropertyRelative("triggerAt").floatValue = 50f;
            t0.FindPropertyRelative("prefab").objectReferenceValue = enemyPrefab;
            t0.FindPropertyRelative("spawnDistance").floatValue = 8f;
            t0.FindPropertyRelative("despawnWhenBelow").boolValue = true;
            var t1 = thresholds.GetArrayElementAtIndex(1);
            t1.FindPropertyRelative("name").stringValue = "Enemy cang lo";
            t1.FindPropertyRelative("triggerAt").floatValue = 80f;
            t1.FindPropertyRelative("prefab").objectReferenceValue = enemyPrefab;
            t1.FindPropertyRelative("spawnDistance").floatValue = 5f;
            t1.FindPropertyRelative("despawnWhenBelow").boolValue = true;
        }
        so.ApplyModifiedProperties();

        // 4. Radio: AudioSource + vùng tương tác E + nguồn lo âu
        GameObject radio = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name.ToLower().Contains("radio")) { radio = root; break; }
        if (radio == null)
        {
            Debug.LogWarning("[AnxietySetup] Không tìm thấy object 'Radio' trong scene — bỏ qua bước radio, bạn gắn tay.");
        }
        else
        {
            if (radio.GetComponent<AudioSource>() == null)
                radio.AddComponent<AudioSource>();
            var col = radio.GetComponent<SphereCollider>();
            if (col == null)
            {
                col = radio.AddComponent<SphereCollider>();
                col.radius = 3f;
                col.isTrigger = true;
            }
            if (radio.GetComponent<InteractPlayMusic>() == null)
                radio.AddComponent<InteractPlayMusic>();
            if (radio.GetComponent<AnxietySource>() == null)
                radio.AddComponent<AnxietySource>();
            Debug.Log("[AnxietySetup] Đã cài radio: " + radio.name);
        }

        // 5. UI thanh lo âu
        Canvas canvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            canvas = root.GetComponent<Canvas>();
            if (canvas != null) break;
        }
        if (canvas == null)
        {
            var canvasGo = new GameObject("AnxietyCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
        }

        var sliderGo = new GameObject("VisionBar", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(canvas.transform, false);
        var rect = sliderGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -20f);
        rect.sizeDelta = new Vector2(400f, 20f);

        var slider = sliderGo.GetComponent<Slider>();
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sliderGo.transform, false);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        var faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero; faRect.anchorMax = Vector2.one;
        faRect.offsetMin = new Vector2(2f, 2f); faRect.offsetMax = new Vector2(-2f, -2f);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fill.GetComponent<Image>().color = Color.white;
        slider.fillRect = fillRect;
        slider.targetGraphic = fill.GetComponent<Image>();
        slider.minValue = 0f; slider.maxValue = 1f;
        slider.interactable = false;

        var visionBar = sliderGo.AddComponent<VisionBar>();
        visionBar.slider = slider;
        visionBar.manager = manager;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AnxietySetup] Xong. Đã lưu " + ScenePath);
    }
}
