using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Áp độ nhạy chuột (GameSettings.MouseSensitivity, chỉnh bằng slider trong Setting)
/// vào CinemachineInputAxisController của camera.
/// Gắn script này cùng GameObject với CinemachineInputAxisController
/// (ví dụ "CM ThirdPersonCamera").
///
/// Cách tính: Gain_mới = Gain_gốc × sens (sens = 1 → giữ nguyên như trước,
/// sens = 0.5 → chậm một nửa). Chỉ scale các axis có tên bắt đầu bằng tiền tố
/// "Look" (Look Orbit X/Y) để không đụng vào zoom bằng scroll (Orbit Scale).
/// Trước đây slider chỉ ghi PlayerPrefs mà không script nào đọc ra → cảm giác
/// "không hoạt động". Script này là chỗ áp dụng.
/// </summary>
[DisallowMultipleComponent]
public class MouseSensitivityApplier : MonoBehaviour
{
    [Tooltip("Bỏ trống để tự lấy CinemachineInputAxisController trên cùng object")]
    [SerializeField] private CinemachineInputAxisController controller;

    [Tooltip("Chỉ scale các axis có tên bắt đầu bằng tiền tố này. Để trống = áp cho tất cả")]
    [SerializeField] private string axisNamePrefix = "Look";

    private readonly Dictionary<string, float> baseGains = new Dictionary<string, float>();
    private float lastAppliedSensitivity = float.MinValue;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<CinemachineInputAxisController>();
    }

    private void Update()
    {
        float sens = GameSettings.MouseSensitivity;
        if (Mathf.Approximately(sens, lastAppliedSensitivity)) return; // tránh áp mỗi frame

        // Capture gain gốc lần đầu (danh sách Controllers tạo lần đầu khi chạy)
        if (baseGains.Count == 0 && CaptureBaseGains()) lastAppliedSensitivity = sens; // áp với sens hiện tại
        else if (baseGains.Count > 0) lastAppliedSensitivity = sens;

        Apply(sens);
    }

    private bool CaptureBaseGains()
    {
        if (controller == null) controller = GetComponent<CinemachineInputAxisController>();
        if (controller == null) return false;

        var controllers = controller.Controllers;
        if (controllers == null || controllers.Count == 0) return false;

        baseGains.Clear();
        foreach (var c in controllers)
            baseGains[c.Name] = c.Input.Gain;

        return true;
    }

    private void Apply(float sens)
    {
        if (controller == null || baseGains.Count == 0) return;

        foreach (var c in controller.Controllers)
        {
            if (!string.IsNullOrEmpty(axisNamePrefix) && !c.Name.StartsWith(axisNamePrefix)) continue;

            float baseGain;
            if (!baseGains.TryGetValue(c.Name, out baseGain)) continue;

            var input = c.Input;
            input.Gain = baseGain * sens;
            c.Input = input;
        }
    }
}
