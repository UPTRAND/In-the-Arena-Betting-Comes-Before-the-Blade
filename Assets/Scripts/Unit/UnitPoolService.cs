#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 전투 중 Instantiate/Destroy를 제거하기 위한 UnitData 기반 풀입니다.
    /// </summary>
    public static class UnitPoolService
    {
        private sealed class Pool
        {
            public readonly Stack<Unit> Available = new Stack<Unit>();
            public Transform Root;
        }

        private static readonly Dictionary<GameObject, Pool> Pools = new Dictionary<GameObject, Pool>();
        private static Transform s_Root;

        public static void Prewarm(UnitData data, int count)
        {
            if (data == null || data.UnitPrefab == null || count <= 0) return;
            Pool pool = GetOrCreatePool(data.UnitPrefab);
            int needed = count - pool.Available.Count;
            for (int i = 0; i < needed; i++)
            {
                pool.Available.Push(Create(data.UnitPrefab, pool));
            }
        }

        public static Unit Spawn(
            UnitData data,
            Transform parent,
            int team,
            Vector3 position,
            bool activate = true)
        {
            if (data == null || data.UnitPrefab == null) return null;

            Pool pool = GetOrCreatePool(data.UnitPrefab);
            Unit unit = pool.Available.Count > 0 ? pool.Available.Pop() : Create(data.UnitPrefab, pool);
            Transform unitTransform = unit.transform;
            unitTransform.SetParent(parent, false);
            unitTransform.SetPositionAndRotation(position, Quaternion.identity);
            unit.Initialize(data, team);
            unit.gameObject.SetActive(activate);
            return unit;
        }

        public static void Return(Unit unit)
        {
            if (unit == null) return;
            GameObject source = unit.PoolSource;
            if (source == null)
            {
                Object.Destroy(unit.gameObject);
                return;
            }

            Pool pool = GetOrCreatePool(source);
            unit.PrepareForPool();
            unit.transform.SetParent(pool.Root, false);
            unit.gameObject.SetActive(false);
            if (!pool.Available.Contains(unit)) pool.Available.Push(unit);
        }

        public static void Clear()
        {
            Pools.Clear();
            if (s_Root != null) Object.Destroy(s_Root.gameObject);
            s_Root = null;
        }

        private static Pool GetOrCreatePool(GameObject prefab)
        {
            if (Pools.TryGetValue(prefab, out Pool pool))
            {
                if (pool.Root != null) return pool;
                Pools.Remove(prefab);
            }

            EnsureRoot();
            var root = new GameObject(prefab.name + "_Pool").transform;
            root.SetParent(s_Root, false);
            pool = new Pool { Root = root };
            Pools.Add(prefab, pool);
            return pool;
        }

        private static Unit Create(GameObject prefab, Pool pool)
        {
            GameObject instance = Object.Instantiate(prefab, pool.Root);
            Unit unit = instance.GetComponent<Unit>();
            if (unit == null)
            {
                Object.Destroy(instance);
                return null;
            }

            unit.SetPoolSource(prefab);
            instance.SetActive(false);
            return unit;
        }

        private static void EnsureRoot()
        {
            if (s_Root != null) return;
            var rootObject = new GameObject("[UnitPool]");
            s_Root = rootObject.transform;
        }
    }
}
#endif
