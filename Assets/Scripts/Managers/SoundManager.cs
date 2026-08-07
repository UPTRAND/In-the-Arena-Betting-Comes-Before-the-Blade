#if UNITY_6000_0_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public sealed class SoundManager : Manager_Base
{
    private const ushort SoundInitializationOrder = 10;
    private const float MinimumLinearVolume = 0.0001f;
    private const string MasterVolumeKey = "InTheArena.MasterVolume";
    private const string BgmVolumeKey = "InTheArena.BGMVolume";
    private const string SfxVolumeKey = "InTheArena.SFXVolume";

    public static SoundManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private SoundCatalog m_Catalog;
    [SerializeField] private AudioMixer m_AudioMixer;

    [Header("Mixer Groups")]
    [SerializeField] private string m_MasterGroupName = "Master";
    [SerializeField] private string m_BgmGroupName = "BGM";
    [SerializeField] private string m_SfxGroupName = "SFX";

    [Header("Exposed Mixer Parameters")]
    [SerializeField] private string m_MasterVolumeParameter = "MasterVolume";
    [SerializeField] private string m_BgmVolumeParameter = "BGMVolume";
    [SerializeField] private string m_SfxVolumeParameter = "SFXVolume";

    [Header("SFX Pool")]
    [SerializeField, Min(1)] private int m_SfxPoolSize = 24;

    private readonly List<AudioSource> m_SfxSources = new List<AudioSource>(24);
    private readonly Dictionary<AudioSource, Coroutine> m_SfxFadeRoutines =
        new Dictionary<AudioSource, Coroutine>();
    private readonly HashSet<string> m_Warnings = new HashSet<string>();

    private AudioSource m_BgmSourceA;
    private AudioSource m_BgmSourceB;
    private AudioSource m_ActiveBgmSource;
    private Coroutine m_BgmFadeRoutine;
    private string m_CurrentBgmId;
    private bool m_PoolExhaustionWarned;
    private float m_MasterVolume = 1f;
    private float m_BgmVolume = 1f;
    private float m_SfxVolume = 1f;

    public override ushort InitializationOrder => SoundInitializationOrder;

    public float MasterVolume
    {
        get => m_MasterVolume;
        set => SetVolume(ref m_MasterVolume, value, MasterVolumeKey, m_MasterVolumeParameter);
    }

    public float BgmVolume
    {
        get => m_BgmVolume;
        set => SetVolume(ref m_BgmVolume, value, BgmVolumeKey, m_BgmVolumeParameter);
    }

    public float SfxVolume
    {
        get => m_SfxVolume;
        set => SetVolume(ref m_SfxVolume, value, SfxVolumeKey, m_SfxVolumeParameter);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            WarnOnce("duplicate", "A duplicate instance was destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (GetComponentInParent<Managers>() == null) DontDestroyOnLoad(gameObject);
    }

    public override bool Setup() => Instance == this;

    protected override bool Init()
    {
        if (m_Catalog == null) WarnOnce("catalog-null", "SoundCatalog is not assigned.");
        else m_Catalog.RebuildLookup();

        AudioMixerGroup bgmGroup = FindMixerGroup(m_BgmGroupName);
        AudioMixerGroup sfxGroup = FindMixerGroup(m_SfxGroupName);
        FindMixerGroup(m_MasterGroupName);

        Transform root = new GameObject("[AudioSources]").transform;
        root.SetParent(transform, false);
        m_BgmSourceA = CreateSource(root, "BGM_A", bgmGroup);
        m_BgmSourceB = CreateSource(root, "BGM_B", bgmGroup);

        m_SfxSources.Capacity = Mathf.Max(m_SfxSources.Capacity, m_SfxPoolSize);
        for (int i = 0; i < m_SfxPoolSize; i++)
            m_SfxSources.Add(CreateSource(root, $"SFX_{i:00}", sfxGroup));

        m_MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        m_BgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
        m_SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        ApplyVolume(m_MasterVolumeParameter, m_MasterVolume);
        ApplyVolume(m_BgmVolumeParameter, m_BgmVolume);
        ApplyVolume(m_SfxVolumeParameter, m_SfxVolume);
        return true;
    }

    public void PlayBgm(string id, bool loop = true, float fadeDuration = 0.5f)
    {
        if (!TryGetClip(id, true, out AudioClip clip) || !EnsureReady()) return;
        if (m_CurrentBgmId == id && m_ActiveBgmSource != null && m_ActiveBgmSource.isPlaying) return;

        StopBgmFade();
        AudioSource previous = m_ActiveBgmSource;
        AudioSource next = previous == m_BgmSourceA ? m_BgmSourceB : m_BgmSourceA;
        ResetSource(next);
        next.clip = clip;
        next.loop = loop;
        next.volume = fadeDuration > 0f ? 0f : 1f;
        next.Play();
        m_ActiveBgmSource = next;
        m_CurrentBgmId = id;

        if (fadeDuration <= 0f)
        {
            if (previous != null) ResetSource(previous);
            next.volume = 1f;
            return;
        }

        m_BgmFadeRoutine = StartCoroutine(CrossFadeBgm(previous, next, fadeDuration));
    }

    public void PlayRandomBgm(IEnumerable<string> candidateIds, bool loop = true, float fadeDuration = 0.5f)
    {
        if (candidateIds == null)
        {
            WarnOnce("random-null", "Random BGM candidates are null.");
            return;
        }

        string selectedId = null;
        int validCount = 0;
        foreach (string id in candidateIds)
        {
            if (!TryGetClip(id, true, out _)) continue;
            validCount++;
            if (Random.Range(0, validCount) == 0) selectedId = id;
        }

        if (validCount == 0)
        {
            WarnOnce("random-empty", "Random BGM candidates contain no valid ids.");
            return;
        }

        PlayBgm(selectedId, loop, fadeDuration);
    }

    public void StopBgm(float fadeDuration = 0.5f)
    {
        StopBgmFade();
        m_CurrentBgmId = null;
        AudioSource inactiveSource = m_ActiveBgmSource == m_BgmSourceA ? m_BgmSourceB : m_BgmSourceA;
        if (inactiveSource != null) ResetSource(inactiveSource);
        if (m_ActiveBgmSource == null) return;

        if (fadeDuration <= 0f)
        {
            ResetSource(m_ActiveBgmSource);
            m_ActiveBgmSource = null;
            return;
        }

        m_BgmFadeRoutine = StartCoroutine(FadeOutBgm(m_ActiveBgmSource, fadeDuration));
    }

    public AudioSource PlaySfx(
        string id,
        float pitch = 1f,
        bool loop = false,
        float volumeMultiplier = 1f)
    {
        if (!TryGetClip(id, false, out AudioClip clip) || !EnsureReady()) return null;

        AudioSource source = FindAvailableSfxSource();
        if (source == null)
        {
            if (!m_PoolExhaustionWarned)
            {
                m_PoolExhaustionWarned = true;
                Debug.LogWarning($"[SoundManager] All {m_SfxSources.Count} SFX sources are in use.", this);
            }
            return null;
        }

        StopSfxFade(source);
        ResetSource(source);
        source.clip = clip;
        source.loop = loop;
        source.pitch = pitch < 0f ? Random.Range(0.95f, 1.05f) : Mathf.Clamp(pitch, 0f, 3f);
        source.volume = Mathf.Clamp01(volumeMultiplier);
        source.Play();
        return source;
    }

    public void StopSfx(AudioSource source, bool fade = false, float fadeDuration = 0.25f)
    {
        if (source == null || !m_SfxSources.Contains(source)) return;
        StopSfxFade(source);
        if (!fade || fadeDuration <= 0f)
        {
            ResetSource(source);
            return;
        }

        m_SfxFadeRoutines[source] = StartCoroutine(FadeOutSfx(source, fadeDuration));
    }

    public AudioSource PlaySfxReplacingPrevious(
        ref AudioSource previous,
        string id,
        float pitch = 1f,
        bool loop = false,
        float volumeMultiplier = 1f)
    {
        StopSfx(previous);
        previous = PlaySfx(id, pitch, loop, volumeMultiplier);
        return previous;
    }

    public override void OnApplicationPauseChanged(bool paused)
    {
        if (paused) PlayerPrefs.Save();
    }

    public override void Release()
    {
        StopBgmFade();
        foreach (Coroutine routine in m_SfxFadeRoutines.Values)
            if (routine != null) StopCoroutine(routine);
        m_SfxFadeRoutines.Clear();
        PlayerPrefs.Save();
        if (Instance == this) Instance = null;
        base.Release();
    }

    protected override void OnDestroy()
    {
        Release();
        base.OnDestroy();
    }

    private AudioMixerGroup FindMixerGroup(string groupName)
    {
        if (m_AudioMixer == null)
        {
            WarnOnce("mixer-null", "AudioMixer is not assigned. Audio will use the default output.");
            return null;
        }

        AudioMixerGroup[] groups = m_AudioMixer.FindMatchingGroups(groupName);
        if (groups.Length > 0) return groups[0];
        WarnOnce("group-" + groupName, $"AudioMixer group '{groupName}' was not found.");
        return null;
    }

    private AudioSource CreateSource(Transform parent, string sourceName, AudioMixerGroup group)
    {
        var sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(parent, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.reverbZoneMix = 0f;
        source.outputAudioMixerGroup = group;
        return source;
    }

    private bool TryGetClip(string id, bool isBgm, out AudioClip clip)
    {
        clip = null;
        if (m_Catalog == null)
        {
            WarnOnce("catalog-null", "SoundCatalog is not assigned.");
            return false;
        }

        bool found = isBgm ? m_Catalog.TryGetBgm(id, out clip) : m_Catalog.TryGetSfx(id, out clip);
        if (!found) WarnOnce((isBgm ? "bgm-" : "sfx-") + id, $"{(isBgm ? "BGM" : "SFX")} id '{id}' was not found or has no clip.");
        return found;
    }

    private bool EnsureReady()
    {
        if (IsInitialized) return true;
        WarnOnce("not-ready", "The manager has not been initialized. Register it in Managers._allManagers.");
        return false;
    }

    private AudioSource FindAvailableSfxSource()
    {
        for (int i = 0; i < m_SfxSources.Count; i++)
            if (!m_SfxSources[i].isPlaying) return m_SfxSources[i];
        return null;
    }

    private void SetVolume(ref float field, float value, string preferenceKey, string parameter)
    {
        field = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(preferenceKey, field);
        ApplyVolume(parameter, field);
    }

    private void ApplyVolume(string parameter, float linearVolume)
    {
        if (m_AudioMixer == null || string.IsNullOrWhiteSpace(parameter)) return;
        if (!m_AudioMixer.SetFloat(parameter, LinearToDecibels(linearVolume)))
            WarnOnce("parameter-" + parameter, $"Exposed AudioMixer parameter '{parameter}' was not found.");
    }

    internal static float LinearToDecibels(float linearVolume)
        => Mathf.Log10(Mathf.Max(MinimumLinearVolume, Mathf.Clamp01(linearVolume))) * 20f;

    private IEnumerator CrossFadeBgm(AudioSource previous, AudioSource next, float duration)
    {
        float previousStartVolume = previous != null ? previous.volume : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (previous != null) previous.volume = Mathf.Lerp(previousStartVolume, 0f, progress);
            next.volume = progress;
            yield return null;
        }

        if (previous != null) ResetSource(previous);
        next.volume = 1f;
        m_BgmFadeRoutine = null;
    }

    private IEnumerator FadeOutBgm(AudioSource source, float duration)
    {
        yield return FadeVolume(source, duration);
        ResetSource(source);
        if (m_ActiveBgmSource == source) m_ActiveBgmSource = null;
        m_BgmFadeRoutine = null;
    }

    private IEnumerator FadeOutSfx(AudioSource source, float duration)
    {
        yield return FadeVolume(source, duration);
        ResetSource(source);
        m_SfxFadeRoutines.Remove(source);
    }

    private static IEnumerator FadeVolume(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;
        while (elapsed < duration && source.isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
    }

    private void StopBgmFade()
    {
        if (m_BgmFadeRoutine == null) return;
        StopCoroutine(m_BgmFadeRoutine);
        m_BgmFadeRoutine = null;
    }

    private void StopSfxFade(AudioSource source)
    {
        if (!m_SfxFadeRoutines.TryGetValue(source, out Coroutine routine)) return;
        if (routine != null) StopCoroutine(routine);
        m_SfxFadeRoutines.Remove(source);
    }

    private static void ResetSource(AudioSource source)
    {
        source.Stop();
        source.clip = null;
        source.loop = false;
        source.pitch = 1f;
        source.volume = 1f;
    }

    private void WarnOnce(string key, string message)
    {
        if (m_Warnings.Add(key)) Debug.LogWarning("[SoundManager] " + message, this);
    }
}
#endif
