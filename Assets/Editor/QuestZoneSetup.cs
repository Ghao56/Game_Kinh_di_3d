using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Dựng sẵn các tấm trigger trong scene: một tấm đỏ bắt đầu explore_house, N tấm xanh
/// cộng điểm mục tiêu 'explore' (mỗi tấm một uniqueKey nên chỉ tính 1 lần), một tấm đỏ
/// bắt đầu mother_care ở ngưỡng cửa phòng mẹ, cùng 3 vật tương tác cho 3 mục tiêu của
/// mother_care. Scene chưa có địa hình phòng nên mọi vật thể được đặt cạnh nhân vật;
/// chuyển component sang vật thật rồi xoá placeholder sau.
/// Required của explore_house sống trong asset, tool không ghi đè giá trị đó.
public static class QuestZoneSetup
{
    private const string ScenePath = "Assets/Scenes/MHoang.unity";
    private const string ZoneParentName = "Quest Zones";
    private const string InteractableParentName = "Quest Interactables";

    private const string MarkerFolder = "Assets/Quests";
    private const string PadMarkerPath = MarkerFolder + "/QuestZone_Start.mat";
    private const string ReportMarkerPath = MarkerFolder + "/QuestZone_Report.mat";
    private const string LegacyExploreMarkerPath = MarkerFolder + "/QuestZone_Explore.mat";

    private const string ExploreQuestId = "explore_house";
    private const string ExploreObjectiveId = "explore";
    private const string MotherQuestId = "mother_care";

    private const float ZoneSpacing = 6f;
    private const float ZoneStartOffset = 3f;
    private const float InteractableSpacing = 1.2f;

    /// Số zone "phòng" tạo sẵn cho mục tiêu khám phá. Mỗi phòng một uniqueKey riêng,
    /// nên Required của explore_house có thể chạy 1-3 tuỳ số phòng thật đưa vào.
    private const int ExploreRoomCount = 3;
    private static readonly Vector2[] ExploreRoomOffsets =
    {
        new Vector2(0f, -4f),
        new Vector2(-4.5f, -4.5f),
        new Vector2(-4.5f, 3.5f)
    };

    /// Cao thực tế của vùng trigger. Visual vẫn là tấm mỏng nằm sàn, chỉ collider được
    /// kéo cao để không bị lọt qua khi capsule của CharacterController lơ lửng sát mặt sàn.
    private const float TriggerHeight = 1.6f;

    private static readonly Color PadColor = new Color(1f, 0.15f, 0.15f, 0.35f);
    private static readonly Color ReportColor = new Color(0.2f, 0.9f, 0.4f, 0.35f);

    private static readonly Vector3 ExplorePadSize = new Vector3(2.4f, 0.08f, 2.4f);
    private static readonly Vector3 MotherPadSize = new Vector3(1.8f, 0.08f, 0.5f);

    private struct ZoneData
    {
        public string Name;
        public QuestZone.ZoneMode Mode;
        public string QuestId;
        public string RequiresCompletedQuestId;

        /// Mode ReportObjective: mục tiêu được cộng khi bước vào zone này.
        public string ObjectiveId;

        /// Khoá đếm một lần. Để trống thì QuestZone tự lấy tên GameObject.
        public string UniqueKey;

        public bool ReportObjectiveOnStart;

        /// Vị trí zone so với nhân vật (X, Z).
        public Vector3 Offset;
        public Vector3 Size;
    }

    private struct InteractableData
    {
        public string Name;
        public string ObjectiveId;
        public string PromptText;
        public Vector3 Size;
        public bool HideAfterUse;
    }

