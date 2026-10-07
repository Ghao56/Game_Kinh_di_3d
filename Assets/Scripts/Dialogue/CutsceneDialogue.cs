using System.Collections;
using UnityEngine;

/// Hiện thoại sau `delay` giây khi object được bật — dùng để chạy thoại trong cutscene.
public class CutsceneDialogue : MonoBehaviour
{
    [SerializeField] private DialogueData dialogue;
    [SerializeField] private float delay = 1f;

    private void OnEnable()
    {
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        DialogueManager manager = DialogueManager.Instance;
        if (manager == null)
        {
            Debug.LogError($"[{nameof(CutsceneDialogue)}] Không có DialogueManager trong scene. Chạy Tools/Setup Dialogue.", this);
            yield break;
        }
        if (dialogue == null)
        {
            Debug.LogError($"[{nameof(CutsceneDialogue)}] Chưa gán Dialogue.", this);
            yield break;
        }
        manager.ShowDialogue(dialogue);
    }
}
