using UnityEngine;

/// Một dòng thoại. Dùng trong DialogueData hoặc truyền thẳng qua DialogueManager.ShowLines.
[System.Serializable]
public class DialogueLine
{
    [Tooltip("Tên người nói. Để trống thì ẩn dòng tên, phần text sẽ nới xuống để không nhảy layout.")]
    public string speaker = string.Empty;

    [TextArea(2, 5)]
    [Tooltip("Nội dung. Hỗ trợ rich text của TMP: <b>, <i>, <color>, <size>.")]
    public string text = string.Empty;

    [Tooltip("Giây mỗi ký tự thường. Rồi sẽ nhân ngẫu nhiên trong [Jitter Min, Jitter Max] cho tự nhiên.")]
    public float charDelay = 0.035f;

    [Range(0.5f, 1f)] [Tooltip("Hệ số ngẫu nhiên delay mỗi ký tự. 1 = không ngẫu nhiên.")]
    public float jitter = 0.25f;

    [Min(0f)] [Tooltip("Nghỉ sau dấu phẩy: , ; :")]
    public float commaDelay = 0.1f;

    [Min(0f)] [Tooltip("Nghỉ sau dấu câu: . ! ? … (chỉ nghỉ dài ở dấu cuối, dấu trong '...' hoặc '?!' nghỉ ngắn)")]
    public float sentenceDelay = 0.28f;

    [Min(0f)] [Tooltip("Nghỉ sau khoảng trắng.")]
    public float spaceDelay = 0.015f;

    [Min(0f)] [Tooltip("Nghỉ sau xuống dòng.")]
    public float newlineDelay = 0.25f;

    [Tooltip("Khoảng pitch ngẫu nhiên của tiếng blip, ví dụ 0.92 – 1.08.")]
    public Vector2 blipPitchRange = new Vector2(0.92f, 1.08f);

    [Range(0f, 1f)] [Tooltip("Âm lượng blip. 0 = tắt tiếng cho dòng này.")]
    public float blipVolume = 0.4f;

    [Tooltip("Phát blip cả ở khoảng trắng. Thường để tắt để đỡ rối.")]
    public bool blipOnSpaces = false;

    [Tooltip("Tự sang dòng tiếp / tự đóng thoại sau khi gõ xong, không cần bấm. Bỏ tick để chờ người chơi bấm.")]
    public bool autoAdvance = false;

    [Min(0f)] [Tooltip("Giây chờ sau khi gõ xong dòng auto trước khi tự chuyển tiếp.")]
    public float autoAdvanceDelay = 1.5f;
}

[CreateAssetMenu(fileName = "DialogueData", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [TextArea(1, 40)] [Tooltip("Ghi chú cho người làm nội dung, không hiện trong game.")]
    public string notes = string.Empty;

    public DialogueLine[] lines = new DialogueLine[0];
}