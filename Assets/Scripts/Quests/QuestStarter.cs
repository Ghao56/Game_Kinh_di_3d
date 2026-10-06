using System.Collections;
using UnityEngine;

/// Tự bắt đầu quest sau khi scene chạy. Dùng tạm cho quest 4.1 cho tới khi
/// điện thoại gọi StartQuest("explore_house") ở tin nhắn cuối.
public class QuestStarter : MonoBehaviour
{
    [Tooltip("Quest sẽ bắt đầu. Phải nằm trong QuestManager.All Quests.")]
    [SerializeField] private string questId = "explore_house";

    [Min(0f)]
    [Tooltip("Chờ số giây sau khi scene chạy, để HUD kịp hiện.")]
    [SerializeField] private float delay = 0.5f;

    private void Start()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        QuestManager manager = QuestManager.Instance;
        if (manager == null)
        {
            Debug.LogError($"[{nameof(QuestStarter)}] Không có QuestManager trong scene.", this);
            yield break;
        }

        manager.StartQuest(questId);
    }
}