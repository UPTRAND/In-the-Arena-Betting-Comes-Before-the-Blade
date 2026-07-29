#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

public enum PoolDomain { UI, Projectile }

[CreateAssetMenu(fileName = "PoolCatalog", menuName = "In The Arena/Pooling/Pool Catalog")]
public sealed class PoolCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public PoolDomain Domain;
        public GameObject Prefab;
        public PoolPolicy Policy = new PoolPolicy(0, 32, PoolScope.Scene);
    }

    [SerializeField] private List<Entry> m_Entries = new List<Entry>();
    public IReadOnlyList<Entry> Entries => m_Entries;
}
#endif
