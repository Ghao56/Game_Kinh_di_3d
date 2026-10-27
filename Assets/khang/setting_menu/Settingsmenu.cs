using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menu Settings đơn giản: Âm thanh + Đồ họa + Mouse Sensitivity.
/// Gắn script này vào một GameObject LUÔN ACTIVE (ví dụ SettingsManager),
/// KHÔNG gắn vào Canvas Settings (vì Canvas đó bị tắt lúc đầu).
/// Nhấn phím Toggle Key (mặc định Tab) hoặc nút Settings để mở/đóng.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Kéo Canvas Settings vào đây (canvas sẽ được tắt lúc bắt đầu)")]
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("Phím bật/tắt menu")]
    [SerializeField] private Key toggleKey = Key.Tab;
    [Tooltip("Dừng game (timeScale = 0) và hiện chuột khi mở Settings")]
    [SerializeField] private bool pauseWhileOpen = true;
    [Tooltip("Khóa lại chuột khi đóng Settings (bật nếu đang trong gameplay FPS/TPS)")]
    [SerializeField] private bool lockCursorOnClose = false;
    [Tooltip("Các Canvas khác (HUD, crosshair...) sẽ bị ẩn khi mở Settings")]
    [SerializeField] private GameObject[] otherCanvases;
    [Tooltip("Tùy chọn: script xoay camera / Cinemachine Input Axis Controller, sẽ bị tắt khi mở menu")]
    [SerializeField] private Behaviour cameraInputController;

    [Header("Audio")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Graphics")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("Gameplay")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValueText; // tùy chọn, có thể để trống

    // Danh sách độ phân giải (đã lọc trùng) khớp với các option của dropdown
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();

    // Trạng thái các canvas khác trước khi mở menu
    private bool[] otherCanvasStates;

    private void Awake()
    {
        SetupResolutionDropdown();
        SetupQualityDropdown();
    }

    private void Start()
    {
        // Gắn sự kiện bằng code, không cần kéo thả OnValueChanged trong Inspector
        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        qualityDropdown.onValueChanged.AddListener(SetQuality);
        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);

        LoadSettings();

        // Luôn ẩn menu lúc bắt đầu
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
            Toggle();
    }

    // ───────────────────────── Mở / đóng ─────────────────────────

    public void Toggle()
    {
        if (settingsPanel.activeSelf) Close();
        else Open();
    }

    public void Open()
    {
        if (settingsPanel.activeSelf) return; // tránh mở 2 lần làm mất trạng thái canvas

        // Ghi nhớ trạng thái rồi tắt các canvas khác
        otherCanvasStates = new bool[otherCanvases.Length];
        for (int i = 0; i < otherCanvases.Length; i++)
        {
            if (otherCanvases[i] == null) continue;
            otherCanvasStates[i] = otherCanvases[i].activeSelf;
            otherCanvases[i].SetActive(false);
        }

        if (cameraInputController != null) cameraInputController.enabled = false;

        settingsPanel.SetActive(true);

        if (pauseWhileOpen)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void Close()
    {
        if (!settingsPanel.activeSelf) return;

        PlayerPrefs.Save();
        settingsPanel.SetActive(false);

        // Trả các canvas khác về trạng thái trước khi mở
        if (otherCanvasStates != null)
        {
            for (int i = 0; i < otherCanvases.Length; i++)
            {
                if (otherCanvases[i] == null) continue;
                otherCanvases[i].SetActive(otherCanvasStates[i]);
            }
        }

        if (cameraInputController != null) cameraInputController.enabled = true;

        if (pauseWhileOpen) Time.timeScale = 1f;

        if (lockCursorOnClose)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ───────────────────────── Audio ─────────────────────────

    public void SetMasterVolume(float value) => ApplyVolume(GameSettings.MasterParam, value);
    public void SetMusicVolume(float value) => ApplyVolume(GameSettings.MusicParam, value);
    public void SetSfxVolume(float value) => ApplyVolume(GameSettings.SfxParam, value);

    private void ApplyVolume(string exposedParam, float sliderValue)
    {
        GameSettings.ApplyVolume(audioMixer, exposedParam, sliderValue);
        PlayerPrefs.SetFloat(exposedParam, sliderValue);
    }

    // ───────────────────────── Graphics ─────────────────────────

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreenMode = isFullscreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        PlayerPrefs.SetInt(GameSettings.FullscreenKey, isFullscreen ? 1 : 0);
    }

    public void SetResolution(int index)
    {
        if (index < 0 || index >= resolutions.Count) return;

        Vector2Int res = resolutions[index];
        Screen.SetResolution(res.x, res.y, Screen.fullScreenMode);

        PlayerPrefs.SetInt(GameSettings.ResWidthKey, res.x);
        PlayerPrefs.SetInt(GameSettings.ResHeightKey, res.y);
    }

    public void SetQuality(int index)
    {
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt(GameSettings.QualityKey, index);
    }

    // ───────────────────────── Gameplay ─────────────────────────

    public void SetSensitivity(float value)
    {
        PlayerPrefs.SetFloat(GameSettings.SensitivityKey, value);
        UpdateSensitivityText(value);
    }

    private void UpdateSensitivityText(float value)
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = value.ToString("0.0");
    }

    // ───────────────────────── Setup dropdown ─────────────────────────

    private void SetupResolutionDropdown()
    {
        resolutions.Clear();
        var options = new List<string>();

        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (resolutions.Contains(size)) continue; // bỏ trùng (khác tần số quét)

            resolutions.Add(size);
            options.Add($"{r.width} x {r.height}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
    }

    private void SetupQualityDropdown()
    {
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
    }

    // ───────────────────────── Load ─────────────────────────

    private void LoadSettings()
    {
        // Âm thanh: SetValueWithoutNotify để không kích hoạt lưu lại, rồi áp dụng thủ công
        float master = PlayerPrefs.GetFloat(GameSettings.MasterParam, 1f);
        float music = PlayerPrefs.GetFloat(GameSettings.MusicParam, 1f);
        float sfx = PlayerPrefs.GetFloat(GameSettings.SfxParam, 1f);

        masterSlider.SetValueWithoutNotify(master);
        musicSlider.SetValueWithoutNotify(music);
        sfxSlider.SetValueWithoutNotify(sfx);

        GameSettings.ApplyVolume(audioMixer, GameSettings.MasterParam, master);
        GameSettings.ApplyVolume(audioMixer, GameSettings.MusicParam, music);
        GameSettings.ApplyVolume(audioMixer, GameSettings.SfxParam, sfx);

        // Fullscreen
        bool fullscreen = PlayerPrefs.GetInt(GameSettings.FullscreenKey, 1) == 1;
        fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        // Quality
        int quality = PlayerPrefs.GetInt(GameSettings.QualityKey, QualitySettings.GetQualityLevel());
        quality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);
        qualityDropdown.SetValueWithoutNotify(quality);
        QualitySettings.SetQualityLevel(quality);

        // Resolution: nếu chưa lưu thì dùng độ phân giải hiện tại
        int width = PlayerPrefs.GetInt(GameSettings.ResWidthKey, Screen.currentResolution.width);
        int height = PlayerPrefs.GetInt(GameSettings.ResHeightKey, Screen.currentResolution.height);

        int resIndex = resolutions.IndexOf(new Vector2Int(width, height));
        if (resIndex < 0) resIndex = resolutions.Count - 1; // không tìm thấy -> chọn độ phân giải cao nhất

        resolutionDropdown.SetValueWithoutNotify(resIndex);
        resolutionDropdown.RefreshShownValue();
        Screen.SetResolution(resolutions[resIndex].x, resolutions[resIndex].y, Screen.fullScreenMode);

        // Sensitivity
        float sens = GameSettings.MouseSensitivity;
        sensitivitySlider.SetValueWithoutNotify(sens);
        UpdateSensitivityText(sens);
    }
}

