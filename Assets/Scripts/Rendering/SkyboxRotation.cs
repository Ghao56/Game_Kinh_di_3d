using UnityEngine;

/// <summary>
/// Slowly rotates the active skybox material to simulate drifting clouds.
/// Works with Skybox/6 Sided and Skybox/Panoramic (both expose "_Rotation").
/// Attach to any persistent object (e.g. the camera or a global object).
/// </summary>
public class SkyboxRotation : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second. 1-3 looks like slow wind-driven clouds.")]
    [SerializeField, Range(0f, 360f)] private float rotationSpeed = 2f;

    [Tooltip("Use unscaled time so clouds keep moving while the game is paused or slowed.")]
    [SerializeField] private bool useUnscaledTime = true;

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
        float angle = Mathf.Repeat(_skybox.GetFloat(RotationId) + rotationSpeed * delta, 360f);
        _skybox.SetFloat(RotationId, angle);
    }
}
