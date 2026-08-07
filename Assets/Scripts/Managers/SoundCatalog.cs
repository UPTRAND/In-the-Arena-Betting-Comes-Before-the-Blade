#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundCatalog", menuName = "In The Arena/Audio/Sound Catalog")]
public sealed class SoundCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private string m_Id;
        [SerializeField] private AudioClip m_Clip;

        public string Id => m_Id;
        public AudioClip Clip => m_Clip;
    }

    [SerializeField] private List<Entry> m_BgmEntries = new List<Entry>();
    [SerializeField] private List<Entry> m_SfxEntries = new List<Entry>();

    private readonly Dictionary<string, AudioClip> m_BgmLookup =
        new Dictionary<string, AudioClip>(StringComparer.Ordinal);
    private readonly Dictionary<string, AudioClip> m_SfxLookup =
        new Dictionary<string, AudioClip>(StringComparer.Ordinal);
    private bool m_IsLookupReady;

    public bool TryGetBgm(string id, out AudioClip clip)
        => TryGet(m_BgmEntries, m_BgmLookup, id, out clip);

    public bool TryGetSfx(string id, out AudioClip clip)
        => TryGet(m_SfxEntries, m_SfxLookup, id, out clip);

    public void RebuildLookup()
    {
        BuildLookup(m_BgmEntries, m_BgmLookup);
        BuildLookup(m_SfxEntries, m_SfxLookup);
        m_IsLookupReady = true;
    }

    private bool TryGet(
        List<Entry> entries,
        Dictionary<string, AudioClip> lookup,
        string id,
        out AudioClip clip)
    {
        if (!m_IsLookupReady) RebuildLookup();
        clip = null;
        return !string.IsNullOrWhiteSpace(id) && lookup.TryGetValue(id, out clip) && clip != null;
    }

    private static void BuildLookup(List<Entry> entries, Dictionary<string, AudioClip> lookup)
    {
        lookup.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Clip == null) continue;
            lookup.TryAdd(entry.Id, entry.Clip);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_IsLookupReady = false;
        ValidateEntries(m_BgmEntries, "BGM");
        ValidateEntries(m_SfxEntries, "SFX");
    }

    private void ValidateEntries(List<Entry> entries, string category)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
            if (!ids.Add(entry.Id))
                Debug.LogWarning($"[SoundCatalog] {category} id '{entry.Id}' is duplicated.", this);
        }
    }
#endif
}
#endif
