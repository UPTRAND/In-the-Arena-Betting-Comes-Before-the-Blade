#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InTheArena.Unit
{
    [DisallowMultipleComponent]
    public sealed class SkillVfxPresenter : MonoBehaviour
    {
        private const float MaximumLifetime = 10f;
        private const int TargetSortingOrderOffset = 10;

        private static SkillVfxPresenter s_Instance;

        private readonly List<ActiveVfx> m_ActiveVfx = new List<ActiveVfx>(64);

        public static SkillVfxPresenter EnsureExists(Transform parent)
        {
            if (s_Instance != null) return s_Instance;

            var gameObject = new GameObject("[SkillVfxPresenter]");
            if (parent != null) gameObject.transform.SetParent(parent, false);
            s_Instance = gameObject.AddComponent<SkillVfxPresenter>();
            return s_Instance;
        }

        public static void ClearAllActive()
        {
            if (s_Instance == null) return;
            s_Instance.ClearAll();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
        }

        private void OnEnable()
        {
            SkillVfxRequestBus.Requested += OnVfxRequested;
        }

        private void OnDisable()
        {
            SkillVfxRequestBus.Requested -= OnVfxRequested;
        }

        private void Update()
        {
            for (int i = m_ActiveVfx.Count - 1; i >= 0; i--)
            {
                ActiveVfx active = m_ActiveVfx[i];
                if (active.GameObject == null)
                {
                    m_ActiveVfx.RemoveAt(i);
                    continue;
                }

                active.Elapsed += Time.deltaTime;
                if (active.ShouldStopBecauseFollowTargetDied() ||
                    active.HasDurationExpired() ||
                    active.Elapsed >= MaximumLifetime ||
                    (!active.HasCustomDuration() &&
                     !HasLivingParticles(active.Particles) &&
                     !HasPlayingAnimators(active.Animators)))
                {
                    StopEffects(active.Particles, active.Animators);
                    active.GameObject.SetActive(false);
                    Destroy(active.GameObject);
                    m_ActiveVfx.RemoveAt(i);
                    continue;
                }

                active.UpdateFollowPosition();
                m_ActiveVfx[i] = active;
            }
        }

        private static void StopEffects(ParticleSystem[] particles, Animator[] animators)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null) continue;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null) continue;
                animator.enabled = false;
            }
        }

        private void ClearAll()
        {
            for (int i = m_ActiveVfx.Count - 1; i >= 0; i--)
            {
                ActiveVfx active = m_ActiveVfx[i];
                StopEffects(active.Particles, active.Animators);
                if (active.GameObject != null)
                {
                    active.GameObject.SetActive(false);
                    Destroy(active.GameObject);
                }
            }

            m_ActiveVfx.Clear();
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;

            SkillVfxRequestBus.Requested -= OnVfxRequested;
            ClearAll();
        }

        private void OnVfxRequested(SkillVfxRequest request)
        {
            if (request.Prefab == null)
                return;

            Quaternion rotation = request.Prefab.transform.rotation;
            GameObject instance = Instantiate(request.Prefab, request.Position, rotation, transform);
            if (instance == null)
                return;

            ApplyScale(instance, request.Scale);
            instance.SetActive(true);
            ApplyTargetSorting(instance, request.Target.Unit);
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            PlayEffects(particles, animators);
            m_ActiveVfx.Add(new ActiveVfx(
                instance,
                particles,
                animators,
                request.Target,
                ResolveFollowOffset(request.Target.Unit, request.Position),
                request.Duration));
        }

        private static void ApplyScale(GameObject instance, float scale)
        {
            if (instance == null || Mathf.Approximately(scale, 1f))
                return;

            instance.transform.localScale *= Mathf.Max(0f, scale);
        }

        private static void ApplyTargetSorting(GameObject instance, Unit target)
        {
            if (instance == null || target == null) return;

            Transform targetRoot = target.VisualRoot != null ? target.VisualRoot : target.transform;
            Renderer[] targetRenderers = targetRoot.GetComponentsInChildren<Renderer>(true);
            SortingGroup[] targetSortingGroups = targetRoot.GetComponentsInChildren<SortingGroup>(true);

            int targetSortingLayerId = 0;
            int targetSortingOrder = int.MinValue;
            bool foundTargetRenderer = false;
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null) continue;
                if (foundTargetRenderer && renderer.sortingOrder < targetSortingOrder) continue;

                targetSortingLayerId = renderer.sortingLayerID;
                targetSortingOrder = renderer.sortingOrder;
                foundTargetRenderer = true;
            }

            for (int i = 0; i < targetSortingGroups.Length; i++)
            {
                SortingGroup sortingGroup = targetSortingGroups[i];
                if (sortingGroup == null) continue;
                if (foundTargetRenderer && sortingGroup.sortingOrder < targetSortingOrder) continue;

                targetSortingLayerId = sortingGroup.sortingLayerID;
                targetSortingOrder = sortingGroup.sortingOrder;
                foundTargetRenderer = true;
            }
            if (!foundTargetRenderer) return;

            SortingGroup vfxSortingGroup = instance.GetComponent<SortingGroup>();
            if (vfxSortingGroup == null)
                vfxSortingGroup = instance.AddComponent<SortingGroup>();

            vfxSortingGroup.sortingLayerID = targetSortingLayerId;
            vfxSortingGroup.sortingOrder = targetSortingOrder + TargetSortingOrderOffset;
        }

        private static void PlayEffects(ParticleSystem[] particles, Animator[] animators)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null) continue;
                particle.gameObject.SetActive(true);
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = particle.main;
                main.loop = false;
                main.prewarm = false;
                main.startDelay = 0f;
                particle.Play(true);
                if (ShouldEmitFallbackParticle(particle))
                    particle.Emit(1);
            }

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null) continue;
                animator.gameObject.SetActive(true);
                animator.Rebind();
                animator.Update(0f);
            }
        }

        private static bool ShouldEmitFallbackParticle(ParticleSystem particle)
        {
            ParticleSystem.EmissionModule emission = particle.emission;
            if (!emission.enabled || emission.burstCount > 0)
                return false;

            ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
            return rate.constantMax > 0f;
        }

        private static Vector3 ResolveFollowOffset(Unit target, Vector3 spawnPosition)
        {
            return target != null
                ? spawnPosition - target.HitPosition
                : Vector3.zero;
        }

        private static bool HasLivingParticles(ParticleSystem[] particles)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null) continue;
                if (particle.IsAlive(true)) return true;
            }
            return false;
        }

        private static bool HasPlayingAnimators(Animator[] animators)
        {
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || !animator.isActiveAndEnabled ||
                    animator.runtimeAnimatorController == null)
                    continue;

                for (int layer = 0; layer < animator.layerCount; layer++)
                {
                    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                    if (animator.IsInTransition(layer)) return true;
                    if (state.length > 0f && state.normalizedTime < 1f) return true;
                }
            }
            return false;
        }

        private struct ActiveVfx
        {
            public readonly GameObject GameObject;
            public readonly ParticleSystem[] Particles;
            public readonly Animator[] Animators;
            public readonly UnitHandle FollowTarget;
            public readonly bool HasFollowTarget;
            public readonly Vector3 FollowOffset;
            public readonly float Duration;
            public float Elapsed;

            public ActiveVfx(
                GameObject gameObject,
                ParticleSystem[] particles,
                Animator[] animators,
                UnitHandle followTarget,
                Vector3 followOffset,
                float duration)
            {
                GameObject = gameObject;
                Particles = particles;
                Animators = animators;
                FollowTarget = followTarget;
                HasFollowTarget = followTarget.Unit != null;
                FollowOffset = followOffset;
                Duration = Mathf.Max(0f, duration);
                Elapsed = 0f;
            }

            public bool HasDurationExpired()
            {
                return Duration > 0f && Elapsed >= Duration;
            }

            public bool HasCustomDuration()
            {
                return Duration > 0f;
            }

            public bool ShouldStopBecauseFollowTargetDied()
            {
                Unit target = FollowTarget.Unit;
                return HasFollowTarget && (target == null || target.IsDead);
            }

            public void UpdateFollowPosition()
            {
                Unit target = FollowTarget.Unit;
                if (GameObject == null || target == null || target.IsDead)
                    return;

                GameObject.transform.position = target.HitPosition + FollowOffset;
            }
        }
    }
}
#endif
