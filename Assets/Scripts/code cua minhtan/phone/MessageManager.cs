using UnityEngine;
using TMPro;
using System.Collections;

public class MessageManager : MonoBehaviour
{
    [Header("Kéo chữ TextMeshPro vào đây")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Thời gian chờ giữa các tin nhắn (giây)")]
    [SerializeField] private float delayTime = 3f;

    [Header("Có tính thời gian khi game pause không")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Danh sách tin nhắn")]
    [SerializeField]
    private string[] linesOfText =
    {
        "Cha: Con đang ở đâu, sao chưa về?",
        "Cha: Gọi lại cho cha ngay!",
        "Cha: ...Đừng làm cha lo lắng..."
    };

    private Coroutine messageRoutine;

    private void OnEnable()
    {
        if (messageText == null)
        {
            Debug.LogError($"{nameof(MessageManager)}: chưa gán messageText!", this);
            return;
        }

        // Đảm bảo không có Coroutine cũ nào còn chạy trước khi bắt đầu cái mới
        if (messageRoutine != null)
            StopCoroutine(messageRoutine);

        messageRoutine = StartCoroutine(ShowMessagesRoutine());
    }

    private void OnDisable()
    {
        if (messageRoutine != null)
        {
            StopCoroutine(messageRoutine);
            messageRoutine = null;
        }
    }

    private IEnumerator ShowMessagesRoutine()
    {
        messageText.text = "";
        string currentText = "";

        for (int i = 0; i < linesOfText.Length; i++)
        {
            currentText += linesOfText[i] + "\n\n";
            messageText.text = currentText;

            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(delayTime);
            else
                yield return new WaitForSeconds(delayTime);
        }

        messageRoutine = null; // đánh dấu đã chạy xong
    }
}