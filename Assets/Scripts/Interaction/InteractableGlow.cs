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
    }

    public void SetHighlighted(bool on)
    {
        if (renderers == null)
            return;

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