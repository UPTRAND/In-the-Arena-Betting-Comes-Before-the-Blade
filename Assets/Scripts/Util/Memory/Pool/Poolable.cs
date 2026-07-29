#if UNITY_6000_0_OR_NEWER
using UnityEngine;

[DisallowMultipleComponent]
public abstract class Poolable : MonoBehaviour, IPoolLifecycle
{
    [Header("Pool Configuration")]
    [SerializeField, Min(1)] private int m_PoolSize = 10;

    public int poolSize { get => m_PoolSize; set => m_PoolSize = Mathf.Max(1, value); }
    public bool IsSpawned { get; private set; }
    public Transform CachedTransform => transform;

    public abstract void OnSpawn();
    public abstract void OnDespawn();

    public virtual void OnPoolRent(in PoolSpawnContext context)
    {
        IsSpawned = true;
        OnSpawn();
    }

    public virtual void OnPoolReturn()
    {
        OnDespawn();
        IsSpawned = false;
    }

    public virtual void GameObjectSetActive(bool value)
    {
        IsSpawned = value;
        gameObject.SetActive(value);
    }

    public virtual void Despawn()
    {
        PoolMember member = GetComponent<PoolMember>();
        if (member != null && member.ReturnToPool()) return;
        if (!IsSpawned && !gameObject.activeSelf) return;
        OnPoolReturn();
        gameObject.SetActive(false);
    }

    protected virtual void OnDestroy() => IsSpawned = false;
}
#endif
