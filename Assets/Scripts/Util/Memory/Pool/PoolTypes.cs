#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

public enum PoolScope { Persistent, Scene, Stage, Round }

[Serializable]
public struct PoolPolicy
{
    [Min(0)] public int InitialCapacity;
    [Min(1)] public int MaxCapacity;
    public PoolScope Scope;
    public bool ResetTransformOnReturn;

    public PoolPolicy(int initialCapacity, int maxCapacity, PoolScope scope, bool resetTransformOnReturn = true)
    {
        InitialCapacity = Mathf.Max(0, initialCapacity);
        MaxCapacity = Mathf.Max(1, maxCapacity);
        Scope = scope;
        ResetTransformOnReturn = resetTransformOnReturn;
    }

    public PoolPolicy Normalized()
    {
        int max = Mathf.Max(1, MaxCapacity);
        return new PoolPolicy(Mathf.Clamp(InitialCapacity, 0, max), max, Scope, ResetTransformOnReturn);
    }

    public static PoolPolicy Default(PoolScope scope = PoolScope.Scene)
        => new PoolPolicy(0, 32, scope, true);
}

public readonly struct PoolKey : IEquatable<PoolKey>
{
    public GameObject Prefab { get; }
    public Type ComponentType { get; }

    public PoolKey(GameObject prefab, Type componentType)
    {
        Prefab = prefab;
        ComponentType = componentType;
    }

    public bool IsValid => Prefab != null && ComponentType != null;
    public string DisplayName => IsValid ? $"{Prefab.name}<{ComponentType.Name}>" : "InvalidPoolKey";
    public bool Equals(PoolKey other) => Prefab == other.Prefab && ComponentType == other.ComponentType;
    public override bool Equals(object obj) => obj is PoolKey other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            return ((Prefab != null ? Prefab.GetHashCode() : 0) * 397) ^
                   (ComponentType != null ? ComponentType.GetHashCode() : 0);
        }
    }
}

public readonly struct PoolSpawnContext
{
    public Transform Parent { get; }
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public bool Activate { get; }

    public PoolSpawnContext(Transform parent, Vector3 position, Quaternion rotation, bool activate = true)
    {
        Parent = parent;
        Position = position;
        Rotation = rotation;
        Activate = activate;
    }

    public static PoolSpawnContext At(Vector3 position, Transform parent = null, bool activate = true)
        => new PoolSpawnContext(parent, position, Quaternion.identity, activate);
}

public readonly struct PoolStats
{
    public int Created { get; }
    public int InUse { get; }
    public int Available { get; }
    public int PeakInUse { get; }
    public int FailedRentCount { get; }

    public PoolStats(int created, int inUse, int available, int peakInUse, int failedRentCount)
    {
        Created = created;
        InUse = inUse;
        Available = available;
        PeakInUse = peakInUse;
        FailedRentCount = failedRentCount;
    }
}

public interface IPoolLifecycle
{
    void OnPoolRent(in PoolSpawnContext context);
    void OnPoolReturn();
}

internal interface IPoolOwner
{
    bool Return(PoolMember member);
}
#endif
