using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor tool: bake style runtime của SettingsUIRestyler vào SettingsPanel.prefab
/// để UI nhìn thấy trong Editor (Prefab Mode) giống 100% khi Play.
///
/// Sau khi bake:
/// - Tất cả object/style do restyler sinh ra (VeilPanel, ColumnPanel, tiêu đề,
///   % label, màu, vị trí...) được LƯU VÀO prefab asset.
/// - SettingsUIRestyler lúc runtime thấy prefab đã bake (IsBaked) sẽ bỏ qua
///   restyle, chỉ gắn updater cho % label -> mọi chỉnh sửa trên prefab giữ nguyên.
/// - Cả BackButton/Btn_Exit trong scene game cũng được normalize để mở scene
///   nhìn đúng như khi Play (Btn_Exit nằm riêng trong scene, không thuộc prefab).
/// </summary>
public static class SettingsUIBaker
{
    private const string PrefabPath = "Assets/khang/setting_menu/SettingsPanel.prefab";
    private const string GameScenePath = "Assets/Scenes/MAIN_GAME/MainGame.unity";

    // Trùng màu với SettingsUIRestyler để normalize nút Back/Exit trong scene
    private static readonly Color TextMain = new Color(0.90f, 0.88f, 0.86f, 1f);
    private static readonly Color ControlDark = new Color(0.13f, 0.13f, 0.15f, 1f);

    [MenuItem("Tools/Settings UI/Bake Style Vào Prefab")]
    public static void Bake() => BakeInternal(resetFirst: false);

    [MenuItem("Tools/Settings UI/Bake Lại Từ Đầu (xoá style cũ)")]
    public static void ReBake() => BakeInternal(resetFirst: true);

    private static bool CanBake() => !Application.isPlaying && !EditorApplication.isCompiling;

    [MenuItem("Tools/Settings UI/Bake Style Vào Prefab", true)]
    private static bool ValidateBake() => CanBake();

    [MenuItem("Tools/Settings UI/Bake Lại Từ Đầu (xoá style cũ)", true)]
    private static bool ValidateReBake() => CanBake();

    private static void BakeInternal(bool resetFirst)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError($"[SettingsUIBaker] Không tìm thấy prefab: {PrefabPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (resetFirst) DeleteGeneratedStyle(root);

            var restyler = root.GetComponentInChildren<SettingsUIRestyler>(true);
            if (restyler == null) restyler = root.AddComponent<SettingsUIRestyler>();

            // Chạy đúng chuỗi lệnh mà Awake chạy lúc Play, nhưng ngay trong Editor
            restyler.RunFullRestyle();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SettingsUIBaker] Đã bake ({(resetFirst ? "re-bake" : "bake")}) style vào {PrefabPath}. " +
                      "Mở Prefab Mode để chỉnh sửa; lúc Play sẽ giữ nguyên các thay đổi này.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        PolishSceneButtons();

        AssetDatabase.Refresh();
    }

    // ───────────────────────── Prefab ─────────────────────────

    // Xoá các object do restyle sinh ra + bật lại nền cũ (child "SettingsPanel")
    // để đưa prefab về trạng thái gốc trước khi bake, sau đó bake lại toàn bộ.
    private static void DeleteGeneratedStyle(GameObject prefabRoot)
    {
        Canvas canvas = prefabRoot.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogError("[SettingsUIBaker] Prefab không có Canvas nào.");
            return;
        }

        Transform root = canvas.transform;
        string[] generated = { "VeilPanel", "ColumnPanel", "SETTINGS", "AUDIO", "DISPLAY" };

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            foreach (string name in generated)
            {
                if (child.name == name)
                {
                    Object.DestroyImmediate(child.gameObject);
                    break;
                }
            }
        }

        foreach (Slider s in root.GetComponentsInChildren<Slider>(true))
        {
            DestroyChildIfExists(s.transform, "ValueLabel");
            DestroyChildIfExists(s.transform, "SensitivityLabel");
        }

        foreach (TMP_Dropdown d in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            if (d.transform.parent != null) DestroyChildIfExists(d.transform.parent, "Header");
        }

        foreach (Toggle t in root.GetComponentsInChildren<Toggle>(true))
        {
            if (t.transform.parent != null) DestroyChildIfExists(t.transform.parent, "FullscreenHeader");
        }

        // Bật lại nền cũ (ghosts.png); RunFullRestyle sẽ tự tắt lại như runtime cũ
        Transform oldBg = root.Find("SettingsPanel");
        if (oldBg != null) oldBg.gameObject.SetActive(true);
    }

    private static void DestroyChildIfExists(Transform parent, string childName)
    {
        if (parent == null) return;
        Transform child = parent.Find(childName);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    // ───────────────────────── Scene game ─────────────────────────

    // Btn_Exit nằm riêng trong scene game (không thuộc prefab) và từng được
    // restyler định vị lúc runtime. Bake xong thì restyler không chạy lại
    // nữa, nên phải normalize trực tiếp trong scene cho edit mode trông đúng.
    private static void PolishSceneButtons()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
        {
            Debug.LogWarning($"[SettingsUIBaker] Không tìm thấy scene: {GameScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject exit = FindInScene(scene, "Btn_Exit");
            GameObject back = FindInScene(scene, "BackButton");

            if (exit == null && back == null)
            {
                Debug.Log("[SettingsUIBaker] Scene không có Btn_Exit/BackButton, bỏ qua normalize scene.");
                return;
            }

            ApplyButton(back, back != null && exit != null ? new Vector2(-140f, -485f) : new Vector2(0f, -485f), "BACK");
            ApplyButton(exit, new Vector2(140f, -485f), "EXIT");

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SettingsUIBaker] Đã normalize BackButton/Btn_Exit trong {GameScenePath}.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    // DFS theo tên GameObject trong một scene cụ thể (kể cả object inactive)
    private static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
    {
        GameObject found = null;

        void Visit(Transform t)
        {
            if (found != null) return;
            if (t.name == name) { found = t.gameObject; return; }
            for (int i = 0; i < t.childCount; i++) Visit(t.GetChild(i));
        }

        foreach (GameObject rootGo in scene.GetRootGameObjects())
            Visit(rootGo.transform);

        return found;
    }

    // Sao chép các giá trị mà StyleBackButton áp dụng lúc runtime vào prefab gốc
    private static void ApplyButton(GameObject go, Vector2 position, string text)
    {
        if (go == null) return;

        RectTransform rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260f, 54f);
            rt.anchoredPosition = position;
        }

        Image img = go.GetComponent<Image>();
        if (img != null) img.color = ControlDark;

        TMP_Text textComp = go.GetComponentInChildren<TMP_Text>(true);
        if (textComp != null)
        {
            textComp.fontSize = 30f;
            textComp.color = TextMain;
            textComp.text = text;
            if (text == "EXIT") textComp.alignment = TextAlignmentOptions.Center;
        }

        Button btn = go.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.40f, 0.20f, 0.18f, 1f);
            cb.pressedColor = new Color(0.62f, 0.28f, 0.24f, 1f);
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
        }
    }
}
