using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Tạo DialogueData mẫu và một DialogueTrigger thử ngay cạnh người chơi trong MHoang.
public static class DialogueTestSetup
{
    private const string DataFolder = "Assets/Dialogue";
    private const string DataPath = DataFolder + "/Dialogue_Test.asset";
    private const string TriggerName = "DialogueTrigger_Test";

    [MenuItem("Tools/Setup Dialogue Test")]
    public static void Run()
    {
        DialogueFontSetup.EnsureFolder(DataFolder);
        CreateOrUpdateData();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[DialogueTestSetup] Hãy mở scene đã lưu (vd TestCutscreen.unity) trước khi chạy.");
            return;
        }

        var player = Object.FindFirstObjectByType<ThirdPersonController>();
        if (player == null)
        {
            Debug.LogError("[DialogueTestSetup] Không tìm thấy ThirdPersonController trong scene.");
            return;
        }

        var existing = GameObject.Find(TriggerName);
        GameObject trigger;
        if (existing != null)
        {
            trigger = existing;
        }
        else
        {
            trigger = new GameObject(TriggerName);
            Undo.RegisterCreatedObjectUndo(trigger, "Create Dialogue Trigger Test");
            var box = trigger.AddComponent<BoxCollider>();
            Undo.AddComponent<DialogueTrigger>(trigger);
            box.isTrigger = true;
            box.size = new Vector3(4f, 3f, 4f);
            box.center = new Vector3(0f, 1.5f, 0f);
        }

        Vector3 pos = player.transform.position + player.transform.forward * 3f;
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 40f))
            pos = hit.point;

        trigger.transform.position = pos;

        var triggerComponent = trigger.GetComponent<DialogueTrigger>();
        var so = new SerializedObject(triggerComponent);
        SerializedProperty dialogueProp = so.FindProperty("dialogue");
        if (dialogueProp == null)
        {
            Debug.LogError("[DialogueTestSetup] DialogueTrigger không có field 'dialogue'.");
        }
        else
        {
            DialogueData live = AssetDatabase.LoadAssetAtPath<DialogueData>(DataPath);
            if (live == null)
            {
                Debug.LogError($"[DialogueTestSetup] Không load được '{DataPath}' để gán vào trigger.");
            }
            else
            {
                dialogueProp.objectReferenceValue = live;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(triggerComponent);
                Debug.Log($"[DialogueTestSetup] Gán '{live.name}' vào trigger (instance {live.GetInstanceID()}).");
            }
        }
        EditorUtility.SetDirty(trigger);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        VerifySaved(scene);

        Debug.Log($"[DialogueTestSetup] Xong. Asset='{DataPath}', trigger tại {pos} " +
                  $"đối diện người chơi, Is Trigger=bật. Xoá object '{TriggerName}' để gỡ khỏi scene.");
    }

    private static void CreateOrUpdateData()
    {
        var data = AssetDatabase.LoadAssetAtPath<DialogueData>(DataPath);
        bool created = false;
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DialogueData>();
            AssetDatabase.CreateAsset(data, DataPath);
            created = true;
        }

        data.notes = "Thoại cutscene: Vào đi… rồi *Thở dài.";
        data.lines = new[]
        {
            new DialogueLine
            {
                speaker = string.Empty,
                text = "Vào đi…",
                charDelay = 0.035f,
                sentenceDelay = 0.28f
            },
            new DialogueLine
            {
                speaker = string.Empty,
                text = "*Thở dài",
                charDelay = 0.035f,
                sentenceDelay = 0.28f
            },
        };

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DialogueTestSetup] {(created ? "Đã tạo" : "Đã cập nhật")} '{DataPath}' với {data.lines.Length} dòng.");
    }

    private static void VerifySaved(Scene scene)
    {
        EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);

        GameObject trigger = GameObject.Find(TriggerName);
        if (trigger == null)
        {
            Debug.LogError("[DialogueTestSetup] Trigger biến mất sau khi lưu scene.");
            return;
        }

        DialogueTrigger component = trigger.GetComponent<DialogueTrigger>();
        var so = new SerializedObject(component);
        SerializedProperty prop = so.FindProperty("dialogue");

        if (prop != null && prop.objectReferenceValue != null)
        {
            DialogueData saved = prop.objectReferenceValue as DialogueData;
            int lines = saved != null && saved.lines != null ? saved.lines.Length : 0;
            Debug.Log($"[DialogueTestSetup] ĐÃ LƯU OK: trigger -> '{saved.name}' với {lines} dòng.");
        }
        else
        {
            Debug.LogError("[DialogueTestSetup] LƯU THẤT BẠI: trigger.dialogue vẫn null sau khi lưu scene.");
        }
    }

    }