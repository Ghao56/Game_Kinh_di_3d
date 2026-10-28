using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Nút "Exit" trong bảng Setting ở scene game (MainGame):
/// bấm vào sẽ khôi phục timeScale, lưu cài đặt rồi quay về scene menu ("UI menu").
/// Gắn script này vào Button Exit; onClick sẽ tự được nối trong Awake nên
/// không cần kéo thả sự kiện trong Inspector.
/// </summary>
[DisallowMultipleComponent]
public class ExitToMenu : MonoBehaviour
{
    [Tooltip("Tên scene menu để quay về khi bấm Exit")]
    [SerializeField] private string menuSceneName = "UI menu";

    private void Awake()
    {
        var button = GetComponent<UnityEngine.UI.Button>();
        if (button != null) button.onClick.AddListener(ExitToMenuScene);
        else Debug.LogWarning("[ExitToMenu] Trên GameObject này không có Button!", this);
    }

    /// <summary>Đóng setting, khôi phục timeScale và Load scene menu.</summary>
    public void ExitToMenuScene()
    {
        // Chống treo nếu Setting vẫn đang mở (timeScale = 0)
        Time.timeScale = 1f;

        // Đóng panel + Save PlayerPrefs (nếu SettingsMenu đang có trong scene)
        var settings = FindFirstObjectByType<SettingsMenu>(FindObjectsInactive.Include);
        if (settings != null) settings.Close();

        SceneManager.LoadScene(menuSceneName);
    }

    private void OnDestroy()
    {
        var button = GetComponent<UnityEngine.UI.Button>();
        if (button != null) button.onClick.RemoveListener(ExitToMenuScene);
    }
}
