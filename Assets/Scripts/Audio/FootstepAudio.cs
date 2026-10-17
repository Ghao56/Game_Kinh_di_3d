using System;
using UnityEngine;

/// Bước chân và tiếng động phát ra khi người chơi di chuyển.
///
/// Cố tình KHÔNG sửa ThirdPersonController: tốc độ lấy từ độ dời vị trí transform,
/// trạng thái suy ra theo ngưỡng tốc độ. Nếu sau này đổi moveSpeed / sprintMultiplier
/// trong ThirdPersonController thì phải chỉnh lại các ngưỡng ở đây cho khớp.
[RequireComponent(typeof(CharacterController))]
public class FootstepAudio : MonoBehaviour
{
    private enum MoveState
    {
        Crouch,
        Walk,
        Sprint
    }

    [Header("Tham chiếu")]
    [Tooltip("Để trống thì tự tìm trong chính GameObject này.")]
    [SerializeField] private CharacterController characterController;

    [Header("Ngưỡng tốc độ (m/s) — phải khớp ThirdPersonController")]
    [Tooltip("Dưới ngưỡng này là đi ngồi.")]
    [SerializeField] [Min(0.01f)] private float crouchMaxSpeed = 3f;

    [Tooltip("Trên ngưỡng này là chạy.")]
    [SerializeField] [Min(0.01f)] private float sprintMinSpeed = 6.5f;

    [Header("Quãng đường mỗi bước (m)")]
    [SerializeField] [Min(0.1f)] private float stepLengthWalk = 2.2f;
    [SerializeField] [Min(0.1f)] private float stepLengthSprint = 2.8f;
    [SerializeField] [Min(0.1f)] private float stepLengthCrouch = 1.6f;

    [Header("Tiếng động cho enemy AI nghe")]
    [SerializeField] [Min(0f)] private float noiseLoudnessWalk = 0.5f;
    [SerializeField] [Min(0f)] private float noiseRadiusWalk = 8f;
    [SerializeField] [Min(0f)] private float noiseLoudnessSprint = 1f;
    [SerializeField] [Min(0f)] private float noiseRadiusSprint = 18f;
    [SerializeField] [Min(0f)] private float noiseLoudnessCrouch = 0.15f;
    [SerializeField] [Min(0f)] private float noiseRadiusCrouch = 3f;

    [Header("Mặt sàn")]
    [Tooltip("Tag dạng FloorWood -> key footstep_wood_*. Để trống thì bỏ qua cách này.")]
    [SerializeField] private string surfaceTagPrefix = "Floor";

    [Tooltip("Dùng tên PhysicMaterial của sàn làm tên bề mặt, ví dụ \"wood\".")]
    [SerializeField] private bool usePhysicMaterialName = true;

    [SerializeField] [Min(0.01f)] private float surfaceProbeHeight = 1f;
    [SerializeField] [Min(0.01f)] private float surfaceProbeDistance = 0.5f;
    [SerializeField] private LayerMask surfaceLayers = ~0;

    [Header("Debug")]
    [SerializeField] private bool logSteps;

    private Vector3 lastPosition;
    private float distanceSinceStep;
    private string lastStepKey; // key bước cuối - dùng để cắt đuôi khi dừng.

