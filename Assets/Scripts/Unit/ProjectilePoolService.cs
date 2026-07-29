#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    public sealed class ProjectilePoolService
    {
        private const int DefaultInitialCapacity = 16;
        private const int DefaultMaxCapacity = 128;
        private readonly ObjectPoolingFactory<Projectile> m_Factory;
        private readonly Dictionary<GameObject, PoolPolicy> m_Policies =
            new Dictionary<GameObject, PoolPolicy>();
        private readonly List<Projectile> m_Active = new List<Projectile>(DefaultMaxCapacity);
        private readonly Dictionary<Projectile, ProjectileData> m_RuntimeData =
            new Dictionary<Projectile, ProjectileData>(DefaultMaxCapacity);

        internal ProjectilePoolService(ObjectPoolingFactory<Projectile> factory) => m_Factory = factory;

        public bool Register(GameObject prefab, PoolPolicy policy)
        {
            if (prefab == null) return false;
            PoolPolicy normalized = policy.Normalized();
            m_Policies[prefab] = normalized;
            return m_Factory.Register(prefab, normalized);
        }

        public bool Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return false;
            EnsureRegistered(prefab);
            return m_Factory.Prewarm(prefab, count);
        }

        public bool TrySpawn(GameObject prefab, Vector3 position, out Projectile projectile)
        {
            projectile = null;
            if (prefab == null) return false;
            EnsureRegistered(prefab);
            if (!m_Factory.TryRent(prefab, PoolSpawnContext.At(position), out projectile))
                return false;
            m_Active.Add(projectile);
            return true;
        }

        public bool TrySpawn(
            GameObject prefab,
            Vector3 position,
            UnitHandle target,
            in ProjectileImpactPayload payload,
            float speed,
            float lifetime,
            out Projectile projectile)
        {
            if (!TrySpawn(prefab, position, out projectile)) return false;
            projectile.Initialize(target, payload, speed, lifetime);
            return true;
        }

        public bool TrySpawn(
            ProjectileData data,
            Vector3 position,
            UnitHandle target,
            in ProjectileImpactPayload payload,
            out Projectile projectile)
        {
            projectile = null;
            if (data == null || data.Prefab == null) return false;
            if (!TrySpawn(data.Prefab, position, out projectile)) return false;
            m_RuntimeData[projectile] = data;
            projectile.Initialize(target, payload, data);
            return true;
        }

        public bool Return(Projectile projectile)
        {
            if (projectile == null) return false;
            m_Active.Remove(projectile);
            m_RuntimeData.Remove(projectile);
            return m_Factory.Return(projectile);
        }

        internal void SimulationFrame(float deltaTime)
        {
            for (int i = m_Active.Count - 1; i >= 0; i--)
            {
                Projectile projectile = m_Active[i];
                ProjectileData data = null;
                if (projectile != null) m_RuntimeData.TryGetValue(projectile, out data);
                if (projectile != null && projectile.SimulationFrame(deltaTime, data)) continue;
                m_Active.RemoveAt(i);
                if (projectile != null) m_RuntimeData.Remove(projectile);
                if (projectile != null) m_Factory.Return(projectile);
            }
        }

        public void ClearRound()
        {
            for (int i = m_Active.Count - 1; i >= 0; i--)
            {
                Projectile projectile = m_Active[i];
                if (projectile != null) m_Factory.Return(projectile);
            }
            m_Active.Clear();
            m_RuntimeData.Clear();
        }

        public void ClearStage()
        {
            ClearRound();
            m_Factory.ClearScope(PoolScope.Stage, true);
        }

        private void EnsureRegistered(GameObject prefab)
        {
            if (m_Factory.IsRegistered(prefab)) return;
            if (!m_Policies.TryGetValue(prefab, out PoolPolicy policy))
            {
                policy = new PoolPolicy(
                    DefaultInitialCapacity,
                    DefaultMaxCapacity,
                    PoolScope.Stage);
                m_Policies.Add(prefab, policy);
            }
            m_Factory.Register(prefab, policy);
        }
    }
}
#endif
