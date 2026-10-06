using System;
using UnityEngine;

/// Mọi mục tiêu đều là bộ đếm: đếm theo vùng (khám phá), cộng dồn (nhặt đồ)
/// hoặc đếm số lần tương tác.
[Serializable]
public class QuestObjective
{
    [Tooltip("Khoá mục tiêu, ví dụ: explore. QuestZone/QuestObjectiveInteractable gọi theo chuỗi này.")]
    [SerializeField] private string id = "objective";

    [SerializeField] private string text = "Mục tiêu";

    [Min(1)]
    [SerializeField] private int required = 1;

    public string Id => id;
    public string Text => text;
    public int Required => Mathf.Max(1, required);
}