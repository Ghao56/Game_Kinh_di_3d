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
    public string optionsButtonName = "Setting";
    public string quitButtonName = "Quit";

    [Header("Dải sáng sau chữ (Gradient Highlight Bar)")]
    [Tooltip("Độ đậm của dải sáng khi chuột KHÔNG nằm trên nút (0 - mờ nhất, 1 - đậm nhất)")]
    [Range(0f, 1f)] public float glowRestAlpha = 0.20f;
    [Tooltip("Độ đậm của dải sáng khi HOVER nút")]
    [Range(0f, 1f)] public float glowHoverAlpha = 0.45f;
    [Tooltip("Màu dải sáng. Nên để trắng-xám, alpha trong màu <= 1")]
    public Color glowColor = Color.white;

    private UIDocument document;

    private Button startButton;
    private Button optionsButton;
    private Button quitButton;

    private Texture2D glowRestTexture;
    private Texture2D glowHoverTexture;

    void OnEnable()
    {
        document = GetComponent<UIDocument>();

        VisualElement root = document.rootVisualElement;

        startButton = root.Q<Button>(startButtonName);
        optionsButton = root.Q<Button>(optionsButtonName);
        quitButton = root.Q<Button>(quitButtonName);

        if (startButton != null) startButton.clicked += OnStartClicked;
        else Debug.LogWarning($"Không tìm thấy Button tên '{startButtonName}' trong UXML!");

        if (optionsButton != null) optionsButton.clicked += OnOptionsClicked;
        else Debug.LogWarning($"Không tìm thấy Button tên '{optionsButtonName}' trong UXML!");

        if (quitButton != null) quitButton.clicked += OnQuitClicked;
        else Debug.LogWarning($"Không tìm thấy Button tên '{quitButtonName}' trong UXML!");

        SetupGlow(root);
    }

    void OnDisable()
    {
        // Gỡ đăng ký khi object tắt/huỷ, tránh lỗi gọi vào object đã không còn
        if (startButton != null) startButton.clicked -= OnStartClicked;
        if (optionsButton != null) optionsButton.clicked -= OnOptionsClicked;
        if (quitButton != null) quitButton.clicked -= OnQuitClicked;

        if (glowRestTexture != null) { if (Application.isPlaying) Destroy(glowRestTexture); else DestroyImmediate(glowRestTexture); }
        if (glowHoverTexture != null) { if (Application.isPlaying) Destroy(glowHoverTexture); else DestroyImmediate(glowHoverTexture); }
        glowRestTexture = null;
        glowHoverTexture = null;
    }

    // === Gradient Highlight Bar ===
    // Vẽ một texture gradient ngang (trong suốt ở hai đầu, mềm ở trên/dưới)
    // rồi đặt làm background của nút, đứng NGAY SAU chữ → tạo dải sáng sau chữ.

    void SetupGlow(VisualElement root)
    {
        glowRestTexture = CreateGlowTexture(glowRestAlpha, softness: 0.32f);
        glowHoverTexture = CreateGlowTexture(glowHoverAlpha, softness: 0.45f);

        // Áp cho mọi nút có class "menu-button" trong UXML (đã gán trong Menu.uxml)
        root.Query<Button>(className: "menu-button").ForEach(ApplyGlow);
    }

    void ApplyGlow(Button button)
    {
        // Background mặc định đã tự kéo dãn phủ nút, chỉ cần gán texture
        SetGlowTexture(button, glowRestTexture);

        // Hover: quầng sáng to hơn + đậm hơn
        button.RegisterCallback<MouseEnterEvent>(_ => SetGlowTexture(button, glowHoverTexture));
        button.RegisterCallback<MouseLeaveEvent>(_ => SetGlowTexture(button, glowRestTexture));
    }

    void SetGlowTexture(VisualElement button, Texture2D texture)
    {
        button.style.backgroundImage = new StyleBackground(texture);
    }

    Texture2D CreateGlowTexture(float peakAlpha, float softness)
    {
        const int width = 256;
        const int height = 72;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, linear: true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color[width * height];
        float horizontalFadeWidth = width * 0.35f; // khoảng cách mờ dần về hai đầu ngang

        for (int y = 0; y < height; y++)
        {
            // Mềm dần theo chiều dọc (hình chuông Gaussian) → không có viền cứng trên/dưới
            float dy = (y - (height - 1) * 0.5f) / (height * 0.5f); // -1 .. 1
            float vertical = Mathf.Exp(-dy * dy / (2f * softness * softness));

            for (int x = 0; x < width; x++)
            {
                // Mờ dần về hai đầu ngang → kiểu Gradient Highlight Bar
                float distanceToEdge = Mathf.Min(x, width - 1 - x);
                float horizontal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distanceToEdge / horizontalFadeWidth));

                float alpha = glowColor.a * peakAlpha * vertical * horizontal;
                pixels[y * width + x] = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    void OnStartClicked()
    {
        // Anti-lag freeze: nếu player mở Setting trong menu (SettingsMenu pauseWhileOpen
        // đặt timeScale = 0) rồi bấm Start mà không đóng panel, timeScale=0 sẽ kéo sang
        // scene game khiến nhân vật đứng hình + camera không xoay (deltaTime = 0).
        Time.timeScale = 1f;

        SceneManager.LoadScene(gameSceneName);
    }

    void OnOptionsClicked()
    {
        Debug.Log("[MainMenu] Setting clicked → tìm SettingsMenu...");

        var settings = FindFirstObjectByType<SettingsMenu>(FindObjectsInactive.Include);
        if (settings != null)
        {
            Debug.Log($"[MainMenu] Tìm thấy '{settings.gameObject.name}' → Toggle()");
            settings.Toggle();
        }
        else
        {
            Debug.LogWarning("Setting - chưa thấy SettingsPanel.prefab trong scene! Kéo prefab vào scene để dùng.");
        }
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