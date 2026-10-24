using UnityEngine;
using UnityEngine.UI;

// Vòng tiến độ giữ E: fill (Image Radial Filled) + nền (HoldRing_Background) hiện/ẩn chung.
// Interactor đang giữ E → fill tăng theo holdTimer; thả E → fill rút nhanh về 0 rồi cả hai ẩn.
public class HoldRingUI : MonoBehaviour
{
    [SerializeField] private Image fill; // để trống = Image trên chính object này
    [SerializeField] private Image background; // viền nền, ẩn/hiện cùng fill
    [SerializeField, Min(0.5f)] private float drainSpeed = 4f; // tốc độ rút khi thả E (1 = 1 vòng/giây)

    private Interactor interactor;
    private float display;

    private void Update()
    {
        if (fill == null) fill = GetComponent<Image>();
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();

        float target = interactor != null ? interactor.HoldProgress01 : 0f;
        display = target > 0f
            ? target // giữ E: đi đúng tiến độ thật
            : Mathf.MoveTowards(display, 0f, drainSpeed * Time.deltaTime); // thả E: rút nhanh

        bool visible = display > 0.001f;
        if (fill != null && fill.enabled != visible) fill.enabled = visible;
        if (background != null && background.enabled != visible) background.enabled = visible;
        if (visible && fill != null) fill.fillAmount = Mathf.Clamp01(display);
    }

    private void OnDisable()
    {
        display = 0f;
        if (background != null) background.enabled = true;
    }
}
