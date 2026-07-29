#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    public static class ProjectilePoolService
    {
        private static readonly Dictionary<GameObject, Stack<Projectile>> Pools =
            new Dictionary<GameObject, Stack<Projectile>>();

        public static Projectile Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return null;
            if (!Pools.TryGetValue(prefab, out Stack<Projectile> pool))
            {
                pool = new Stack<Projectile>(16);
                Pools.Add(prefab, pool);
            }

            Projectile projectile;
            if (pool.Count > 0)
            {
                projectile = pool.Pop();
            }
            else
            {
                GameObject instance = Object.Instantiate(prefab);
                projectile = instance.GetComponent<Projectile>();
                if (projectile == null)
                {
                    Object.Destroy(instance);
                    return null;
                }
                projectile.SetPoolSource(prefab);
            }

            projectile.transform.SetPositionAndRotation(position, Quaternion.identity);
            projectile.gameObject.SetActive(true);
            return projectile;
        }

        public static void Return(Projectile projectile)
        {
            if (projectile == null) return;
            GameObject source = projectile.PoolSource;
            if (source == null)
            {
                Object.Destroy(projectile.gameObject);
                return;
            }

            if (!Pools.TryGetValue(source, out Stack<Projectile> pool))
            {
                pool = new Stack<Projectile>(16);
                Pools.Add(source, pool);
            }

            projectile.ResetRuntime();
            projectile.gameObject.SetActive(false);
            pool.Push(projectile);
        }

        public static void Clear()
        {
            Pools.Clear();
        }
    }
}
#endif
