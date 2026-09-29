using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class LightingDiagnosticsRunner
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Chay chan doan anh sang (batchmode)")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError($"[{nameof(LightingDiagnosticsRunner)}] Đang ở Play mode, hãy thoát trước.");
            EditorApplication.Exit(2);
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var host = new GameObject("__LightingDiagnostics__");
        LightingDiagnostics diag = host.AddComponent<LightingDiagnostics>();
        diag.SetAutoStart(true);

        Debug.Log($"[{nameof(LightingDiagnosticsRunner)}] Vao Play mode de do...");

        EditorApplication.EnterPlaymode();
    }
}
