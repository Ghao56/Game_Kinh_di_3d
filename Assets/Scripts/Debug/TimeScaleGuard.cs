using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Giám sát Time.timeScale toàn game. Tự tạo khi Play (không cần gắn ra scene).
/// Mỗi khi timeScale đổi giá trị (nhất là 0 => game đứng hình) sẽ log ra Console
/// kèm frame + scene, giúp xác địnhscript nào vừa khóa thời gian.
/// Script dùng xong có thể xóa. Không can Thiệp gameplay.
/// </summary>
public static class TimeScaleGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var go = new GameObject("[TimeScaleGuard]");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<GuardComponent>();
    }

    private class GuardComponent : MonoBehaviour
    {
        private float last = 1f;

        private void LateUpdate()
        {
            if (!Mathf.Approximately(Time.timeScale, last))
            {
                last = Time.timeScale;
                Debug.LogWarning(
                    $"[TimeScaleGuard] Frame {Time.frameCount}: timeScale = {Time.timeScale} (scene: {SceneManager.GetActiveScene().name})",
                    this);
            }
        }
    }
}
