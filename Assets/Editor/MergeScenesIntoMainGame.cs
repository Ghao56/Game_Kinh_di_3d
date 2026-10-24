using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MergeScenesIntoMainGame
{
    const string MainGamePath = "Assets/Scenes/MAIN_GAME/MainGame.unity";
    static readonly string[] SourcePaths =
    {
        "Assets/Scenes/khang.unity",
        "Assets/Scenes/taann.unity",
        "Assets/Scenes/MHoang.unity",
    };

    [MenuItem("Tools/Merge Scenes Into MainGame")]
    public static void Merge()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var main = EditorSceneManager.OpenScene(MainGamePath, OpenSceneMode.Single);
        int moved = 0;

        foreach (var path in SourcePaths)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[MergeScenes] Missing: {path}");
                continue;
            }

            var src = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var roots = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < src.GetRootGameObjects().Length; i++)
                roots.Add(src.GetRootGameObjects()[i]);

            foreach (var root in roots)
            {
                Undo.MoveGameObjectToScene(root, main, "Merge Scenes");
                moved++;
            }

            EditorSceneManager.CloseScene(src, true);
            Debug.Log($"[MergeScenes] Merged {roots.Count} roots from {path}");
        }

        EditorSceneManager.MarkSceneDirty(main);
        EditorSceneManager.SaveScene(main);
        Debug.Log($"[MergeScenes] Done. Moved {moved} root objects into MainGame.unity");
    }
}
