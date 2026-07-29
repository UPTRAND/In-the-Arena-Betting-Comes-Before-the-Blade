#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PoolMember : MonoBehaviour
{
    private IPoolOwner m_Owner;
    private PoolKey m_Key;
    private IPoolLifecycle[] m_Callbacks = Array.Empty<IPoolLifecycle>();

    public PoolKey Key => m_Key;
    public GameObject SourcePrefab => m_Key.Prefab;
    public bool IsRented { get; private set; }

    internal void Configure(IPoolOwner owner, PoolKey key)
    {
        m_Owner = owner;
        m_Key = key;
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        var callbacks = new List<IPoolLifecycle>(behaviours.Length);
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] is IPoolLifecycle lifecycle) callbacks.Add(lifecycle);
        m_Callbacks = callbacks.ToArray();
        IsRented = false;
    }

    internal bool BeginRent()
    {
        if (IsRented) return false;
        IsRented = true;
        return true;
    }

    internal void InvokeRent(in PoolSpawnContext context)
    {
        for (int i = 0; i < m_Callbacks.Length; i++) m_Callbacks[i].OnPoolRent(context);
    }

    internal void InvokeReturn()
    {
        for (int i = m_Callbacks.Length - 1; i >= 0; i--)
        {
            try { m_Callbacks[i].OnPoolReturn(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    internal void CompleteReturn() => IsRented = false;
    public bool ReturnToPool() => m_Owner != null && m_Owner.Return(this);
}
#endif
