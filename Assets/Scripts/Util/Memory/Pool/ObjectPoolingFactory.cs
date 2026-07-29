#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ObjectPoolingFactory<T> : IPoolOwner where T : Component
{
    private sealed class Bucket
    {
        public readonly Stack<T> Available = new Stack<T>();
        public readonly Stack<T> Scratch = new Stack<T>();
        public readonly HashSet<T> InUse = new HashSet<T>();
        public readonly List<T> All = new List<T>();
        public PoolPolicy Policy;
        public Transform Root;
        public int PeakInUse;
        public int FailedRentCount;
    }

    private readonly Dictionary<PoolKey, Bucket> m_Buckets = new Dictionary<PoolKey, Bucket>();
    private readonly Dictionary<string, PoolKey> m_LegacyKeys = new Dictionary<string, PoolKey>();
    private readonly Transform m_Root;

    public ObjectPoolingFactory(Transform root) => m_Root = root;
    public int RegisteredPoolCount => m_Buckets.Count;

    public bool Register(GameObject prefab, PoolPolicy policy)
    {
        if (prefab == null || !prefab.TryGetComponent<T>(out _))
        {
            Debug.LogError($"[ObjectPoolingFactory<{typeof(T).Name}>] 유효하지 않은 프리팹입니다.");
            return false;
        }

        PoolKey key = new PoolKey(prefab, typeof(T));
        PoolPolicy normalized = policy.Normalized();
        if (m_Buckets.TryGetValue(key, out Bucket existing))
        {
            existing.Policy = new PoolPolicy(
                Mathf.Max(existing.Policy.InitialCapacity, normalized.InitialCapacity),
                Mathf.Max(existing.Policy.MaxCapacity, normalized.MaxCapacity),
                existing.Policy.Scope,
                existing.Policy.ResetTransformOnReturn || normalized.ResetTransformOnReturn);
            return true;
        }

        Transform root = new GameObject(prefab.name + "_" + typeof(T).Name + "_Pool").transform;
        root.SetParent(m_Root, false);
        var bucket = new Bucket { Policy = normalized, Root = root };
        m_Buckets.Add(key, bucket);
        if (!m_LegacyKeys.ContainsKey(prefab.name)) m_LegacyKeys.Add(prefab.name, key);
        else m_LegacyKeys.Remove(prefab.name);
        return Prewarm(key, normalized.InitialCapacity);
    }

    public bool IsRegistered(GameObject prefab)
        => prefab != null && m_Buckets.ContainsKey(new PoolKey(prefab, typeof(T)));

    public bool Prewarm(GameObject prefab, int count)
        => prefab != null && Prewarm(new PoolKey(prefab, typeof(T)), count);

    public bool Prewarm(PoolKey key, int count)
    {
        if (!m_Buckets.TryGetValue(key, out Bucket bucket)) return false;
        PruneDestroyed(bucket);
        int target = Mathf.Clamp(count, 0, bucket.Policy.MaxCapacity);
        while (bucket.All.Count < target)
        {
            T item = Create(key, bucket);
            if (item == null) return false;
            bucket.Available.Push(item);
        }
        return bucket.All.Count >= target;
    }

    public bool TryRent(GameObject prefab, in PoolSpawnContext context, out T instance)
    {
        instance = null;
        return prefab != null && TryRent(new PoolKey(prefab, typeof(T)), context, out instance);
    }

    public bool TryRent(PoolKey key, in PoolSpawnContext context, out T instance)
    {
        instance = null;
        if (!m_Buckets.TryGetValue(key, out Bucket bucket)) return false;
        PruneDestroyed(bucket);
        while (bucket.Available.Count > 0 && instance == null) instance = bucket.Available.Pop();
        if (instance == null && bucket.All.Count < bucket.Policy.MaxCapacity) instance = Create(key, bucket);
        if (instance == null)
        {
            bucket.FailedRentCount++;
            return false;
        }

        PoolMember member = instance.GetComponent<PoolMember>();
        if (member == null || !member.BeginRent())
        {
            bucket.FailedRentCount++;
            instance = null;
            return false;
        }

        bucket.InUse.Add(instance);
        bucket.PeakInUse = Mathf.Max(bucket.PeakInUse, bucket.InUse.Count);
        Transform cached = instance.transform;
        cached.SetParent(context.Parent, true);
        cached.SetPositionAndRotation(context.Position, context.Rotation);
        try
        {
            member.InvokeRent(context);
            instance.gameObject.SetActive(context.Activate);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            // OnPoolRent 도중 일부 상태만 변경되었을 수 있으므로 반환 콜백까지 실행해 롤백한다.
            ReturnInternal(instance, member, bucket, true);
            instance = null;
            return false;
        }
    }

    public bool Return(T instance)
    {
        if (instance == null) return false;
        PoolMember member = instance.GetComponent<PoolMember>();
        return member != null && Return(member);
    }

    bool IPoolOwner.Return(PoolMember member) => Return(member);

    private bool Return(PoolMember member)
    {
        if (member == null || !member.IsRented || !m_Buckets.TryGetValue(member.Key, out Bucket bucket))
            return false;
        T instance = member.GetComponent<T>();
        if (instance == null || !bucket.InUse.Contains(instance)) return false;
        ReturnInternal(instance, member, bucket, true);
        return true;
    }

    public PoolStats GetStats(GameObject prefab)
        => prefab != null ? GetStats(new PoolKey(prefab, typeof(T))) : default;

    public PoolStats GetStats(PoolKey key)
    {
        if (!m_Buckets.TryGetValue(key, out Bucket bucket)) return default;
        return new PoolStats(bucket.All.Count, bucket.InUse.Count, bucket.Available.Count,
            bucket.PeakInUse, bucket.FailedRentCount);
    }

    public void Trim(PoolScope scope)
    {
        foreach (KeyValuePair<PoolKey, Bucket> pair in m_Buckets)
        {
            Bucket bucket = pair.Value;
            if (bucket.Policy.Scope != scope) continue;
            int keep = Mathf.Max(0, bucket.Policy.InitialCapacity - bucket.InUse.Count);
            while (bucket.Available.Count > keep)
            {
                T item = bucket.Available.Pop();
                bucket.All.Remove(item);
                DestroyObject(item != null ? item.gameObject : null);
            }
        }
    }

    public void ClearScope(PoolScope scope, bool returnActive)
    {
        var keys = new List<PoolKey>();
        foreach (KeyValuePair<PoolKey, Bucket> pair in m_Buckets)
            if (pair.Value.Policy.Scope == scope) keys.Add(pair.Key);
        for (int i = 0; i < keys.Count; i++) ClearBucket(keys[i], returnActive);
    }

    public void Clear()
    {
        var keys = new List<PoolKey>(m_Buckets.Keys);
        for (int i = 0; i < keys.Count; i++) ClearBucket(keys[i], true);
        m_LegacyKeys.Clear();
    }

    public void Initialize(Transform parent, IEnumerable<GameObject> prefabs, string defaultKey = "")
    {
        Clear();
        if (prefabs == null) return;
        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null || !prefab.TryGetComponent<T>(out T sample)) continue;
            int size = sample is Poolable poolable ? poolable.poolSize : 10;
            Register(prefab, new PoolPolicy(size, Mathf.Max(size, 32), PoolScope.Scene));
        }
    }

    public T Spawn(GameObject prefab, Vector3 position, GameObject owner = null)
        => TryRent(prefab, PoolSpawnContext.At(position), out T item) ? item : null;

    public T Spawn(string key, Vector3 position, GameObject owner = null)
        => !string.IsNullOrEmpty(key) && m_LegacyKeys.TryGetValue(key, out PoolKey poolKey) &&
           TryRent(poolKey, PoolSpawnContext.At(position), out T item) ? item : null;

    public void Despawn(T pooled) => Return(pooled);

    private T Create(PoolKey key, Bucket bucket)
    {
        GameObject created = UnityEngine.Object.Instantiate(key.Prefab, bucket.Root);
        if (!created.TryGetComponent<T>(out T instance))
        {
            DestroyObject(created);
            return null;
        }
        PoolMember member = created.GetComponent<PoolMember>();
        if (member == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[ObjectPoolingFactory<{typeof(T).Name}>] {key.Prefab.name} 프리팹에 PoolMember가 없어 런타임 복제본에 자동 추가했습니다.",
                key.Prefab);
#endif
            member = created.AddComponent<PoolMember>();
        }
        member.Configure(this, key);
        created.SetActive(false);
        bucket.All.Add(instance);
        return instance;
    }

    private static void ReturnInternal(T instance, PoolMember member, Bucket bucket, bool invokeCallback)
    {
        if (invokeCallback)
        {
            try { member.InvokeReturn(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        instance.gameObject.SetActive(false);
        bucket.InUse.Remove(instance);
        Transform cached = instance.transform;
        cached.SetParent(bucket.Root, false);
        if (bucket.Policy.ResetTransformOnReturn)
        {
            cached.localPosition = Vector3.zero;
            cached.localRotation = Quaternion.identity;
            cached.localScale = Vector3.one;
        }
        member.CompleteReturn();
        bucket.Available.Push(instance);
    }

    private void ClearBucket(PoolKey key, bool returnActive)
    {
        if (!m_Buckets.TryGetValue(key, out Bucket bucket)) return;
        if (returnActive)
        {
            var active = new List<T>(bucket.InUse);
            for (int i = 0; i < active.Count; i++) Return(active[i]);
        }
        if (bucket.InUse.Count > 0) return;
        for (int i = 0; i < bucket.All.Count; i++)
            if (bucket.All[i] != null) DestroyObject(bucket.All[i].gameObject);
        DestroyObject(bucket.Root != null ? bucket.Root.gameObject : null);
        m_Buckets.Remove(key);
        RemoveLegacyKey(key);
    }

    private void RemoveLegacyKey(PoolKey key)
    {
        string removeName = null;
        foreach (KeyValuePair<string, PoolKey> pair in m_LegacyKeys)
        {
            if (pair.Value.Equals(key))
            {
                removeName = pair.Key;
                break;
            }
        }
        if (removeName != null) m_LegacyKeys.Remove(removeName);
    }

    private static void DestroyObject(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(target);
        else UnityEngine.Object.DestroyImmediate(target);
    }

    private static void PruneDestroyed(Bucket bucket)
    {
        while (bucket.Available.Count > 0)
        {
            T available = bucket.Available.Pop();
            if (available != null) bucket.Scratch.Push(available);
        }
        while (bucket.Scratch.Count > 0) bucket.Available.Push(bucket.Scratch.Pop());

        for (int i = bucket.All.Count - 1; i >= 0; i--)
        {
            T item = bucket.All[i];
            if (item != null) continue;
            bucket.InUse.Remove(item);
            bucket.All.RemoveAt(i);
        }
    }
}
#endif
