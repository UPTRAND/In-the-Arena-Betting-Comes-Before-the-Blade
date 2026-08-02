#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// Deterministic, allocation-free approach positions around a retained target.
    /// </summary>
    public static class EngagementSlotSystem
    {
        private const int Capacity = UnitSpatialIndex.MaxUnits;
        private const int SlotsPerRing = 8;
        public const float ContactPadding = 0.05f;
        public const float ArrivalTolerance = 0.05f;
        public const float DistanceEpsilon = 0.001f;
        private static readonly Unit[] Owners = new Unit[Capacity];
        private static readonly UnitHandle[] Targets = new UnitHandle[Capacity];
        private static readonly int[] Slots = new int[Capacity];

        public static Vector3 GetPosition(Unit owner, Unit target)
        {
            if (owner == null || target == null) return target != null ? target.GroundPosition : default;

            int reservation = FindOwner(owner);
            UnitHandle targetHandle = new UnitHandle(target);
            if (reservation < 0)
            {
                reservation = FindFree();
                if (reservation < 0) return target.GroundPosition;
                Owners[reservation] = owner;
            }

            if (!Targets[reservation].Equals(targetHandle))
            {
                Targets[reservation] = targetHandle;
                Slots[reservation] = FindAvailableSlot(targetHandle, owner, target);
            }

            int slot = Slots[reservation];
            int ring = slot / SlotsPerRing;
            int positionInRing = slot % SlotsPerRing;
            float angle = positionInRing * (Mathf.PI * 2f / SlotsPerRing) +
                          (ring & 1) * (Mathf.PI / SlotsPerRing);
            bool ranged = owner.CurrentBasicAttackData?.Delivery is HomingProjectileAttackDelivery;
            float baseRadius = ranged
                ? Mathf.Max(GetContactDistance(owner, target), owner.CurrentAttackRange * 0.85f)
                : GetContactDistance(owner, target);
            float ownerRadius = owner.UnitData != null ? owner.UnitData.VisualRadius : 0.5f;
            float radius = baseRadius + ring * Mathf.Max(0.2f, ownerRadius * 1.5f);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            return target.GroundPosition + offset;
        }

        public static void Release(Unit owner)
        {
            int index = FindOwner(owner);
            if (index < 0) return;
            Owners[index] = null;
            Targets[index] = default;
            Slots[index] = 0;
        }

        public static void ReleaseTarget(Unit target)
        {
            if (target == null) return;
            UnitHandle handle = new UnitHandle(target);
            for (int i = 0; i < Capacity; i++)
            {
                if (Owners[i] == null || !Targets[i].Equals(handle)) continue;
                Owners[i] = null;
                Targets[i] = default;
                Slots[i] = 0;
            }
        }

        public static void Clear()
        {
            for (int i = 0; i < Capacity; i++)
            {
                Owners[i] = null;
                Targets[i] = default;
                Slots[i] = 0;
            }
        }

        public static float GetContactDistance(Unit owner, Unit target)
        {
            float ownerRadius = owner?.UnitData != null ? owner.UnitData.VisualRadius : 0.5f;
            float targetRadius = target?.UnitData != null ? target.UnitData.VisualRadius : 0.5f;
            return ownerRadius + targetRadius + ContactPadding;
        }

        private static int FindAvailableSlot(UnitHandle target, Unit owner, Unit targetUnit)
        {
            int angularStart = GetPreferredAngularSlot(owner, targetUnit);
            bool searchClockwiseFirst = (owner.InstanceId & 1) == 0;
            int ringCount = Mathf.CeilToInt(Capacity / (float)SlotsPerRing);
            for (int ring = 0; ring < ringCount; ring++)
            {
                for (int angularOffset = 0; angularOffset < SlotsPerRing; angularOffset++)
                {
                    int signedOffset = GetSymmetricOffset(
                        angularOffset,
                        searchClockwiseFirst);
                    int positionInRing =
                        (angularStart + signedOffset + SlotsPerRing) % SlotsPerRing;
                    int candidate = ring * SlotsPerRing + positionInRing;
                    if (candidate >= Capacity) break;

                    bool occupied = false;
                    for (int i = 0; i < Capacity; i++)
                    {
                        if (Owners[i] != null && Targets[i].Equals(target) && Slots[i] == candidate)
                        {
                            occupied = true;
                            break;
                        }
                    }
                    if (!occupied) return candidate;
                }
            }
            return angularStart;
        }

        private static int GetPreferredAngularSlot(Unit owner, Unit target)
        {
            Vector3 approach = owner.GroundPosition - target.GroundPosition;
            approach.y = 0f;
            if (approach.sqrMagnitude <= 0.0001f)
                return (owner.InstanceId & int.MaxValue) % SlotsPerRing;

            float angle = Mathf.Atan2(approach.z, approach.x);
            if (angle < 0f) angle += Mathf.PI * 2f;
            float anglePerSlot = Mathf.PI * 2f / SlotsPerRing;
            return Mathf.RoundToInt(angle / anglePerSlot) % SlotsPerRing;
        }

        private static int GetSymmetricOffset(int searchIndex, bool clockwiseFirst)
        {
            if (searchIndex == 0) return 0;
            int magnitude = (searchIndex + 1) / 2;
            bool clockwise = (searchIndex & 1) == 1
                ? clockwiseFirst
                : !clockwiseFirst;
            return clockwise ? magnitude : -magnitude;
        }

        private static int FindOwner(Unit owner)
        {
            for (int i = 0; i < Capacity; i++)
                if (Owners[i] == owner) return i;
            return -1;
        }

        private static int FindFree()
        {
            for (int i = 0; i < Capacity; i++)
                if (Owners[i] == null) return i;
            return -1;
        }
    }
}
#endif
