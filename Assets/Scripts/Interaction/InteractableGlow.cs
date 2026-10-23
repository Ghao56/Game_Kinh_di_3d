using UnityEngine;

/// <summary>
/// Đánh dấu vật có thể highlight. Khi được highlight, các Renderer con được set bit
/// highlightRenderingLayerBit trong renderingLayerMask — độc lập với Layer vật lý,
/// không đổi mesh/material gốc. Mask-pass của InteractableOutlineFeature đọc bit này.
/// </summary>
public class InteractableGlow : MonoBehaviour
{
    [SerializeField] private Color glowColor = new Color(0.45f, 0.85f, 1f, 1f);
    [Tooltip("Phải khớp InteractableOutlineFeature.highlightRenderingLayerBit.")]
    [SerializeField] private uint highlightRenderingLayerBit = 1u << 1;

    private Renderer[] renderers;
    private uint[] originalMasks;
    private bool aimOn;
    private bool hintOn;
    // Cua ghep: sang theo canh chinh (tim bang DoorInteractable.linkedDoors cua cung GameObject).
    private InteractableGlow[] linkedGlows;

    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalMasks = new uint[renderers.Length];

        var mpb = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMasks[i] = renderers[i].renderingLayerMask;
            mpb.SetColor(GlowColorId, glowColor);
            renderers[i].SetPropertyBlock(mpb);
        }

        DoorInteractable door = GetComponentInParent<DoorInteractable>();
        int count = door != null && door.LinkedDoors != null ? door.LinkedDoors.Length : 0;
        linkedGlows = new InteractableGlow[count];
        for (int i = 0; i < count; i++)
            linkedGlows[i] = door != null && door.LinkedDoors[i] != null ? door.LinkedDoors[i].GetComponent<InteractableGlow>() : null;
    }

    public void SetHighlighted(bool on)
    {
        aimOn = on;
        Refresh();

        for (int i = 0; i < linkedGlows.Length; i++)
        {
            InteractableGlow other = linkedGlows[i];
            if (other != null && other != this) other.RefreshFromLinked(on);
        }
    }

    // Chi den khi canh phu: khong lan truyen nua de tranh loop hai chieu.
    private void RefreshFromLinked(bool on)
    {
        aimOn = on;
        Refresh();
    }

    // Glow goi y quest (xuyen vat, gui tu DoorInteractable): bat da khi quest mo, tat khi da tuong tac.
    public void SetQuestHint(bool on)
    {
        hintOn = on;
        Refresh();
    }

    private void Refresh()
    {
        if (renderers == null) return;

        bool on = aimOn || hintOn;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].renderingLayerMask = on
                ? originalMasks[i] | highlightRenderingLayerBit
                : originalMasks[i];
        }
    }

    private void OnDestroy()
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].renderingLayerMask = originalMasks[i];
        }
    }
}