/// <summary>
/// Nơi tập trung key lưu trữ + hàm tiện ích. Script khác (ví dụ PlayerLook)
/// chỉ cần gọi GameSettings.MouseSensitivity, không phụ thuộc vào menu.
/// </summary>
public static class GameSettings
{
    // Tên PHẢI trùng với Exposed Parameter trong AudioMixer
    public const string MasterParam = "MasterVolume";
    public const string MusicParam = "MusicVolume";
    public const string SfxParam = "SFXVolume";

    public const string FullscreenKey = "Fullscreen";
    public const string ResWidthKey = "ResWidth";
    public const string ResHeightKey = "ResHeight";
    public const string QualityKey = "Quality";
    public const string SensitivityKey = "MouseSensitivity";

    /// <summary>Độ nhạy chuột, mặc định 1. Nhân vào look delta trong script camera.</summary>
    public static float MouseSensitivity => PlayerPrefs.GetFloat(SensitivityKey, 1f);

    /// <summary>Đổi giá trị slider (0.0001–1) sang dB rồi gán vào AudioMixer.</summary>
    public static void ApplyVolume(AudioMixer mixer, string exposedParam, float sliderValue)
    {
        float clamped = Mathf.Clamp(sliderValue, 0.0001f, 1f);
        mixer.SetFloat(exposedParam, Mathf.Log10(clamped) * 20f);
    }
}