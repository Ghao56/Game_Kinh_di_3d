using UnityEditor;
using UnityEngine;

/// Chạy đúng thứ tự: font -> UI -> dữ liệu thử -> kiểm tra.
public static class DialogueSetupAll
{
    [MenuItem("Tools/Setup Dialogue")]
    public static void Run()
    {
        Debug.Log("[DialogueSetupAll] 1/4 Font...");
        DialogueFontSetup.Run();

        Debug.Log("[DialogueSetupAll] 2/4 UI trong MHoang...");
        DialogueUISetup.Run();

        Debug.Log("[DialogueSetupAll] 3/4 Dữ liệu và trigger thử...");
        DialogueTestSetup.Run();

        Debug.Log("[DialogueSetupAll] 4/4 Kiểm tra...");
        DialogueValidate.Run();
    }
}
