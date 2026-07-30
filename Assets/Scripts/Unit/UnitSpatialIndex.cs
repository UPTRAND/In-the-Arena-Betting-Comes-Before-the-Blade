#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// Allocation-free uniform grid shared by targeting and local separation.
    /// Rebuilt once at the beginning of every simulation tick.
    /// </summary>
    public sealed class UnitSpatialIndex
    {
        public const float CellSize = 1f;
        public const int BucketCount = 256;
        public const int MaxUnits = 108;

        private readonly int[] m_Heads = new int[BucketCount];
        private readonly int[] m_Next = new int[MaxUnits];
        private readonly int[] m_CellX = new int[MaxUnits];
        private readonly int[] m_CellZ = new int[MaxUnits];
        private readonly Unit[] m_Units = new Unit[MaxUnits];
        private int m_Count;
        private int m_MinCellX;
        private int m_MaxCellX;
        private int m_MinCellZ;
        private int m_MaxCellZ;

        public int Count => m_Count;

        public UnitSpatialIndex()
        {
            ClearHeads();
        }

        public void Rebuild(IReadOnlyList<Unit> red, IReadOnlyList<Unit> blue)
        {
            ClearHeads();
            m_Count = 0;
            AddTeam(red);
            AddTeam(blue);
        }

        public Unit FindNearestEnemy(Unit owner, float maxDistance)
        {
            if (owner == null || m_Count == 0) return null;

            Vector3 origin = owner.SimulationPosition;
            int originX = ToCell(origin.x);
            int originZ = ToCell(origin.z);
            int maxRing = maxDistance > 0f
                ? Mathf.CeilToInt(maxDistance / CellSize)
                : Mathf.Max(
                    Mathf.Max(Mathf.Abs(originX - m_MinCellX), Mathf.Abs(originX - m_MaxCellX)),
                    Mathf.Max(Mathf.Abs(originZ - m_MinCellZ), Mathf.Abs(originZ - m_MaxCellZ)));
            float bestDistanceSqr = maxDistance > 0f
                ? maxDistance * maxDistance
                : float.MaxValue;
            Unit best = null;

            for (int ring = 0; ring <= maxRing; ring++)
            {
                VisitRing(owner, origin, originX, originZ, ring, ref best, ref bestDistanceSqr);
                float unvisitedLowerBound = ring * CellSize;
                if (best != null && unvisitedLowerBound * unvisitedLowerBound > bestDistanceSqr)
                    break;
            }

            return best;
        }

        public Vector3 CalculateSeparation(Unit owner, float radius)
        {
            if (owner == null || radius <= 0f || m_Count == 0) return Vector3.zero;

            Vector3 origin = owner.SimulationPosition;
            int cellX = ToCell(origin.x);
            int cellZ = ToCell(origin.z);
            int cellRadius = Mathf.Max(1, Mathf.CeilToInt(radius / CellSize));
            float radiusSqr = radius * radius;
            Vector3 result = Vector3.zero;

            for (int z = cellZ - cellRadius; z <= cellZ + cellRadius; z++)
            {
                for (int x = cellX - cellRadius; x <= cellX + cellRadius; x++)
                {
                    int index = m_Heads[Hash(x, z)];
                    while (index >= 0)
                    {
                        Unit other = m_Units[index];
                        if (m_CellX[index] == x && m_CellZ[index] == z &&
                            other != null && other != owner && other.Team == owner.Team &&
                            !other.IsDead && other.gameObject.activeInHierarchy)
                        {
                            Vector3 delta = origin - other.SimulationPosition;
                            delta.y = 0f;
                            float sqr = delta.sqrMagnitude;
                            if (sqr > 0.0001f && sqr < radiusSqr)
                            {
                                float distance = Mathf.Sqrt(sqr);
                                result += delta / distance * (1f - distance / radius);
                            }
                        }
                        index = m_Next[index];
                    }
                }
            }

            return Vector3.ClampMagnitude(result, 1f);
        }

        public int CollectEnemiesInRadius(
            int sourceTeam,
            Vector3 position,
            float radius,
            Unit[] output)
        {
            if (radius <= 0f || output == null || output.Length == 0) return 0;

            int centerX = ToCell(position.x);
            int centerZ = ToCell(position.z);
            int cellRadius = Mathf.Max(1, Mathf.CeilToInt(radius / CellSize));
            float radiusSqr = radius * radius;
            int count = 0;
            for (int z = centerZ - cellRadius; z <= centerZ + cellRadius; z++)
            {
                for (int x = centerX - cellRadius; x <= centerX + cellRadius; x++)
                {
                    int index = m_Heads[Hash(x, z)];
                    while (index >= 0)
                    {
                        Unit candidate = m_Units[index];
                        if (m_CellX[index] == x && m_CellZ[index] == z &&
                            candidate != null && candidate.Team != sourceTeam &&
                            !candidate.IsDead && candidate.gameObject.activeInHierarchy)
                        {
                            Vector3 delta = candidate.SimulationPosition - position;
                            delta.y = 0f;
                            if (delta.sqrMagnitude <= radiusSqr)
                            {
                                if (count >= output.Length) return count;
                                output[count++] = candidate;
                            }
                        }
                        index = m_Next[index];
                    }
                }
            }
            return count;
        }

        private void AddTeam(IReadOnlyList<Unit> team)
        {
            for (int i = 0; i < team.Count && m_Count < MaxUnits; i++)
            {
                Unit unit = team[i];
                if (unit == null || unit.IsDead || !unit.gameObject.activeInHierarchy) continue;

                Vector3 position = unit.SimulationPosition;
                int x = ToCell(position.x);
                int z = ToCell(position.z);
                if (m_Count == 0)
                {
                    m_MinCellX = m_MaxCellX = x;
                    m_MinCellZ = m_MaxCellZ = z;
                }
                else
                {
                    m_MinCellX = Mathf.Min(m_MinCellX, x);
                    m_MaxCellX = Mathf.Max(m_MaxCellX, x);
                    m_MinCellZ = Mathf.Min(m_MinCellZ, z);
                    m_MaxCellZ = Mathf.Max(m_MaxCellZ, z);
                }

                int bucket = Hash(x, z);
                m_Units[m_Count] = unit;
                m_CellX[m_Count] = x;
                m_CellZ[m_Count] = z;
                m_Next[m_Count] = m_Heads[bucket];
                m_Heads[bucket] = m_Count++;
            }
        }

        private void VisitRing(
            Unit owner,
            Vector3 origin,
            int originX,
            int originZ,
            int ring,
            ref Unit best,
            ref float bestDistanceSqr)
        {
            if (ring == 0)
            {
                VisitCell(owner, origin, originX, originZ, ref best, ref bestDistanceSqr);
                return;
            }

            int minX = originX - ring;
            int maxX = originX + ring;
            int minZ = originZ - ring;
            int maxZ = originZ + ring;
            for (int x = minX; x <= maxX; x++)
            {
                VisitCell(owner, origin, x, minZ, ref best, ref bestDistanceSqr);
                VisitCell(owner, origin, x, maxZ, ref best, ref bestDistanceSqr);
            }
            for (int z = minZ + 1; z < maxZ; z++)
            {
                VisitCell(owner, origin, minX, z, ref best, ref bestDistanceSqr);
                VisitCell(owner, origin, maxX, z, ref best, ref bestDistanceSqr);
            }
        }

        private void VisitCell(
            Unit owner,
            Vector3 origin,
            int x,
            int z,
            ref Unit best,
            ref float bestDistanceSqr)
        {
            int index = m_Heads[Hash(x, z)];
            while (index >= 0)
            {
                Unit candidate = m_Units[index];
                if (m_CellX[index] == x && m_CellZ[index] == z &&
                    candidate != null && candidate.Team != owner.Team &&
                    !candidate.IsDead && candidate.gameObject.activeInHierarchy)
                {
                    Vector3 offset = candidate.SimulationPosition - origin;
                    offset.y = 0f;
                    float distanceSqr = offset.sqrMagnitude;
                    if (distanceSqr < bestDistanceSqr ||
                        distanceSqr == bestDistanceSqr && candidate.InstanceId < best.InstanceId)
                    {
                        bestDistanceSqr = distanceSqr;
                        best = candidate;
                    }
                }
                index = m_Next[index];
            }
        }

        private void ClearHeads()
        {
            for (int i = 0; i < m_Heads.Length; i++) m_Heads[i] = -1;
        }

        private static int ToCell(float value) => Mathf.FloorToInt(value / CellSize);

        private static int Hash(int x, int z)
            => unchecked((x * 73856093 ^ z * 19349663) & (BucketCount - 1));
    }
}
#endif
