using UnityEngine;

public class InteractionOriginFollower : MonoBehaviour
{
    [Header("Follow Sources")]
    [SerializeField] private Transform bodyTarget;
    [SerializeField] private Transform lookSource;

    [Header("Offset")]
    [Tooltip("Offset so với bodyTarget. LƯU Ý: bodyTarget đứng ở GIỮA capsule, không phải ở chân. " +
             "Nhân vật cao 2m: chân ở -0.48, đỉnh đầu ở 1.52. Nên Y = 0.5 là ngực — ray phóng từ đây.")]
    [SerializeField] private float eyeHeight = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmo = true;
    [SerializeField] private float gizmoRayLength = 2f;

    private void OnValidate()
    {
        if (bodyTarget == null)
        {
            Debug.LogWarning("[InteractionOriginFollower] Chưa gán Body Target.", this);
        }

        if (lookSource == null)
        {
            Debug.LogWarning("[InteractionOriginFollower] Chưa gán Look Source.", this);
        }
    }

    private void LateUpdate()
    {
        if (bodyTarget == null || lookSource == null)
        {
            return;
        }

        transform.position = bodyTarget.position + Vector3.up * eyeHeight;
        transform.rotation = lookSource.rotation;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.05f);
        Gizmos.DrawRay(transform.position, transform.forward * gizmoRayLength);
    }
}