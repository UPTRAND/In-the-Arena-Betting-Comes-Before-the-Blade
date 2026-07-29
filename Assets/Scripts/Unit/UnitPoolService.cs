#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    public sealed class UnitPoolService
    {
        public const int MaxActiveUnits = 108;
        private readonly ObjectPoolingFactory<Unit> m_Factory;
        private int m_ActiveCount;

        internal UnitPoolService(ObjectPoolingFactory<Unit> factory) => m_Factory = factory;
        public int ActiveCount => m_ActiveCount;

        public bool Prewarm(UnitData data, int count)
        {
            if (data == null || data.UnitPrefab == null || count <= 0 || count > MaxActiveUnits) return false;
            if (!m_Factory.IsRegistered(data.UnitPrefab))
                m_Factory.Register(data.UnitPrefab, new PoolPolicy(0, MaxActiveUnits, PoolScope.Stage));
            return m_Factory.Prewarm(data.UnitPrefab, count);
        }

        public bool TrySpawn(
            UnitData data,
            Transform parent,
            int team,
            Vector3 position,
            bool activate,
            out Unit unit)
        {
            unit = null;
            if (data == null || data.UnitPrefab == null || m_ActiveCount >= MaxActiveUnits) return false;
            if (!m_Factory.IsRegistered(data.UnitPrefab) && !Prewarm(data, 1)) return false;

            var context = new PoolSpawnContext(parent, position, Quaternion.identity, false);
            if (!m_Factory.TryRent(data.UnitPrefab, context, out unit)) return false;
            try
            {
                unit.SetPoolSource(data.UnitPrefab);
                unit.Initialize(data, team);
                unit.gameObject.SetActive(activate);
                m_ActiveCount++;
                return true;
            }
            catch
            {
                m_Factory.Return(unit);
                unit = null;
                throw;
            }
        }

        public Unit Spawn(UnitData data, Transform parent, int team, Vector3 position, bool activate = true)
            => TrySpawn(data, parent, team, position, activate, out Unit unit) ? unit : null;

        public bool Return(Unit unit)
        {
            if (!m_Factory.Return(unit)) return false;
            m_ActiveCount = Mathf.Max(0, m_ActiveCount - 1);
            return true;
        }

        public void ClearStage()
        {
            m_Factory.ClearScope(PoolScope.Stage, true);
            m_ActiveCount = 0;
        }
    }
}
#endif
