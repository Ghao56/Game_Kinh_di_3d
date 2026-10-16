using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

// Gắn script này vào object có component UIDocument (object đang chứa UXML menu).
// Các field tên nút dưới đây phải khớp CHÍNH XÁC với thuộc tính "Name" của từng
// Button trong UI Builder / file UXML (không phải text hiển thị trên nút,
// mà là ô "Name" trong Inspector bên UI Builder).
[RequireComponent(typeof(UIDocument))]
public class MainMenu : MonoBehaviour
{
    [Header("Tên scene")]
    public string gameSceneName = "khang";

    [Header("Tên (Name) của từng Button trong UXML")]
    public string startButtonName = "Start";
    public string continueButtonName = "Continue";
    public string optionsButtonName = "Options";
    public string quitButtonName = "Quit";

    private UIDocument document;

    private Button startButton;
    private Button continueButton;
    private Button optionsButton;
    private Button quitButton;

    void OnEnable()
    {
        document = GetComponent<UIDocument>();

        VisualElement root = document.rootVisualElement;

        startButton = root.Q<Button>(startButtonName);
        continueButton = root.Q<Button>(continueButtonName);
        optionsButton = root.Q<Button>(optionsButtonName);
        quitButton = root.Q<Button>(quitButtonName);

        if (startButton != null) startButton.clicked += OnStartClicked;
        else Debug.LogWarning($"Không tìm thấy Button tên '{startButtonName}' trong UXML!");

        if (continueButton != null) continueButton.clicked += OnContinueClicked;
        else Debug.LogWarning($"Không tìm thấy Button tên '{continueButtonName}' trong UXML!");

        if (optionsButton != null) optionsButton.clicked += OnOptionsClicked;
        else Debug.LogWarning($"Không tìm thấy Button tên '{optionsButtonName}' trong UXML!");

        if (quitButton != null) quitButton.clicked += OnQuitClicked;
        else Debug.LogWarning($"Không tìm thấy Button tên '{quitButtonName}' trong UXML!");
    }

    void OnDisable()
    {
        // Gỡ đăng ký khi object tắt/huỷ, tránh lỗi gọi vào object đã không còn
        if (startButton != null) startButton.clicked -= OnStartClicked;
        if (continueButton != null) continueButton.clicked -= OnContinueClicked;
        if (optionsButton != null) optionsButton.clicked -= OnOptionsClicked;
        if (quitButton != null) quitButton.clicked -= OnQuitClicked;
    }

    void OnStartClicked()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    void OnContinueClicked()
    {
        // TODO: load save game (sẽ làm sau)
        Debug.Log("Continue - chưa làm, sẽ bổ sung sau.");
    }

    void OnOptionsClicked()
    {
        // TODO: mở panel Options (sẽ làm sau)
        Debug.Log("Options - chưa làm, sẽ bổ sung sau.");
    }

    void OnQuitClicked()
    {
        Debug.Log("Thoát game...");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}