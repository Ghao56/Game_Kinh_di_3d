using System;
using System.Collections.Generic;
using UnityEngine;

/// Danh mục âm thanh của game: key -> clip, âm lượng, pitch, khoảng cách, caption.
/// Tạo/gán bằng menu Tools/Setup Audio.
[CreateAssetMenu(menuName = "Audio/Sfx Library", fileName = "SfxLibrary")]
public class SfxLibrary : ScriptableObject
{
    public const string GroupMaster = "Master";
    public const string GroupMusic = "Music";
    public const string GroupAmbience = "Ambience";
    public const string GroupSfx = "SFX";
    public const string GroupVoice = "Voice";
    public const string GroupUi = "UI";

    /// Mô tả một key mà game đang chờ có clip.
    /// Setup tool dùng danh sách này để tạo sẵn entry rỗng — thiếu clip vẫn chạy,
    /// chỉ cảnh báo 1 lần mỗi key thay vì spam mỗi frame.
    public struct KeySpec
    {
        public string Key;
        public string Group;
        public string Caption;
        public float SpatialBlend;
        public float MaxDistance;
    }

    /// Caption để trống vì dùng chữ có dấu thì TMP font mặc định chưa chắc có glyph.
    public static readonly KeySpec[] ExpectedKeys =
    {
        // Nền
        new KeySpec { Key = "rain_loop",         Group = GroupAmbience, Caption = "",               SpatialBlend = 0f, MaxDistance = 40f },
        new KeySpec { Key = "house_ambience",    Group = GroupAmbience, Caption = "",               SpatialBlend = 0f, MaxDistance = 40f },

        // Kinh dị
        new KeySpec { Key = "knock_door",        Group = GroupSfx,      Caption = "[KNOCK]",        SpatialBlend = 1f, MaxDistance = 25f },
        new KeySpec { Key = "footsteps_ceiling", Group = GroupSfx,      Caption = "[FOOTSTEPS ABOVE]", SpatialBlend = 1f, MaxDistance = 25f },
        new KeySpec { Key = "door_creak",        Group = GroupSfx,      Caption = "[CREAK]",         SpatialBlend = 1f, MaxDistance = 18f },
        new KeySpec { Key = "door_open",         Group = GroupSfx,      Caption = "[DOOR OPENS]",    SpatialBlend = 1f, MaxDistance = 18f },
        new KeySpec { Key = "door_close",        Group = GroupSfx,      Caption = "[DOOR CLOSES]",   SpatialBlend = 1f, MaxDistance = 18f },
        new KeySpec { Key = "door_locked",       Group = GroupSfx,      Caption = "[LOCKED]",        SpatialBlend = 1f, MaxDistance = 18f },
        new KeySpec { Key = "memory_thud",       Group = GroupSfx,      Caption = "",                SpatialBlend = 1f, MaxDistance = 30f },

        // Điện thoại
        new KeySpec { Key = "phone_vibrate",     Group = GroupUi,       Caption = "",                SpatialBlend = 0f, MaxDistance = 5f },
        new KeySpec { Key = "phone_signal_lost", Group = GroupUi,       Caption = "[NO SIGNAL]",     SpatialBlend = 0f, MaxDistance = 5f },

        // Giọng nói
        new KeySpec { Key = "mom_voice_lines",   Group = GroupVoice,    Caption = "",                SpatialBlend = 0f, MaxDistance = 10f },
        new KeySpec { Key = "caller_voice",      Group = GroupVoice,    Caption = "[CALLER]",        SpatialBlend = 0f, MaxDistance = 10f },
        new KeySpec { Key = "nam_voice",         Group = GroupVoice,    Caption = "[VOICE]",         SpatialBlend = 0f, MaxDistance = 10f },

        // Bước chân — key chung (fallback khi chưa biết mặt sàn)
        new KeySpec { Key = "footstep_walk",     Group = GroupSfx,      Caption = "",                SpatialBlend = 1f, MaxDistance = 15f },
        new KeySpec { Key = "footstep_sprint",   Group = GroupSfx,      Caption = "",                SpatialBlend = 1f, MaxDistance = 18f },
        new KeySpec { Key = "footstep_crouch",   Group = GroupSfx,      Caption = "",                SpatialBlend = 1f, MaxDistance = 12f },

        // Bước chân — gỗ
        new KeySpec { Key = "footstep_wood_walk",   Group = GroupSfx, Caption = "", SpatialBlend = 1f, MaxDistance = 15f },
        new KeySpec { Key = "footstep_wood_sprint", Group = GroupSfx, Caption = "", SpatialBlend = 1f, MaxDistance = 18f },
        new KeySpec { Key = "footstep_wood_crouch", Group = GroupSfx, Caption = "", SpatialBlend = 1f, MaxDistance = 12f },

        // Bước chân — gạch
        new KeySpec { Key = "footstep_tile_walk",   Group = GroupSfx, Caption = "", SpatialBlend = 1f, MaxDistance = 15f },
        new KeySpec { Key = "footstep_tile_sprint", Group = GroupSfx, Caption = "", SpatialBlend = 1f, MaxDistance = 18f },
        new KeySpec { Key = "footstep_tile_crouch", Group = GroupSfx, Caption = "", SpatialBlend = 1f, MaxDistance = 12f },
    };

