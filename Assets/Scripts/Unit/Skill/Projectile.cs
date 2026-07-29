#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    public interface IProjectileImpactResolver
    {
        bool ApplyImpact(
            in ProjectileImpactPayload payload,
            Unit primaryTarget,
            Vector3 impactPosition);
    }

    public readonly struct ProjectileImpactPayload
    {
        public readonly UnitHandle Source;
        public readonly int SourceTeam;
        public readonly float Damage;
        public readonly bool IsCritical;
        public readonly bool IsSkill;
        public readonly bool IsReaction;
        private readonly IProjectileImpactResolver m_Resolver;

        public ProjectileImpactPayload(
            UnitHandle source,
            int sourceTeam,
            float damage,
            bool isCritical,
            bool isSkill,
            bool isReaction,
            IProjectileImpactResolver resolver)
        {
            Source = source;
            SourceTeam = sourceTeam;
            Damage = Mathf.Max(0f, damage);
            IsCritical = isCritical;
            IsSkill = isSkill;
            IsReaction = isReaction;
            m_Resolver = resolver;
        }

        public bool Apply(Unit primaryTarget, Vector3 impactPosition)
            => primaryTarget != null && !primaryTarget.IsDead && m_Resolver != null &&
               m_Resolver.ApplyImpact(in this, primaryTarget, impactPosition);
    }

    internal enum ProjectileSimulationState
    {
        Inactive = 0,
        Flying = 1,
        ImpactPresentation = 2
    }

    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour, IPoolLifecycle
    {
        private const float DefaultHitDistance = 0.25f;

        [SerializeField] private Animator m_Animator;

        private UnitHandle m_Target;
        private ProjectileImpactPayload m_Payload;
        private float m_Speed;
        private float m_HitDistance;
        private float m_RemainingLifetime;
        private float m_ImpactPresentationRemaining;
        private ProjectileOrientationMode m_OrientationMode;
        private ProjectileSimulationState m_State;
        private Transform m_CameraTransform;

        private void Awake()
        {
            if (m_Animator == null) m_Animator = GetComponentInChildren<Animator>(true);
        }

        internal void Initialize(
            UnitHandle target,
            in ProjectileImpactPayload payload,
            ProjectileData data)
        {
            m_Target = target;
            m_Payload = payload;
            m_Speed = data != null ? data.Speed : 20f;
            m_HitDistance = data != null ? data.HitDistance : DefaultHitDistance;
            m_RemainingLifetime = data != null ? data.Lifetime : 5f;
            m_OrientationMode = data != null
                ? data.OrientationMode
                : ProjectileOrientationMode.FullBillboard;
            m_ImpactPresentationRemaining = 0f;
            m_State = ProjectileSimulationState.Flying;
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            m_CameraTransform = mainCamera != null ? mainCamera.transform : null;

            if (data != null) TryPlayAnimatorState(data.FlightStateName);
            Unit targetUnit = target.Unit;
            if (targetUnit != null)
                ApplyOrientation(targetUnit.HitPosition - transform.position);
        }

        internal void Initialize(
            UnitHandle target,
            in ProjectileImpactPayload payload,
            float speed,
            float lifetime)
        {
            m_Target = target;
            m_Payload = payload;
            m_Speed = Mathf.Max(0.1f, speed);
            m_HitDistance = DefaultHitDistance;
            m_RemainingLifetime = Mathf.Max(0.1f, lifetime);
            m_OrientationMode = ProjectileOrientationMode.FullBillboard;
            m_ImpactPresentationRemaining = 0f;
            m_State = ProjectileSimulationState.Flying;
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            m_CameraTransform = mainCamera != null ? mainCamera.transform : null;

            Unit targetUnit = target.Unit;
            if (targetUnit != null)
                ApplyOrientation(targetUnit.HitPosition - transform.position);
        }

        internal bool SimulationFrame(float deltaTime, ProjectileData data)
        {
            if (m_State == ProjectileSimulationState.ImpactPresentation)
            {
                m_ImpactPresentationRemaining -= deltaTime;
                return m_ImpactPresentationRemaining > 0f;
            }

            if (m_State != ProjectileSimulationState.Flying) return false;

            Unit target = m_Target.Unit;
            if (target == null || target.IsDead) return false;

            m_RemainingLifetime -= deltaTime;
            if (m_RemainingLifetime <= 0f) return false;

            Vector3 destination = target.HitPosition;
            Vector3 offset = destination - transform.position;
            float distanceSqr = offset.sqrMagnitude;
            float step = m_Speed * deltaTime;
            float arrivalDistance = Mathf.Max(m_HitDistance, step);
            if (distanceSqr <= arrivalDistance * arrivalDistance)
            {
                transform.position = destination;
                m_Payload.Apply(target, destination);
                return BeginImpactPresentation(data);
            }

            float distance = Mathf.Sqrt(distanceSqr);
            Vector3 direction = offset / distance;
            transform.position += direction * step;
            ApplyOrientation(direction);
            return true;
        }

        private bool BeginImpactPresentation(ProjectileData data)
        {
            string stateName = data != null ? data.ImpactStateName : null;
            float duration = data != null ? data.ImpactPresentationDuration : 0f;
            if (duration <= 0f || !TryPlayAnimatorState(stateName)) return false;

            m_State = ProjectileSimulationState.ImpactPresentation;
            m_ImpactPresentationRemaining = duration;
            return true;
        }

        private void ApplyOrientation(Vector3 velocity)
        {
            if (m_OrientationMode == ProjectileOrientationMode.Fixed) return;

            if (m_CameraTransform == null)
            {
                UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
                m_CameraTransform = mainCamera != null ? mainCamera.transform : null;
            }

            if (m_CameraTransform == null)
            {
                if (velocity.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                return;
            }

            Quaternion cameraRotation = m_CameraTransform.rotation;
            if (m_OrientationMode == ProjectileOrientationMode.FullBillboard ||
                velocity.sqrMagnitude <= 0.0001f)
            {
                transform.rotation = cameraRotation;
                return;
            }

            Vector3 direction = velocity.normalized;
            float horizontal = Vector3.Dot(direction, m_CameraTransform.right);
            float vertical = Vector3.Dot(direction, m_CameraTransform.up);
            float angle = Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg;
            transform.rotation = cameraRotation * Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private bool TryPlayAnimatorState(string stateName)
        {
            if (m_Animator == null || string.IsNullOrWhiteSpace(stateName)) return false;
            int shortHash = Animator.StringToHash(stateName);
            int fullHash = Animator.StringToHash($"Base Layer.{stateName}");
            int stateHash;
            if (m_Animator.HasState(0, shortHash)) stateHash = shortHash;
            else if (m_Animator.HasState(0, fullHash)) stateHash = fullHash;
            else return false;
            m_Animator.Play(stateHash, 0, 0f);
            return true;
        }

        public void OnPoolRent(in PoolSpawnContext context)
        {
            if (m_Animator == null) m_Animator = GetComponentInChildren<Animator>(true);
        }

        public void OnPoolReturn()
        {
            m_Target = default;
            m_Payload = default;
            m_Speed = 0f;
            m_HitDistance = 0f;
            m_RemainingLifetime = 0f;
            m_ImpactPresentationRemaining = 0f;
            m_OrientationMode = ProjectileOrientationMode.Fixed;
            m_State = ProjectileSimulationState.Inactive;
            m_CameraTransform = null;
        }
    }
}
#endif
