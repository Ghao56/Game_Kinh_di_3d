using UnityEngine;

/// Vẽ khung hộp của các BoxCollider con trong Scene view để nhìn thấy ranh giới
/// map. Chỉ là gizmo debug, không ảnh hưởng gì tới gameplay.
public class MapBoundsGizmo : MonoBehaviour
{
    [SerializeField] private Color wireColor = new Color(0f, 1f, 0.35f, 0.6f);

    private void OnDrawGizmos()
    {
        var oldMatrix = Gizmos.matrix;
        var oldColor = Gizmos.color;
        foreach (var box in GetComponentsInChildren<BoxCollider>())
        {
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }
}
