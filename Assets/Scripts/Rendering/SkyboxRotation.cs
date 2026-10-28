using UnityEngine;

/// <summary>
/// Slowly rotates the active skybox material to simulate drifting clouds.
/// Works with Skybox/6 Sided and Skybox/Panoramic (both expose "_Rotation").
/// Attach to any persistent object (e.g. the camera or a global object).
/// </summary>
public class SkyboxRotation : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second. 0.2-0.5 = very slow drift (a full circle takes 12-30 minutes). Negative value reverses the spin direction, e.g. -0.3.")]
    [SerializeField, Range(-10f, 10f)] private float rotationSpeed = 0.3f;

    [Tooltip("Use unscaled time so clouds keep moving while the game is paused or slowed.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Optional: keep clouds moving at a steady, believable pace - avoids noticeable jumps at low framerates.")]
    [SerializeField, Range(0.1f, 2f)] private float maxDeltaSeconds = 0.25f;

    private Material _skybox;
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");

    private void Start()
    {
        _skybox = RenderSettings.skybox;
        if (_skybox == null || !_skybox.HasProperty(RotationId))
        {
            Debug.LogWarning("[SkyboxRotation] Active skybox has no '_Rotation' property.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        delta = Mathf.Min(delta, maxDeltaSeconds); // avoid big jumps after hitches/pauses
        float angle = Mathf.Repeat(_skybox.GetFloat(RotationId) + rotationSpeed * delta, 360f);
        _skybox.SetFloat(RotationId, angle);
    }
}