    [MenuItem("Tools/Setup Quest Zones")]
    public static void SetupZones()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[QuestZoneSetup] Dừng Play mode trước khi chạy Setup.");
            return;
        }

        OpenSceneIfNeeded();
        RemoveLegacyZones();

        Material padMaterial = GetMarkerMaterial(PadMarkerPath, PadColor);
        Material reportMaterial = GetMarkerMaterial(ReportMarkerPath, ReportColor);

        Vector3 playerPosition = GetPlayerPosition();

        Transform zoneParent = EnsureParent(ZoneParentName);
        ZoneData[] zones = BuildZones();

        for (int i = 0; i < zones.Length; i++)
        {
            Vector3 position = playerPosition + new Vector3(zones[i].Offset.x, 0f, zones[i].Offset.z);
            Material material = zones[i].Mode == QuestZone.ZoneMode.ReportObjective ? reportMaterial : padMaterial;
            EnsureZone(zoneParent, zones[i], material, position);
        }

        Transform interactableParent = EnsureParent(InteractableParentName);
        InteractableData[] interactables =
        {
            new InteractableData
            {
                Name = "PL_CocNuoc", ObjectiveId = "get_water", PromptText = "Lấy nước cho mẹ",
                Size = new Vector3(0.18f, 0.24f, 0.18f), HideAfterUse = true
            },
            new InteractableData
            {
                Name = "PL_HopThuoc", ObjectiveId = "prepare_medicine", PromptText = "Lấy thuốc",
                Size = new Vector3(0.3f, 0.16f, 0.2f), HideAfterUse = true
            },
            new InteractableData
            {
                Name = "PL_CuaSoPhongMe", ObjectiveId = "check_window", PromptText = "Kiểm tra cửa sổ",
                Size = new Vector3(1.2f, 1.2f, 0.12f), HideAfterUse = false
            }
        };

        for (int i = 0; i < interactables.Length; i++)
        {
            Vector3 position = playerPosition + new Vector3(ZoneStartOffset, 0f, -InteractableSpacing * (i + 1));
            EnsureInteractable(interactableParent, interactables[i], position);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (Application.isBatchMode)
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Selection.activeGameObject = zoneParent.gameObject;
        Debug.Log($"[QuestZoneSetup] Xong {zones.Length} zone: Pad_ExploreHouse (nhận quest explore_house) + " +
                  $"{ExploreRoomCount} pad xanh Pad_Phong* (mỗi phòng +1 điểm 'explore') + Pad_MotherCare " +
                  "+ 3 QuestObjectiveInteractable. " +
                  $"Required của 'explore' đang là {ReadExploreRequired()}: nếu Required = 1 thì chạm 1 pad xanh là xong, " +
                  "nếu = số pad xanh thì phải đi hết. Kéo pad về vị trí thật; 3 vật PL_* là placeholder, " +
                  "chuyển component sang vật thật rồi xoá placeholder.");
    }

    /// Pad đỏ chỉ nhận quest; các pad xanh chỉ cộng điểm mục tiêu explore.
    /// Tách hai việc để quest không vừa hiện vừa xong trong cùng một frame.
    private static ZoneData[] BuildZones()
    {
        List<ZoneData> zones = new List<ZoneData>
        {
            new ZoneData
            {
                Name = "Pad_ExploreHouse",
                Mode = QuestZone.ZoneMode.StartQuest,
                QuestId = ExploreQuestId,
                Offset = new Vector3(ZoneStartOffset, 0f, 0f),
                Size = ExplorePadSize
            }
        };

        for (int i = 0; i < ExploreRoomCount && i < ExploreRoomOffsets.Length; i++)
        {
            zones.Add(new ZoneData
            {
                Name = "Pad_Phong" + (i + 1),
                Mode = QuestZone.ZoneMode.ReportObjective,
                QuestId = ExploreQuestId,
                ObjectiveId = ExploreObjectiveId,
                UniqueKey = "phong" + (i + 1),
                Offset = new Vector3(ExploreRoomOffsets[i].x, 0f, ExploreRoomOffsets[i].y),
                Size = ExplorePadSize
            });
        }

        zones.Add(new ZoneData
        {
            Name = "Pad_MotherCare",
            Mode = QuestZone.ZoneMode.StartQuest,
            QuestId = MotherQuestId,
            RequiresCompletedQuestId = ExploreQuestId,
            Offset = new Vector3(ZoneStartOffset + ZoneSpacing, 0f, 0f),
            Size = MotherPadSize
        });

        return zones.ToArray();
    }

    /// Required đang lưu trong asset explore_house, để log cho biết cần đi bao nhiêu phòng.
    private static int ReadExploreRequired()
    {
        const string path = "Assets/Quests/Quest_ExploreHouse.asset";
        QuestDefinition definition = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        if (definition == null) return -1;

        for (int i = 0; i < definition.Objectives.Count; i++)
        {
            if (definition.Objectives[i].Id == ExploreObjectiveId) return definition.Objectives[i].Required;
        }

        return -1;
    }

    /// Xoá các zone cũ dạng Zone_* (bản 4 vùng phòng + tấm cửa) và material xanh không còn dùng.
    private static void RemoveLegacyZones()
    {
        GameObject parent = GameObject.Find(ZoneParentName);
        if (parent != null)
        {
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < parent.transform.childCount; i++)
                children.Add(parent.transform.GetChild(i));

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].GetComponent<QuestZone>() == null) continue;
                if (!children[i].name.StartsWith("Zone_")) continue;

                Undo.DestroyObjectImmediate(children[i].gameObject);
            }
        }

        if (AssetDatabase.LoadAssetAtPath<Material>(LegacyExploreMarkerPath) != null)
        {
            AssetDatabase.DeleteAsset(LegacyExploreMarkerPath);
            AssetDatabase.SaveAssets();
        }
    }

    private static void OpenSceneIfNeeded()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == ScenePath) return;

        if (!string.IsNullOrEmpty(activeScene.path) || activeScene.rootCount > 0)
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static Transform EnsureParent(string name)
    {
        List<GameObject> roots = new List<GameObject>();
        SceneManager.GetActiveScene().GetRootGameObjects(roots);

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i].name == name) return roots[i].transform;
        }

        GameObject parentGO = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(parentGO, "Create " + name);
        return parentGO.transform;
    }

    private static Vector3 GetPlayerPosition()
    {
        ThirdPersonController controller = Object.FindFirstObjectByType<ThirdPersonController>();
        Vector3 position;
        if (controller != null)
        {
            position = controller.transform.position;
        }
        else
        {
            GameObject player = GameObject.Find("Player");
            position = player != null ? player.transform.position : Vector3.zero;
            Debug.LogWarning("[QuestZoneSetup] Không tìm thấy ThirdPersonController, tạm dùng GameObject 'Player'.");
        }

        Physics.SyncTransforms();
        RaycastHit[] hits = Physics.RaycastAll(
            position + Vector3.up * 5f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.GetComponentInParent<ThirdPersonController>() != null) continue;
            position.y = hits[i].point.y;
            return position;
        }

        position.y = 0f;
        return position;
    }

    private static GameObject EnsureZone(Transform parent, ZoneData data, Material material, Vector3 position)
    {
        Transform existing = parent.Find(data.Name);
        GameObject zoneGO;
        if (existing != null)
        {
            zoneGO = existing.gameObject;
        }
        else
        {
            zoneGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zoneGO.name = data.Name;
            Undo.RegisterCreatedObjectUndo(zoneGO, "Create " + data.Name);
            zoneGO.transform.SetParent(parent, false);
        }

        BoxCollider box = zoneGO.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(zoneGO);
        box.isTrigger = true;
        box.size = new Vector3(1f, TriggerHeight / data.Size.y, 1f);
        box.center = new Vector3(0f, (TriggerHeight * 0.5f - data.Size.y * 0.5f) / data.Size.y, 0f);

        QuestZone zone = zoneGO.GetComponent<QuestZone>();
        if (zone == null) zone = Undo.AddComponent<QuestZone>(zoneGO);

        Renderer zoneRenderer = zoneGO.GetComponent<Renderer>();
        zoneRenderer.sharedMaterial = material;
        zoneRenderer.shadowCastingMode = ShadowCastingMode.Off;
        zoneRenderer.receiveShadows = false;

        Transform zoneTransform = zoneGO.transform;
        zoneTransform.position = new Vector3(position.x, position.y + data.Size.y * 0.5f, position.z);
        zoneTransform.rotation = Quaternion.identity;
        zoneTransform.localScale = data.Size;

        ConfigureZone(zone, data);
        return zoneGO;
    }

    private static void ConfigureZone(QuestZone zone, ZoneData data)
    {
        SerializedObject so = new SerializedObject(zone);
        so.FindProperty("mode").enumValueIndex = (int)data.Mode;
        so.FindProperty("questId").stringValue = data.QuestId;
        so.FindProperty("questToStart").objectReferenceValue = null;
        so.FindProperty("requiresCompletedQuestId").stringValue = data.RequiresCompletedQuestId;
        so.FindProperty("objectiveId").stringValue = data.ObjectiveId;
        so.FindProperty("uniqueKey").stringValue = data.UniqueKey;
        so.FindProperty("reportObjectiveOnStart").boolValue = data.ReportObjectiveOnStart;
        so.FindProperty("fireOnce").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject EnsureInteractable(Transform parent, InteractableData data, Vector3 position)
    {
        Transform existing = parent.Find(data.Name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = data.Name;
            Undo.RegisterCreatedObjectUndo(go, "Create " + data.Name);
            go.transform.SetParent(parent, false);
        }

        BoxCollider box = go.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(go);
        box.isTrigger = false;
        box.center = Vector3.zero;
        box.size = Vector3.one;

        QuestObjectiveInteractable interactable = go.GetComponent<QuestObjectiveInteractable>();
        if (interactable == null) interactable = Undo.AddComponent<QuestObjectiveInteractable>(go);
        if (go.GetComponent<InteractableGlow>() == null) Undo.AddComponent<InteractableGlow>(go);

        Transform goTransform = go.transform;
        goTransform.position = new Vector3(position.x, position.y + data.Size.y * 0.5f, position.z);
        goTransform.rotation = Quaternion.identity;
        goTransform.localScale = data.Size;

        SerializedObject so = new SerializedObject(interactable);
        so.FindProperty("promptText").stringValue = data.PromptText;
        so.FindProperty("holdToInteract").boolValue = false;
        so.FindProperty("holdDuration").floatValue = 0.5f;
        so.FindProperty("questId").stringValue = MotherQuestId;
        so.FindProperty("objectiveId").stringValue = data.ObjectiveId;
        so.FindProperty("hideAfterUse").boolValue = data.HideAfterUse;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    private static Material GetMarkerMaterial(string path, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MarkerFolder))
            AssetDatabase.CreateFolder("Assets", "Quests");

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("[QuestZoneSetup] Không tìm thấy shader để tạo marker.", null);
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);

        return material;
    }
}