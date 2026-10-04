using TMPro;
using UnityEngine;

/// Hiệu ứng từng chữ bật lên rồi về đúng chỗ.
/// Gắn vào cùng GameObject với TMP_Text. DialogueManager đẩy mốc thời gian hiện từng ký tự vào đây.
/// offset được áp lại mỗi frame vì ForceMeshUpdate() dựng lại mesh và xoá sạch offset của frame trước.
[RequireComponent(typeof(TMP_Text))]
public class TMPCharJump : MonoBehaviour
{
    private const float BackC1 = 1.70158f;
    private const float BackC3 = BackC1 + 1f;

    [Header("Hiệu ứng")]
    [Tooltip("Biên độ nhảy. Nên bằng khoảng 0.3 * fontSize.")]
    [SerializeField] private float jumpHeight = 10f;

    [Tooltip("Thời gian một chữ bật lên và về chỗ (giây, không phụ thuộc timeScale).")]
    [SerializeField] private float jumpDuration = 0.14f;

    [Tooltip("Chữ mới hiện sẽ mờ dần lên. 0 = hiện thẳng không fade.")]
    [SerializeField] private bool fadeIn = true;

    private TMP_Text text;
    private float[] revealTime;
    private bool wasAnimating;

    public float JumpDuration => jumpDuration;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        jumpHeight = Mathf.Max(0f, jumpHeight);
        jumpDuration = Mathf.Max(0.001f, jumpDuration);
    }

    public void Configure(float height, float duration)
    {
        jumpHeight = Mathf.Max(0f, height);
        jumpDuration = Mathf.Max(0.001f, duration);
    }

    public void ResetRevealTimes(int count)
    {
        revealTime = new float[Mathf.Max(0, count)];
        for (int i = 0; i < revealTime.Length; i++) revealTime[i] = float.MaxValue;
        wasAnimating = false;
    }

    public void MarkRevealed(int index)
    {
        if (revealTime == null || index < 0 || index >= revealTime.Length) return;
        revealTime[index] = Time.unscaledTime;
    }

    /// Đặt mốc thời gian về quá khứ để ký tự tức thì ở đúng chỗ, không nhảy hàng loạt.
    public void SettleFrom(int firstIndex)
    {
        if (revealTime == null) return;
        float settled = Time.unscaledTime - jumpDuration;
        for (int i = Mathf.Max(0, firstIndex); i < revealTime.Length; i++) revealTime[i] = settled;
    }

    private void LateUpdate()
    {
        if (text == null || revealTime == null) return;

        int visible = Mathf.Clamp(text.maxVisibleCharacters, 0, text.textInfo.characterCount);

        bool animating = false;
        float now = Time.unscaledTime;
        for (int i = 0; i < visible; i++)
        {
            if (now - revealTime[i] < jumpDuration)
            {
                animating = true;
                break;
            }
        }

        if (!animating && !wasAnimating) return;
        wasAnimating = animating;

        text.ForceMeshUpdate();

        TMP_TextInfo info = text.textInfo;

        for (int i = 0; i < visible; i++)
        {
            float age = now - revealTime[i];
            if (age >= jumpDuration) continue;

            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible) continue;

            float t = Mathf.Clamp01(age / jumpDuration);
            float eased = EaseOutBack(t);
            float y = (eased - 1f) * jumpHeight;

            TMP_MeshInfo mi = info.meshInfo[c.materialReferenceIndex];
            if (c.vertexIndex + 3 >= mi.vertices.Length) continue;

            for (int k = 0; k < 4; k++)
            {
                int vi = c.vertexIndex + k;
                mi.vertices[vi] += Vector3.up * y;

                if (fadeIn)
                {
                    float a = Mathf.Clamp01(t * 3f);
                    Color32 src = mi.colors32[vi];
                    mi.colors32[vi] = new Color32(src.r, src.g, src.b, (byte)(src.a * a));
                }
            }
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private static float EaseOutBack(float t)
    {
        float u = t - 1f;
        return 1f + BackC3 * u * u * u + BackC1 * u * u;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        jumpHeight = Mathf.Max(0f, jumpHeight);
        jumpDuration = Mathf.Max(0.001f, jumpDuration);
    }
#endif
}