    private void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        lastPosition = transform.position;
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        distanceSinceStep = 0f;
    }

    private void Update()
    {
        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;

        // Đang rơi / trên không trung -> không có bước chân.
        if (characterController != null && !characterController.isGrounded)
        {
            CutStepTail();
            distanceSinceStep = 0f;
            return;
        }

        float travelled = new Vector3(delta.x, 0f, delta.z).magnitude;

        // timeScale = 0 (pause) -> deltaTime = 0 -> travelled = 0 -> tự nhiên đứng yên.
        // Ngưỡng 2mm/frame + tốc độ tối thiểu 0.8 m/s: lọc hẳn micro-slide của
        // CharacterController trên dốc/địa hình (đứng yên vẫn bị trôi vài mm/frame,
        // ngưỡng cũ 0.0001 coi là đang đi nên tiếng bước không dừng).
        float speed = travelled / Time.deltaTime;
        if (travelled <= 0.002f || speed < 0.8f)
        {
            CutStepTail();
            distanceSinceStep = 0f;
            return;
        }

        MoveState state = Classify(speed);

        distanceSinceStep += travelled;

        float stepLength = StepLength(state);
        if (distanceSinceStep < stepLength) return;

        // Trừ đúng một bước và giữ phần dư để không mất nhịp khi framerate thấp.
        distanceSinceStep = Mathf.Min(distanceSinceStep - stepLength, stepLength);

        PlayStep(state);
    }

    private MoveState Classify(float speed)
    {
        if (speed < crouchMaxSpeed) return MoveState.Crouch;
        if (speed >= sprintMinSpeed) return MoveState.Sprint;
        return MoveState.Walk;
    }

    private float StepLength(MoveState state)
    {
        switch (state)
        {
            case MoveState.Sprint: return stepLengthSprint;
            case MoveState.Crouch: return stepLengthCrouch;
            default: return stepLengthWalk;
        }
    }

    private float NoiseLoudness(MoveState state)
    {
        switch (state)
        {
            case MoveState.Sprint: return noiseLoudnessSprint;
            case MoveState.Crouch: return noiseLoudnessCrouch;
            default: return noiseLoudnessWalk;
        }
    }

    private float NoiseRadius(MoveState state)
    {
        switch (state)
        {
            case MoveState.Sprint: return noiseRadiusSprint;
            case MoveState.Crouch: return noiseRadiusCrouch;
            default: return noiseRadiusWalk;
        }
    }

    private void PlayStep(MoveState state)
    {
        Vector3 position = transform.position;
        string stateKey = state.ToString().ToLowerInvariant();

        AudioManager manager = AudioManager.Instance;

        if (manager != null)
        {
            string key = ResolveStepKey(manager, stateKey);
            lastStepKey = key;

            // stealSameClip: bước mới khoá cùng nguồn (không chồng tiếng); khi dừng,
            // CutStepTail() tắc nguồn này để không còn đuôi loop.
            if (manager.TryPlayAt(key, position, 1f, null, true))
            {
                if (logSteps) Debug.Log($"[{nameof(FootstepAudio)}] Bước {stateKey} -> '{key}'.", this);
            }
        }

        NoiseSystem.ReportNoise(position, NoiseLoudness(state), NoiseRadius(state), gameObject);
    }

    /// Nhân vật ngừng đi -> dừng clip bước cuối ngay (clip là chuỗi loop dài,
    /// để nó phát hết thì tiếng chân "không dừng" sau khi đã đứng lại).
    private void CutStepTail()
    {
        if (string.IsNullOrEmpty(lastStepKey)) return;

        AudioManager manager = AudioManager.Instance;
        if (manager != null) manager.StopAudio(lastStepKey);
    }

    /// Ưu tiên key theo bề mặt (footstep_wood_walk), không có thì dùng key chung (footstep_walk).
    private string ResolveStepKey(AudioManager manager, string stateKey)
    {
        string surface = DetectSurface();

        if (!string.IsNullOrEmpty(surface))
        {
            string surfaceKey = $"footstep_{surface}_{stateKey}";

            if (manager.Library != null && manager.Library.TryGet(surfaceKey, out SfxLibrary.Entry entry) && entry.HasClip)
                return surfaceKey;
        }

        return $"footstep_{stateKey}";
    }

    private string DetectSurface()
    {
        Vector3 origin = transform.position + Vector3.up * surfaceProbeHeight;

        bool hitSomething = Physics.Raycast(
            origin,
            Vector3.down,
            out RaycastHit hit,
            surfaceProbeHeight + surfaceProbeDistance,
            surfaceLayers,
            QueryTriggerInteraction.Ignore);

        if (!hitSomething) return string.Empty;

        if (!string.IsNullOrEmpty(surfaceTagPrefix))
        {
            string tag = hit.collider.tag;

            if (!string.IsNullOrEmpty(tag) && tag.StartsWith(surfaceTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string fromTag = tag.Substring(surfaceTagPrefix.Length);

                if (!string.IsNullOrEmpty(fromTag)) return fromTag.ToLowerInvariant();
            }
        }

        if (usePhysicMaterialName && hit.collider.sharedMaterial != null)
            return Sanitize(hit.collider.sharedMaterial.name);

        return string.Empty;
    }

    private static string Sanitize(string raw)
    {
        return raw.Trim().Replace(' ', '_').ToLowerInvariant();
    }
}