#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace InTheArena.Camera
{
    /// <summary>
    /// Orthographic 카메라의 줌(orthographicSize)과 위치를 BackgroundCameraSafeArea 로컬 평면에 투영하여 클램핑합니다.
    /// 선형 투영 오프셋을 캐싱하여 매우 빠르고 정확하게 계산하며,
    /// Unity Camera Transform 임시 조작 없이 순수 수학으로 처리합니다.
    ///
    /// [제약 조건]
    /// - Orthographic 제약 적용 시 Camera.orthographic == true여야 합니다.
    /// - lensShift는 기본값이어야 합니다.
    /// - 사용자 정의 projectionMatrix는 지원하지 않습니다.
    ///   이번 시스템 외부에서 camera.projectionMatrix를 설정하지 않는 것을 불변 조건으로 둡니다.
    /// </summary>
    public class CameraBackgroundConstraint
    {
        private float m_UnitMinOffsetX;
        private float m_UnitMaxOffsetX;
        private float m_UnitMinOffsetY;
        private float m_UnitMaxOffsetY;

        private Quaternion m_LastRotation;
        private Rect m_LastViewport;
        private Vector3 m_LastSafeAreaPos;
        private Quaternion m_LastSafeAreaRot;
        private Vector2 m_LastSafeAreaSize;
        private float m_LastCameraAspect;
        private Rect m_LastCameraPixelRect;
        private Vector3 m_LastSafeAreaLossyScale;
        private int m_LastSafeAreaInstanceId;

        private bool m_IsCacheValid;

        // Last Valid Context (Hard Context)
        private CameraPose m_LastValidPose;
        private bool m_HasReportedError = false;
        private bool m_HasReportedMinConflictWarning;

        private UnityCamera m_LastCamera;
        private BackgroundCameraSafeArea m_LastSafeArea;
        private ProjectionMode m_LastProjectionMode;
        private Quaternion m_LastDesiredRotation;
        private Vector3 m_LastSafePos;
        private Quaternion m_LastSafeRot;
        private Vector2 m_LastSafeCenter;
        private bool m_HasLastValidPose;

        // 경계에서 반복 계산을 막기 위한 안전 여백
        private const float SafetyMargin = 0.01f;
        // 최종 검증에서의 오차 허용치
        private const float ContainmentEpsilon = 0.0001f;
        private const float PlaneParallelEpsilon = 0.001f;
        private const float ContextEpsilon = 0.0001f;
        private const float SqrContextEpsilon = ContextEpsilon * ContextEpsilon;
        private const float PivotEpsilon = 0.0001f;

        private static bool NormalizeRange(ref float min, ref float max)
        {
            if (min <= max)
                return true;

            if (min - max > PivotEpsilon)
                return false;

            float midpoint = (min + max) * 0.5f;
            min = midpoint;
            max = midpoint;
            return true;
        }

        public void ResetState()
        {
            m_IsCacheValid = false;
            m_HasLastValidPose = false;
            m_HasReportedError = false;
        }

        public CameraPose ConstrainPose(
            CameraPose desiredPose,
            UnityCamera unityCamera,
            BackgroundCameraSafeArea safeArea,
            Rect viewportRect,
            CameraSettings settings,
            Vector2 localShakePadding)
        {
            if (settings == null || unityCamera == null)
            {
                ReportErrorOnce("CameraSettings 또는 Main Camera가 연결되지 않았습니다.");
                m_HasLastValidPose = false;
                m_IsCacheValid = false;
                return desiredPose;
            }

            if (settings.ProjMode != ProjectionMode.Orthographic)
            {
                m_HasLastValidPose = false;
                return desiredPose;
            }

            if (safeArea == null)
            {
                ReportErrorOnce("BackgroundCameraSafeArea가 연결되지 않았습니다.");
                m_HasLastValidPose = false;
                return desiredPose;
            }

            if (!safeArea.ValidateConfiguration(false))
            {
                ReportErrorOnce("BackgroundCameraSafeArea 설정이 유효하지 않습니다.");
                m_HasLastValidPose = false;
                m_IsCacheValid = false;
                return desiredPose;
            }

            if (!unityCamera.orthographic)
            {
                ReportErrorOnce("Orthographic Camera가 아닙니다.");
                m_HasLastValidPose = false;
                return desiredPose;
            }

            if (unityCamera.lensShift.sqrMagnitude > 0.0001f)
            {
                ReportErrorOnce("Lens Shift는 지원하지 않습니다.");
                m_HasLastValidPose = false;
                return desiredPose;
            }

            float alignment = Mathf.Abs(Vector3.Dot(desiredPose.Rotation * Vector3.forward, safeArea.transform.forward));
            if (alignment <= PlaneParallelEpsilon)
            {
                ReportErrorOnce("카메라 시선과 Safe Area 평면이 거의 평행하여 투영할 수 없습니다.");
                return FallbackPose(desiredPose, unityCamera, safeArea, viewportRect, settings, localShakePadding);
            }

            CheckCacheInvalidation(desiredPose.Rotation, viewportRect, safeArea, unityCamera);

            if (!m_IsCacheValid)
            {
                if (!RecalculateCache(desiredPose.Rotation, viewportRect, safeArea, unityCamera))
                {
                    return FallbackPose(desiredPose, unityCamera, safeArea, viewportRect, settings, localShakePadding, "Cache Calculation Failed");
                }
            }

            Rect effRect = safeArea.GetEffectiveLocalRect(localShakePadding);

            float width = m_UnitMaxOffsetX - m_UnitMinOffsetX;
            float height = m_UnitMaxOffsetY - m_UnitMinOffsetY;

            if (width <= 0f || height <= 0f)
            {
                return FallbackPose(desiredPose, unityCamera, safeArea, viewportRect, settings, localShakePadding, "Invalid Footprint Dimensions");
            }

            float usableWidth = effRect.width - SafetyMargin * 2f;
            float usableHeight = effRect.height - SafetyMargin * 2f;
            float maxAllowed = Mathf.Min(usableWidth / width, usableHeight / height);

            maxAllowed = Mathf.Min(settings.MaxOrthographicSize, maxAllowed);

            if (float.IsNaN(maxAllowed) || float.IsInfinity(maxAllowed) || maxAllowed <= 0f)
            {
                ReportErrorOnce("[CameraBackgroundConstraint] Safe Area에서 유효한 Orthographic 크기를 계산할 수 없습니다.");
                return FallbackPose(desiredPose, unityCamera, safeArea, viewportRect, settings, localShakePadding, "Invalid Maximum Orthographic Size");
            }

            m_HasReportedError = false;

            // 1. Zoom Constraint
            float clampedOrthoSize;
            if (maxAllowed < settings.MinOrthographicSize)
            {
                ReportMinConflictWarningOnce(maxAllowed, settings.MinOrthographicSize);
                clampedOrthoSize = Mathf.Min(Mathf.Max(desiredPose.OrthographicSize, Mathf.Epsilon), maxAllowed);
            }
            else
            {
                m_HasReportedMinConflictWarning = false;
                clampedOrthoSize = Mathf.Clamp(desiredPose.OrthographicSize, settings.MinOrthographicSize, maxAllowed);
            }

            // 2. Position Constraint (Target/Pivot Clamping)
            Plane plane = safeArea.BackgroundPlane;
            Ray forwardRay = new Ray(desiredPose.Position, desiredPose.Rotation * Vector3.forward);
            if (!plane.Raycast(forwardRay, out float centerHit))
                return FallbackPose(desiredPose, unityCamera, safeArea, viewportRect, settings, localShakePadding, "Raycast Failed");

            Vector3 worldTarget = forwardRay.GetPoint(centerHit);
            Vector2 localTarget = safeArea.GetLocalPoint(worldTarget);

            float pivotMinX = effRect.xMin - m_UnitMinOffsetX * clampedOrthoSize + SafetyMargin;
            float pivotMaxX = effRect.xMax - m_UnitMaxOffsetX * clampedOrthoSize - SafetyMargin;
            float pivotMinY = effRect.yMin - m_UnitMinOffsetY * clampedOrthoSize + SafetyMargin;
            float pivotMaxY = effRect.yMax - m_UnitMaxOffsetY * clampedOrthoSize - SafetyMargin;

            if (!NormalizeRange(ref pivotMinX, ref pivotMaxX) ||
                !NormalizeRange(ref pivotMinY, ref pivotMaxY))
            {
                return FallbackPose(desiredPose, unityCamera, safeArea, viewportRect, settings, localShakePadding, "Cannot satisfy both constraints (Pivot Inverse)");
            }

            float clampedTargetX = Mathf.Clamp(localTarget.x, pivotMinX, pivotMaxX);
            float clampedTargetY = Mathf.Clamp(localTarget.y, pivotMinY, pivotMaxY);

            // 보정된 로컬 Target 위치를 월드 좌표로 복원
            Vector3 newWorldTarget = safeArea.GetWorldPoint(new Vector2(clampedTargetX, clampedTargetY));

            // 카메라 거리는 유지한 채 카메라를 뒤로 뺌 (높이/거리 유지)
            Vector3 newCamPos = newWorldTarget - forwardRay.direction * centerHit;
            CameraPose newPose = new CameraPose(newCamPos, desiredPose.Rotation, desiredPose.FieldOfView, clampedOrthoSize);

            // 3. Final Fallback Verification (매 프레임 실제 투영 검사로 Jitter 원천 방지)
            if (VerifyPose(newCamPos, desiredPose.Rotation, clampedOrthoSize, unityCamera, safeArea, viewportRect, effRect))
            {
                SaveLastValidContext(newPose, desiredPose.Rotation, unityCamera, safeArea, settings);
                return newPose;
            }

            return FallbackPose(desiredPose, unityCamera, safeArea, viewportRect, settings, localShakePadding, "VerifyPose failed");
        }

        private void SaveLastValidContext(CameraPose pose, Quaternion desiredRot, UnityCamera cam, BackgroundCameraSafeArea safeArea, CameraSettings settings)
        {
            m_LastValidPose = pose;
            m_HasLastValidPose = true;
            m_LastCamera = cam;
            m_LastSafeArea = safeArea;
            m_LastProjectionMode = settings.ProjMode;
            m_LastDesiredRotation = desiredRot;
            m_LastSafePos = safeArea.transform.position;
            m_LastSafeRot = safeArea.transform.rotation;
            m_LastSafeCenter = safeArea.Center;
        }

        private CameraPose FallbackPose(
            CameraPose desiredPose,
            UnityCamera cam,
            BackgroundCameraSafeArea safeArea,
            Rect vpRect,
            CameraSettings settings,
            Vector2 localShakePadding,
            string reason = "")
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrEmpty(reason))
            {
                // 필요할 때만 기록 (에디터 내 디버깅용)
                UnityEngine.Debug.Log($"[ConstrainPose] Fallback: {reason}");
            }
#endif

            if (!m_HasLastValidPose) return desiredPose;

            // Hard Context 검사
            if (m_LastCamera == cam &&
                m_LastSafeArea == safeArea &&
                m_LastProjectionMode == settings.ProjMode &&
                Quaternion.Angle(m_LastDesiredRotation, desiredPose.Rotation) < ContextEpsilon &&
                (m_LastSafePos - safeArea.transform.position).sqrMagnitude < SqrContextEpsilon &&
                Quaternion.Angle(m_LastSafeRot, safeArea.transform.rotation) < ContextEpsilon &&
                (m_LastSafeCenter - safeArea.Center).sqrMagnitude < SqrContextEpsilon)
            {
                // Soft Geometry (Aspect, PixelRect, Viewport, Size, Padding)는 현재 조건으로 VerifyPose만 통과하면 사용
                Rect effRect = safeArea.GetEffectiveLocalRect(localShakePadding);
                if (VerifyPose(m_LastValidPose.Position, m_LastValidPose.Rotation, m_LastValidPose.OrthographicSize, cam, safeArea, vpRect, effRect))
                {
                    return m_LastValidPose;
                }
            }

            return desiredPose;
        }

        private void ReportErrorOnce(string msg)
        {
            if (!m_HasReportedError)
            {
                Debug.LogError(msg);
                m_HasReportedError = true;
            }
        }

        private void ReportMinConflictWarningOnce(float maxAllowed, float configuredMinimum)
        {
            if (m_HasReportedMinConflictWarning)
                return;

            Debug.LogWarning(
                $"[CameraBackgroundConstraint] Background coverage priority overrides MinOrthographicSize. " +
                $"maxAllowed={maxAllowed:F4}, configuredMinimum={configuredMinimum:F4}.");
            m_HasReportedMinConflictWarning = true;
        }

        private void CheckCacheInvalidation(Quaternion rotation, Rect viewportRect, BackgroundCameraSafeArea safeArea, UnityCamera camera)
        {
            if (m_LastRotation != rotation ||
                m_LastViewport != viewportRect ||
                m_LastSafeAreaPos != safeArea.transform.position ||
                m_LastSafeAreaRot != safeArea.transform.rotation ||
                m_LastSafeAreaSize != safeArea.Size ||
                m_LastSafeArea != safeArea ||
                m_LastSafeAreaLossyScale != safeArea.transform.lossyScale ||
                !Mathf.Approximately(m_LastCameraAspect, camera.aspect) ||
                m_LastCameraPixelRect != camera.pixelRect)
            {
                m_IsCacheValid = false;
            }
        }

        private bool RecalculateCache(Quaternion rotation, Rect viewportRect, BackgroundCameraSafeArea safeArea, UnityCamera camera)
        {
            Plane plane = safeArea.BackgroundPlane;
            Vector3 testCamPos = safeArea.transform.position - rotation * Vector3.forward * 100f;

            float orthoSize = 1f;
            float aspect = camera.aspect;
            float w = 2f * orthoSize * aspect;
            float h = 2f * orthoSize;

            Vector2 minLocal = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maxLocal = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < 4; i++)
            {
                float vx = (i % 2 == 0) ? viewportRect.xMin : viewportRect.xMax;
                float vy = (i / 2 == 0) ? viewportRect.yMin : viewportRect.yMax;

                float localX = (vx - 0.5f) * w;
                float localY = (vy - 0.5f) * h;

                Vector3 worldOrigin = testCamPos + rotation * new Vector3(localX, localY, 0f);
                Ray ray = new Ray(worldOrigin, rotation * Vector3.forward);

                if (plane.Raycast(ray, out float enter))
                {
                    Vector3 worldHit = ray.GetPoint(enter);
                    Vector2 localHit = safeArea.GetLocalPoint(worldHit);
                    minLocal.x = Mathf.Min(minLocal.x, localHit.x);
                    maxLocal.x = Mathf.Max(maxLocal.x, localHit.x);
                    minLocal.y = Mathf.Min(minLocal.y, localHit.y);
                    maxLocal.y = Mathf.Max(maxLocal.y, localHit.y);
                }
                else
                {
                    return false;
                }
            }

            Ray centerRay = new Ray(testCamPos, rotation * Vector3.forward);
            if (!plane.Raycast(centerRay, out float cEnter))
            {
                return false;
            }
            Vector2 pivotLocal = safeArea.GetLocalPoint(centerRay.GetPoint(cEnter));

            // Pivot을 기준으로 한 4 모서리 투영 offset
            m_UnitMinOffsetX = minLocal.x - pivotLocal.x;
            m_UnitMaxOffsetX = maxLocal.x - pivotLocal.x;
            m_UnitMinOffsetY = minLocal.y - pivotLocal.y;
            m_UnitMaxOffsetY = maxLocal.y - pivotLocal.y;

            m_LastRotation = rotation;
            m_LastViewport = viewportRect;
            m_LastSafeAreaPos = safeArea.transform.position;
            m_LastSafeAreaRot = safeArea.transform.rotation;
            m_LastSafeAreaSize = safeArea.Size;
            m_LastSafeArea = safeArea;
            m_LastSafeAreaLossyScale = safeArea.transform.lossyScale;
            m_LastCameraAspect = camera.aspect;
            m_LastCameraPixelRect = camera.pixelRect;

            m_IsCacheValid = true;

            return true;
        }

        private bool VerifyPose(Vector3 pos, Quaternion rot, float orthoSize, UnityCamera camera, BackgroundCameraSafeArea safeArea, Rect viewportRect, Rect effRect)
        {
            Plane plane = safeArea.BackgroundPlane;
            float aspect = camera.aspect;
            float w = 2f * orthoSize * aspect;
            float h = 2f * orthoSize;

            for (int i = 0; i < 4; i++)
            {
                float vx = (i % 2 == 0) ? viewportRect.xMin : viewportRect.xMax;
                float vy = (i / 2 == 0) ? viewportRect.yMin : viewportRect.yMax;

                float localX = (vx - 0.5f) * w;
                float localY = (vy - 0.5f) * h;

                Vector3 worldOrigin = pos + rot * new Vector3(localX, localY, 0f);
                Ray ray = new Ray(worldOrigin, rot * Vector3.forward);

                if (plane.Raycast(ray, out float enter))
                {
                    Vector3 worldHit = ray.GetPoint(enter);
                    Vector2 localHit = safeArea.GetLocalPoint(worldHit);
                    if (localHit.x < effRect.xMin - ContainmentEpsilon || localHit.x > effRect.xMax + ContainmentEpsilon ||
                        localHit.y < effRect.yMin - ContainmentEpsilon || localHit.y > effRect.yMax + ContainmentEpsilon)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
