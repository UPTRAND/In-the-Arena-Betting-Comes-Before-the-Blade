#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>공통 Factory에 UI Root 선택과 Open/Close 규칙만 더하는 어댑터입니다.</summary>
public sealed class UIObjectPoolingFactory
{
    private ObjectPoolingFactory<UI_Poolable> m_Factory;
    private UIManager m_Manager;
    private readonly Dictionary<Type, List<GameObject>> m_PrefabsByType =
        new Dictionary<Type, List<GameObject>>();
    private readonly Dictionary<GameObject, PoolPolicy> m_Policies =
        new Dictionary<GameObject, PoolPolicy>();

    public UIObjectPoolingFactory()
    {
        PoolManager pools = PoolManager.Require();
        m_Factory = pools.UIFactory;
    }

    internal UIObjectPoolingFactory(ObjectPoolingFactory<UI_Poolable> factory)
    {
        m_Factory = factory;
    }

    public void BindManager(UIManager manager) => m_Manager = manager;

    public bool Register(GameObject prefab, PoolPolicy policy)
    {
        if (prefab == null || !prefab.TryGetComponent<UI_Poolable>(out UI_Poolable sample))
            return false;
        PoolPolicy normalized = policy.Normalized();
        if (!m_Factory.Register(prefab, normalized)) return false;
        m_Policies[prefab] = normalized;

        Type type = sample.GetType();
        if (!m_PrefabsByType.TryGetValue(type, out List<GameObject> prefabs))
        {
            prefabs = new List<GameObject>();
            m_PrefabsByType.Add(type, prefabs);
        }
        if (!prefabs.Contains(prefab)) prefabs.Add(prefab);
        return true;
    }

    public void Initialize(UIManager manager, IEnumerable<GameObject> poolablePrefabs)
    {
        BindManager(manager);
        if (poolablePrefabs == null) return;
        foreach (GameObject prefab in poolablePrefabs)
        {
            if (prefab == null || !prefab.TryGetComponent<UI_Poolable>(out UI_Poolable sample))
                continue;
            int initial = Mathf.Max(1, sample.poolSize);
            Register(prefab, new PoolPolicy(initial, Mathf.Max(initial, 16), PoolScope.Scene));
        }
    }

    public T Spawn<T>(Vector3 position) where T : UI_Poolable
    {
        if (!m_PrefabsByType.TryGetValue(typeof(T), out List<GameObject> prefabs) || prefabs.Count == 0)
            return null;
        return Spawn<T>(prefabs[0], position);
    }

    public T Spawn<T>(GameObject prefab, Vector3 position) where T : UI_Poolable
    {
        if (prefab == null || !prefab.TryGetComponent<UI_Poolable>(out UI_Poolable metadata))
            return null;
        if (!m_Factory.IsRegistered(prefab))
        {
            if (!m_Policies.TryGetValue(prefab, out PoolPolicy policy) ||
                !m_Factory.Register(prefab, policy))
                return null;
        }
        UI_Root root = m_Manager != null ? m_Manager.GetRootFromType(metadata.GetParent()) : null;
        var context = new PoolSpawnContext(root != null ? root.transform : null, position, Quaternion.identity);
        if (!m_Factory.TryRent(prefab, context, out UI_Poolable item)) return null;
        item.SetRoot(root);
        item.Open();
        return item as T;
    }

    public void Despawn(UI_Poolable pooled)
    {
        if (pooled != null) m_Factory.Return(pooled);
    }

    public void Clear()
    {
        m_Factory.ClearScope(PoolScope.Scene, true);
        m_PrefabsByType.Clear();
        m_Policies.Clear();
    }
}
#endif