    [Serializable]
    public class Entry
    {
        [Tooltip("Khoá dùng để gọi trong code, ví dụ \"knock_door\".")]
        public string key = string.Empty;

        [Tooltip("Master / Music / Ambience / SFX / Voice / UI")]
        public string group = GroupSfx;

        [Tooltip("Để trống nếu chưa có clip — AudioManager sẽ bỏ qua, chỉ cảnh báo 1 lần.")]
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Random pitch trong khoảng này để lặp không bị máy móc.")]
        public Vector2 pitchRange = new Vector2(0.97f, 1.03f);

        [Range(0f, 1f)] public float spatialBlend = 1f;
        public float minDistance = 1f;
        public float maxDistance = 20f;

        [Tooltip("Chữ hiện ở Caption UI, ví dụ [KNOCK]. Để trống = không hiện.")]
        public string caption = string.Empty;

        [Min(0f)] [Tooltip("Khoảng lặng tối thiểu giữa hai lần phát cùng key.")]
        public float cooldown = 0.05f;

        public bool HasClip => clips != null && clips.Length > 0;

        /// Chọn ngẫu nhiên 1 clip, bỏ qua slot null.
        public AudioClip PickClip()
        {
            if (!HasClip) return null;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
                if (clip != null) return clip;
            }

            return clips[0];
        }
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, Entry> lookup;

    public IReadOnlyList<Entry> Entries => entries;

    /// Tìm entry theo key. Dựng bảng tra cứu ở lần gọi đầu tiên.
    /// Sau khi sửa asset trong Inspector, OnValidate tự dựng lại.
    public bool TryGet(string key, out Entry entry)
    {
        if (string.IsNullOrEmpty(key))
        {
            entry = null;
            return false;
        }

        if (lookup == null) Rebuild();

        return lookup.TryGetValue(key, out entry);
    }

    /// Dựng lại bảng tra cứu. Gọi từ Setup tool sau khi sửa asset.
    public void Rebuild()
    {
        lookup = new Dictionary<string, Entry>(entries.Count);

        foreach (Entry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key)) continue;

            if (lookup.ContainsKey(entry.key))
            {
                Debug.LogWarning($"[{nameof(SfxLibrary)}] Key trùng: '{entry.key}'. Giữ entry đầu tiên.", this);
                continue;
            }

            lookup.Add(entry.key, entry);
        }
    }

    /// Tạo entry cho key còn thiếu, giữ nguyên entry đã có (không mất clip đã gán tay).
    public void EnsureKeys(KeySpec[] specs)
    {
        if (entries == null) entries = new List<Entry>();

        foreach (KeySpec spec in specs)
        {
            bool exists = false;

            foreach (Entry entry in entries)
            {
                if (entry != null && entry.key == spec.Key)
                {
                    exists = true;
                    break;
                }
            }

            if (exists) continue;

            entries.Add(new Entry
            {
                key = spec.Key,
                group = spec.Group,
                caption = spec.Caption,
                spatialBlend = spec.SpatialBlend,
                maxDistance = spec.MaxDistance
            });
        }

        Rebuild();
    }

    private void OnValidate()
    {
        Rebuild();
    }
}