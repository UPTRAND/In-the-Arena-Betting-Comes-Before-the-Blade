#if UNITY_EDITOR && PHASE0B_VISUAL_VERIFIER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityCamera = UnityEngine.Camera;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace InTheArena.Camera.Test
{
    /// <summary>
    /// Phase 0-B 전용 런타임 검증 하네스입니다.
    /// Scene, Prefab, CameraSettings 원본을 저장하지 않고 Play Mode에서만 임시 구성을 만듭니다.
    /// </summary>
    public sealed class Phase0BVisualVerifier : MonoBehaviour
    {
        private const float InnerPadding = 0.05f;
        private const float ConstraintSafetyMargin = 0.01f;
        private const float RectEpsilon = 0.0001f;
        private const float MagentaChannelMinimum = 0.9f;
        private const float MagentaChannelMaximum = 0.1f;
        private const string MainGameSceneName = "MainGame";
        private const string BackgroundName = "pixel_background_elven-hall_bg";
        private const string TemporaryCoverageRootName = "BackgroundCoverageRoot (Phase0C Temporary)";
        private const string BettingRootName = "UI_BettingPhase";
        private const string BettingContentName = "MainContent";
        private const string CaptureDirectoryName = "CodexPhase0C";

        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Phase0BVisualVerifier s_Instance;

        private readonly StringBuilder m_Report = new StringBuilder();
        private readonly HashSet<CameraPhase> m_RecordedPhases = new HashSet<CameraPhase>();
        private readonly HashSet<CameraPhase> m_ActualFlowPhases = new HashSet<CameraPhase>();

        private CameraController m_Controller;
        private UnityCamera m_MainCamera;
        private GameObject m_Background;
        private GameObject m_CoverageRoot;
        private RectTransform m_MainContent;
        private Canvas m_MainContentCanvas;
        private BackgroundCameraSafeArea m_SafeArea;
        private CameraViewportProvider m_ViewportProvider;
        private CameraSettings m_OriginalSettings;
        private CameraSettings m_SettingsClone;

        private BackgroundCameraSafeArea m_OriginalSafeAreaReference;
        private CameraViewportProvider m_OriginalViewportProviderReference;
        private RectTransform m_OriginalViewportRect;
        private UnityCamera m_OriginalViewportCamera;
        private Vector2 m_OriginalSafeAreaCenter;
        private Vector2 m_OriginalSafeAreaSize;
        private float m_OriginalInnerPadding;

        private FieldInfo m_ControllerSafeAreaField;
        private FieldInfo m_ControllerViewportProviderField;
        private FieldInfo m_ControllerSettingsField;
        private FieldInfo m_SafeAreaCenterField;
        private FieldInfo m_SafeAreaSizeField;
        private FieldInfo m_SafeAreaPaddingField;
        private FieldInfo m_ViewportRectField;
        private FieldInfo m_ViewportCameraField;

        private bool m_CreatedSafeArea;
        private bool m_CreatedViewportProvider;
        private bool m_IsConfigured;
        private RectTransform m_InitialMainContentReference;
        private CameraPhase m_LastObservedPhase;
        private bool m_HasObservedPhase;
        private Rect m_LastNormalizedViewport;
        private bool m_HasLastNormalizedViewport;
        private string m_ReportPath;
        private string m_CaptureDirectory;
        private string m_SceneName;
        private int m_ConfigurationFrame;
        private bool m_ConfiguredFromSceneLoaded;
        private bool m_FirstRenderedFrameVerified;
        private bool m_IsRunningSyntheticScenario;
        private bool m_FinalEliminationCompleted;
        private Exception m_FinalEliminationException;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Phase0BVisualVerifier existing = FindAnyObjectByType<Phase0BVisualVerifier>(FindObjectsInactive.Include);
            if (existing != null)
            {
                s_Instance = existing;
                return;
            }

            GameObject go = new GameObject(nameof(Phase0BVisualVerifier));
            go.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(go);

            s_Instance = go.AddComponent<Phase0BVisualVerifier>();
            SceneManager.sceneLoaded += s_Instance.OnSceneLoaded;
        }

        private void Awake()
        {
            m_ReportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "phase0b_runtime_report.txt"));
            m_CaptureDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", CaptureDirectoryName));
        }

        private void OnSceneLoaded(UnityScene scene, LoadSceneMode mode)
        {
            TryStartForScene(scene);
        }

        private void TryStartForScene(UnityScene scene)
        {
            if (scene.name != MainGameSceneName)
                return;

            StopAllCoroutines();
            CleanupTemporaryState();
            ResetReport(scene);

            m_Background = FindInSceneIncludingInactive(scene, BackgroundName);
            m_Controller = FindCameraControllerInScene(scene);
            AppendReferenceResult("MainGame Scene", scene.IsValid() && scene.isLoaded, scene.path);
            AppendReferenceResult("Background", m_Background != null, m_Background != null ? GetPath(m_Background.transform) : BackgroundName);
            AppendReferenceResult("CameraController", m_Controller != null, m_Controller != null ? GetPath(m_Controller.transform) : "not found");

            if (m_Background == null || m_Controller == null)
            {
                WriteFailureReport("sceneLoaded 시점에 Background 또는 CameraController를 찾지 못해 첫 렌더 이전 구성을 보장할 수 없습니다.");
                return;
            }

            if (!TryConfigure(scene, out string configurationError, out Exception configurationException))
            {
                WriteFailureReport(configurationError, configurationException);
                return;
            }

            m_ConfigurationFrame = Time.frameCount;
            m_ConfiguredFromSceneLoaded = true;
            StartCoroutine(VerifyAfterFirstRenderedFrame(scene));
        }

        private IEnumerator VerifyAfterFirstRenderedFrame(UnityScene scene)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();

            float angleError = Quaternion.Angle(m_MainCamera.transform.rotation, Quaternion.Euler(
                m_SettingsClone.CameraAngleX,
                m_SettingsClone.CameraAngleY,
                0f));
            m_FirstRenderedFrameVerified = m_MainCamera.orthographic && angleError <= 0.01f;
            m_Report.AppendLine();
            m_Report.AppendLine("## First Render Frame Orthographic 검증");
            m_Report.AppendLine($"- sceneLoaded 구성 Frame: {m_ConfigurationFrame}");
            m_Report.AppendLine($"- sceneLoaded 동기 구성 완료: {m_ConfiguredFromSceneLoaded}");
            m_Report.AppendLine($"- 첫 렌더 후 Frame: {Time.frameCount}");
            m_Report.AppendLine($"- Camera.orthographic: {m_MainCamera.orthographic}");
            m_Report.AppendLine($"- Camera Rotation: {m_MainCamera.transform.eulerAngles}");
            m_Report.AppendLine($"- 설정 Rotation과 오차: {angleError:F6}°");
            m_Report.AppendLine($"- 첫 렌더 Orthographic 45° 계약: {(m_FirstRenderedFrameVerified ? "통과" : "실패")}");

            // CameraController.Start의 ApplySettings 이후에 sentinel을 다시 적용해야
            // 실제 Coverage 밖 Clear Color 노출을 시각적으로 판별할 수 있습니다.
            m_MainCamera.clearFlags = CameraClearFlags.SolidColor;
            m_MainCamera.backgroundColor = Color.magenta;
            yield return new WaitForEndOfFrame();
            m_Report.AppendLine("- Post-Start Clear Color Sentinel: Magenta");

            RecordViewportSnapshot("초기 구성", m_Controller.CurrentPhase);
            CaptureCameraPixelRect("initial");
            m_ActualFlowPhases.Add(m_Controller.CurrentPhase);
            WriteReport();

            StartCoroutine(MonitorPhaseAndViewportChanges(scene));
            StartCoroutine(RunSyntheticCameraScenarios(scene));
        }

        private bool TryConfigure(UnityScene scene, out string error, out Exception exception)
        {
            error = null;
            exception = null;

            try
            {
                if (!DiscoverRequiredReferences(scene, out error))
                    return false;

                RecordPreConfigurationState();

                if (!ValidateReflectionContract(out error))
                    return false;
                if (!ConfigureSafeArea(out error))
                    return false;
                if (!ConfigureViewportProvider(out error))
                    return false;
                if (!ConfigureOrthographic(out error))
                    return false;

                m_MainCamera.backgroundColor = Color.magenta;
                m_MainCamera.clearFlags = CameraClearFlags.SolidColor;
                m_IsConfigured = true;

                m_Report.AppendLine();
                m_Report.AppendLine("## 임시 구성: 완료");
                m_Report.AppendLine("- 필수 참조, Safe Area, ViewportProvider, Settings Clone 연결 성공");
                m_Report.AppendLine("- CameraController.SetProjectionMode(false) 호출 완료");
                m_Report.AppendLine("- Clear Color: Magenta");
                m_Report.AppendLine("- Scene/Prefab/CameraSettings Asset 저장 없음");
                return true;
            }
            catch (Exception caught)
            {
                exception = caught;
                error = $"구성 예외: {caught.GetType().Name}: {caught.Message}";
                return false;
            }
        }

        private bool DiscoverRequiredReferences(UnityScene scene, out string error)
        {
            error = null;
            m_MainCamera = m_Controller.MainCamera != null ? m_Controller.MainCamera : m_Controller.GetComponent<UnityCamera>();
            m_OriginalSettings = m_Controller.Settings;

            GameObject bettingRoot = FindInSceneIncludingInactive(scene, BettingRootName);
            m_MainContent = bettingRoot != null
                ? bettingRoot.transform.Find(BettingContentName) as RectTransform
                : null;
            m_MainContentCanvas = m_MainContent != null ? m_MainContent.GetComponentInParent<Canvas>() : null;

            AppendReferenceResult("Main Camera", m_MainCamera != null, m_MainCamera != null ? GetPath(m_MainCamera.transform) : "not found");
            AppendReferenceResult("CameraSettings", m_OriginalSettings != null, m_OriginalSettings != null ? m_OriginalSettings.name : "not found");
            AppendReferenceResult("Betting UI", bettingRoot != null, bettingRoot != null ? GetPath(bettingRoot.transform) : "not found");
            AppendReferenceResult("MainContent", m_MainContent != null, m_MainContent != null ? GetPath(m_MainContent) : "not found");
            AppendReferenceResult("Canvas", m_MainContentCanvas != null, m_MainContentCanvas != null ? GetPath(m_MainContentCanvas.transform) : "not found");

            if (m_MainCamera == null)
                error = "Main Camera 참조가 없습니다.";
            else if (m_OriginalSettings == null)
                error = "CameraSettings 참조가 없습니다.";
            else if (bettingRoot == null)
                error = "UI_BettingPhase를 찾지 못했습니다.";
            else if (m_MainContent == null)
                error = "UI_BettingPhase/MainContent를 찾지 못했습니다.";
            else if (m_MainContentCanvas == null)
                error = "MainContent의 부모 Canvas를 찾지 못했습니다.";

            return error == null;
        }

        private bool ValidateReflectionContract(out string error)
        {
            error = null;

            return TryGetField(typeof(BackgroundCameraSafeArea), "m_Center", typeof(Vector2), out m_SafeAreaCenterField, ref error) &&
                   TryGetField(typeof(BackgroundCameraSafeArea), "m_Size", typeof(Vector2), out m_SafeAreaSizeField, ref error) &&
                   TryGetField(typeof(BackgroundCameraSafeArea), "m_InnerPadding", typeof(float), out m_SafeAreaPaddingField, ref error) &&
                   TryGetField(typeof(CameraController), "m_SafeArea", typeof(BackgroundCameraSafeArea), out m_ControllerSafeAreaField, ref error) &&
                   TryGetField(typeof(CameraController), "m_ViewportProvider", typeof(CameraViewportProvider), out m_ControllerViewportProviderField, ref error) &&
                   TryGetField(typeof(CameraController), "m_CameraSettings", typeof(CameraSettings), out m_ControllerSettingsField, ref error) &&
                   TryGetField(typeof(CameraViewportProvider), "m_UIRect", typeof(RectTransform), out m_ViewportRectField, ref error) &&
                   TryGetField(typeof(CameraViewportProvider), "m_Camera", typeof(UnityCamera), out m_ViewportCameraField, ref error);
        }

        private static bool TryGetField(
            Type ownerType,
            string fieldName,
            Type expectedType,
            out FieldInfo field,
            ref string error)
        {
            field = ownerType.GetField(fieldName, PrivateInstance);
            if (field == null)
            {
                error = $"Reflection 필드 없음: {ownerType.FullName}.{fieldName}";
                return false;
            }

            if (field.FieldType != expectedType)
            {
                error = $"Reflection 필드 형식 불일치: {ownerType.FullName}.{fieldName}, expected={expectedType.FullName}, actual={field.FieldType.FullName}";
                return false;
            }

            return true;
        }

        private bool ConfigureSafeArea(out string error)
        {
            error = null;
            GameObject existing = FindInSceneIncludingInactive(m_Controller.gameObject.scene, TemporaryCoverageRootName);
            if (existing != null)
            {
                if ((existing.hideFlags & HideFlags.DontSave) == 0)
                {
                    error = $"저장 가능한 동명 오브젝트가 이미 존재합니다: {GetPath(existing.transform)}";
                    return false;
                }

                DestroyImmediate(existing);
            }

            m_CoverageRoot = new GameObject(TemporaryCoverageRootName);
            m_CoverageRoot.hideFlags = HideFlags.DontSave;
            Transform parent = m_Background.transform.parent;
            m_CoverageRoot.transform.SetParent(parent, false);
            m_CoverageRoot.transform.SetPositionAndRotation(
                m_Background.transform.position,
                m_Background.transform.rotation);
            m_CoverageRoot.transform.localScale = Vector3.one;

            m_SafeArea = m_CoverageRoot.AddComponent<BackgroundCameraSafeArea>();
            m_SafeArea.hideFlags = HideFlags.DontSave;
            m_CreatedSafeArea = true;

            m_SafeAreaCenterField.SetValue(m_SafeArea, Vector2.zero);
            m_SafeAreaSizeField.SetValue(m_SafeArea, new Vector2(19.2f, 10.8f));
            m_SafeAreaPaddingField.SetValue(m_SafeArea, InnerPadding);

            if (!m_SafeArea.ValidateConfiguration(false))
            {
                error = "BackgroundCameraSafeArea.ValidateConfiguration(false)가 실패했습니다.";
                return false;
            }

            m_OriginalSafeAreaReference = m_ControllerSafeAreaField.GetValue(m_Controller) as BackgroundCameraSafeArea;
            m_ControllerSafeAreaField.SetValue(m_Controller, m_SafeArea);

            m_Report.AppendLine("- Safe Area: 성공 (temporary independent CoverageRoot)");
            m_Report.AppendLine($"  - Root: {GetPath(m_CoverageRoot.transform)}");
            m_Report.AppendLine($"  - Root Position: {m_CoverageRoot.transform.position}");
            m_Report.AppendLine($"  - Root Rotation: {m_CoverageRoot.transform.eulerAngles}");
            m_Report.AppendLine($"  - Root Scale: {m_CoverageRoot.transform.lossyScale}");
            m_Report.AppendLine($"  - Center: {m_SafeArea.Center}");
            m_Report.AppendLine($"  - Size: {m_SafeArea.Size}");
            m_Report.AppendLine($"  - InnerPadding(float): {m_SafeArea.InnerPadding:F4}");
            return true;
        }

        private bool ConfigureViewportProvider(out string error)
        {
            error = null;
            m_ViewportProvider = m_Controller.GetComponent<CameraViewportProvider>();
            m_CreatedViewportProvider = m_ViewportProvider == null;

            if (m_CreatedViewportProvider)
            {
                m_ViewportProvider = m_Controller.gameObject.AddComponent<CameraViewportProvider>();
                m_ViewportProvider.hideFlags = HideFlags.DontSave;
            }

            m_OriginalViewportRect = m_ViewportRectField.GetValue(m_ViewportProvider) as RectTransform;
            m_OriginalViewportCamera = m_ViewportCameraField.GetValue(m_ViewportProvider) as UnityCamera;
            m_OriginalViewportProviderReference = m_ControllerViewportProviderField.GetValue(m_Controller) as CameraViewportProvider;

            m_ViewportRectField.SetValue(m_ViewportProvider, m_MainContent);
            m_ViewportCameraField.SetValue(m_ViewportProvider, m_MainCamera);
            m_ControllerViewportProviderField.SetValue(m_Controller, m_ViewportProvider);
            m_InitialMainContentReference = m_MainContent;

            if ((m_ViewportRectField.GetValue(m_ViewportProvider) as RectTransform) != m_MainContent ||
                (m_ViewportCameraField.GetValue(m_ViewportProvider) as UnityCamera) != m_MainCamera)
            {
                error = "CameraViewportProvider 필수 참조 연결 검증이 실패했습니다.";
                return false;
            }

            m_Report.AppendLine($"- ViewportProvider: 성공 ({(m_CreatedViewportProvider ? "created" : "reused")})");
            m_Report.AppendLine($"  - UI Rect: {GetPath(m_MainContent)}");
            m_Report.AppendLine($"  - Camera: {GetPath(m_MainCamera.transform)}");
            return true;
        }

        private bool ConfigureOrthographic(out string error)
        {
            error = null;
            CameraSettings currentSettings = m_ControllerSettingsField.GetValue(m_Controller) as CameraSettings;
            if (currentSettings == null)
            {
                error = "CameraController의 CameraSettings가 null입니다.";
                return false;
            }

            m_OriginalSettings = currentSettings;
            m_SettingsClone = Instantiate(currentSettings);
            m_SettingsClone.name = currentSettings.name + " (Phase0B Clone)";
            m_SettingsClone.hideFlags = HideFlags.DontSave;
            m_SettingsClone.ProjMode = ProjectionMode.Orthographic;
            m_ControllerSettingsField.SetValue(m_Controller, m_SettingsClone);
            m_Controller.SetProjectionMode(false);

            if (m_Controller.Settings != m_SettingsClone ||
                m_SettingsClone.ProjMode != ProjectionMode.Orthographic ||
                !m_MainCamera.orthographic)
            {
                error = "Settings Clone 또는 Orthographic 전환 검증이 실패했습니다.";
                return false;
            }

            m_Report.AppendLine("- CameraSettings Clone: 성공");
            m_Report.AppendLine($"  - Original: {m_OriginalSettings.name} / {m_OriginalSettings.ProjMode}");
            m_Report.AppendLine($"  - Clone: {m_SettingsClone.name} / {m_SettingsClone.ProjMode}");
            return true;
        }

        private IEnumerator MonitorPhaseAndViewportChanges(UnityScene scene)
        {
            while (scene.IsValid() && scene.isLoaded && m_Controller != null && m_IsConfigured)
            {
                CameraPhase phase = m_Controller.CurrentPhase;
                if (!m_HasObservedPhase || phase != m_LastObservedPhase)
                {
                    Canvas.ForceUpdateCanvases();
                    yield return new WaitForEndOfFrame();
                    string reason = m_IsRunningSyntheticScenario ? "Camera API 합성 Phase 전환" : "실제 게임 흐름 Phase 전환";
                    RecordViewportSnapshot(reason, phase);
                    if (!m_IsRunningSyntheticScenario)
                        m_ActualFlowPhases.Add(phase);
                    WriteReport();
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        private IEnumerator RunSyntheticCameraScenarios(UnityScene scene)
        {
            m_IsRunningSyntheticScenario = true;
            m_Report.AppendLine();
            m_Report.AppendLine("## Phase 0-C Camera API 합성 시나리오");
            m_Report.AppendLine("- 아래 시나리오는 CameraController 경계/Shake 계약 검증용이며 실제 UI Phase 전환 통과 근거로 사용하지 않음");

            yield return RunFocusScenario(scene, "최대 Zoom-Out / 중앙", "max_zoom_center", Vector3.zero);
            yield return RunFocusScenario(scene, "좌측 경계", "left_boundary", new Vector3(-100f, 0f, 0f));
            yield return RunFocusScenario(scene, "우측 경계", "right_boundary", new Vector3(100f, 0f, 0f));
            yield return RunFocusScenario(scene, "상단 경계", "top_boundary", new Vector3(0f, 0f, 8f));
            yield return RunFocusScenario(scene, "하단 경계", "bottom_boundary", new Vector3(0f, 0f, -10f));

            if (!IsScenarioAlive(scene))
                yield break;
            m_Controller.SetPhase(CameraPhase.Combat);
            yield return new WaitForSecondsRealtime(0.75f);
            RecordViewportSnapshot("Camera API 합성 Combat", CameraPhase.Combat);
            yield return new WaitForEndOfFrame();
            CaptureCameraPixelRect("combat_api");

            m_Controller.SetPhase(CameraPhase.Result);
            yield return new WaitForSecondsRealtime(0.75f);
            RecordViewportSnapshot("Camera API 합성 Result", CameraPhase.Result);
            yield return new WaitForEndOfFrame();
            CaptureCameraPixelRect("result_api");

            Bounds eliminationBounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
            m_FinalEliminationCompleted = false;
            m_FinalEliminationException = null;
            _ = RunFinalEliminationAsync(eliminationBounds);
            float finalEliminationDeadline = Time.realtimeSinceStartup + 5f;
            while (!m_FinalEliminationCompleted && Time.realtimeSinceStartup < finalEliminationDeadline)
                yield return null;

            if (!m_FinalEliminationCompleted || m_FinalEliminationException != null)
            {
                m_Report.AppendLine($"- Final Elimination 비동기 완료 실패: {m_FinalEliminationException?.Message ?? "timeout"}");
                WriteReport();
                m_IsRunningSyntheticScenario = false;
                yield break;
            }

            RecordViewportSnapshot("Camera API 합성 Final Elimination", m_Controller.CurrentPhase);
            yield return new WaitForEndOfFrame();
            CaptureCameraPixelRect("final_elimination");
            m_Controller.EndFinalEliminationFocus();

            m_Controller.ShakeCamera(2f, 0.5f);
            yield return new WaitForSecondsRealtime(0.1f);
            RecordViewportSnapshot("Camera API 최대 Shake", m_Controller.CurrentPhase);
            yield return new WaitForEndOfFrame();
            CaptureCameraPixelRect("max_shake");
            m_Controller.HoldCurrentPoseForFinalElimination();
            m_Controller.EndFinalEliminationFocus();

            _ = m_Controller.ResetToDefaultAsync(0f);
            yield return null;
            RecordViewportSnapshot("Reset to Default", m_Controller.CurrentPhase);
            yield return new WaitForEndOfFrame();
            CaptureCameraPixelRect("reset_to_default");

            m_IsRunningSyntheticScenario = false;
            m_Report.AppendLine("- Camera API 합성 시나리오: 완료");
            WriteReport();
        }

        private IEnumerator RunFocusScenario(UnityScene scene, string label, string captureLabel, Vector3 position)
        {
            if (!IsScenarioAlive(scene))
                yield break;

            _ = m_Controller.FocusOnPositionAsync(
                position,
                m_SettingsClone.MaxOrthographicSize,
                0f);
            yield return null;
            RecordViewportSnapshot($"Camera API {label}", m_Controller.CurrentPhase);
            yield return new WaitForEndOfFrame();
            CaptureCameraPixelRect(captureLabel);
        }

        /// <summary>
        /// Captures the already rendered full screen, then crops and scans only the Main Camera pixel rect.
        /// Rendering the camera into a smaller RenderTexture is intentionally avoided because Camera.rect would
        /// be applied a second time and produce a false magenta-coverage result.
        /// </summary>
        private void CaptureCameraPixelRect(string scenario)
        {
            Texture2D fullScreen = null;
            Texture2D cameraCrop = null;

            try
            {
                if (m_MainCamera == null)
                {
                    m_Report.AppendLine($"- Visual Capture [{scenario}]: skipped (Main Camera missing)");
                    return;
                }

                Directory.CreateDirectory(m_CaptureDirectory);
                fullScreen = ScreenCapture.CaptureScreenshotAsTexture();
                if (fullScreen == null)
                {
                    m_Report.AppendLine($"- Visual Capture [{scenario}]: failed (full-screen texture was null)");
                    return;
                }

                if (!TryGetClampedPixelRect(m_MainCamera.pixelRect, fullScreen.width, fullScreen.height, out RectInt cropRect, out string cropError))
                {
                    m_Report.AppendLine($"- Visual Capture [{scenario}]: failed ({cropError})");
                    return;
                }

                Color[] pixels = fullScreen.GetPixels(cropRect.x, cropRect.y, cropRect.width, cropRect.height);
                int magentaPixels = CountMagentaPixels(pixels);
                float magentaRatio = pixels.Length > 0 ? (float)magentaPixels / pixels.Length : 0f;

                cameraCrop = new Texture2D(cropRect.width, cropRect.height, TextureFormat.RGBA32, false);
                cameraCrop.SetPixels(pixels);
                cameraCrop.Apply(false, false);

                string fileStem = GetCaptureLabel(scenario);
                string fullPath = Path.Combine(m_CaptureDirectory, fileStem + "_full_screen.png");
                string cropPath = Path.Combine(m_CaptureDirectory, fileStem + "_camera_pixel_rect.png");
                File.WriteAllBytes(fullPath, fullScreen.EncodeToPNG());
                File.WriteAllBytes(cropPath, cameraCrop.EncodeToPNG());

                m_Report.AppendLine($"- Visual Capture [{scenario}]: success");
                m_Report.AppendLine("  - Method: ScreenCapture full Screen -> Camera.pixelRect crop (Camera.rect applied once by normal screen render)");
                m_Report.AppendLine($"  - Full Screen: {fullScreen.width}x{fullScreen.height}, path: {fullPath}");
                m_Report.AppendLine($"  - Crop Rect: (x:{cropRect.x}, y:{cropRect.y}, width:{cropRect.width}, height:{cropRect.height}), path: {cropPath}");
                m_Report.AppendLine($"  - Magenta Pixels: {magentaPixels} / {pixels.Length} ({magentaRatio:P4})");
            }
            catch (Exception exception)
            {
                m_Report.AppendLine($"- Visual Capture [{scenario}]: exception ({exception.GetType().Name}: {exception.Message})");
                Debug.LogError($"[Phase0BVisualVerifier] Visual capture failed for {scenario}: {exception}");
            }
            finally
            {
                if (cameraCrop != null)
                    Destroy(cameraCrop);
                if (fullScreen != null)
                    Destroy(fullScreen);
            }
        }

        private static bool TryGetClampedPixelRect(Rect pixelRect, int textureWidth, int textureHeight, out RectInt cropRect, out string error)
        {
            cropRect = default;
            error = null;
            if (textureWidth <= 0 || textureHeight <= 0)
            {
                error = $"invalid full-screen texture size {textureWidth}x{textureHeight}";
                return false;
            }

            int xMin = Mathf.Clamp(Mathf.FloorToInt(pixelRect.xMin), 0, textureWidth);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(pixelRect.yMin), 0, textureHeight);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(pixelRect.xMax), 0, textureWidth);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(pixelRect.yMax), 0, textureHeight);
            if (xMax <= xMin || yMax <= yMin)
            {
                error = $"Camera.pixelRect {FormatRect(pixelRect)} does not intersect the captured screen {textureWidth}x{textureHeight}";
                return false;
            }

            cropRect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            return true;
        }

        private static int CountMagentaPixels(Color[] pixels)
        {
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (pixel.r >= MagentaChannelMinimum &&
                    pixel.g <= MagentaChannelMaximum &&
                    pixel.b >= MagentaChannelMinimum)
                {
                    count++;
                }
            }

            return count;
        }

        private static string GetCaptureLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unnamed";

            StringBuilder sanitized = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                sanitized.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_');
            }

            return sanitized.ToString().Trim('_');
        }

        private async Awaitable RunFinalEliminationAsync(Bounds bounds)
        {
            try
            {
                await m_Controller.FocusFinalEliminationAsync(bounds);
            }
            catch (Exception exception)
            {
                m_FinalEliminationException = exception;
            }
            finally
            {
                m_FinalEliminationCompleted = true;
            }
        }

        private bool IsScenarioAlive(UnityScene scene)
        {
            return scene.IsValid() && scene.isLoaded && m_Controller != null && m_IsConfigured;
        }

        private void RecordPreConfigurationState()
        {
            m_Report.AppendLine();
            m_Report.AppendLine("## Pre-configuration State");
            m_Report.AppendLine($"- Main Camera Projection: {(m_MainCamera.orthographic ? "Orthographic" : "Perspective")}");
            m_Report.AppendLine($"- Controller CameraSettings: {m_OriginalSettings.name} / {m_OriginalSettings.ProjMode}");
            m_Report.AppendLine("- 아래 pre-config 값은 MainGame sceneLoaded 콜백에서 CameraController.Start 이전에 관찰한 값");
            m_Report.AppendLine("- Settings Clone 구성 후 별도 섹션에서 첫 렌더 프레임 상태를 검증함");
            m_Report.AppendLine($"- Pre-config Camera.orthographic: {m_MainCamera.orthographic}");
            m_Report.AppendLine($"- Pre-config Camera.rect: {FormatRect(m_MainCamera.rect)}");
            m_Report.AppendLine($"- Pre-config Camera.pixelRect: {FormatRect(m_MainCamera.pixelRect)}");
        }

        private void RecordViewportSnapshot(string reason, CameraPhase phase)
        {
            RectTransform providerReference = m_ViewportRectField.GetValue(m_ViewportProvider) as RectTransform;
            RectTransform currentAtPath = FindCurrentMainContent();
            bool referenceAlive = providerReference != null;
            bool destroyed = !referenceAlive;
            bool replaced = currentAtPath != null && currentAtPath != m_InitialMainContentReference;
            bool activeInHierarchy = referenceAlive && providerReference.gameObject.activeInHierarchy;
            Canvas canvas = referenceAlive ? providerReference.GetComponentInParent<Canvas>() : null;

            Vector3[] corners = new Vector3[4];
            Vector2 minScreen = Vector2.zero;
            Vector2 maxScreen = Vector2.zero;
            bool hasCorners = referenceAlive && canvas != null && TryGetScreenCorners(providerReference, canvas, corners, out minScreen, out maxScreen);

            Rect cameraRect = m_MainCamera.rect;
            Rect cameraPixelRect = m_MainCamera.pixelRect;
            Rect screenRect = hasCorners
                ? Rect.MinMaxRect(minScreen.x, minScreen.y, maxScreen.x, maxScreen.y)
                : default;
            Rect intersection = hasCorners ? Intersect(screenRect, cameraPixelRect) : default;
            Rect manualNormalized = hasCorners
                ? CameraViewportProvider.NormalizeScreenRect(minScreen, maxScreen, cameraPixelRect)
                : new Rect(0f, 0f, 1f, 1f);
            Rect providerViewport = m_ViewportProvider.GetEffectiveViewportRect();
            bool sameAsPrevious = m_HasLastNormalizedViewport && RectApproximately(providerViewport, m_LastNormalizedViewport);
            bool cameraRectPartial = !RectApproximately(cameraRect, new Rect(0f, 0f, 1f, 1f));
            bool providerPartial = !RectApproximately(providerViewport, new Rect(0f, 0f, 1f, 1f));

            m_Report.AppendLine();
            m_Report.AppendLine($"## Viewport Snapshot - {phase} ({reason})");
            m_Report.AppendLine($"- reference 생존: {referenceAlive}");
            m_Report.AppendLine($"- reference entityID: {(referenceAlive ? providerReference.GetEntityId().ToString() : "0")}");
            m_Report.AppendLine($"- current path entityID: {(currentAtPath != null ? currentAtPath.GetEntityId().ToString() : "0")}");
            m_Report.AppendLine($"- activeInHierarchy: {activeInHierarchy}");
            m_Report.AppendLine($"- 부모 Canvas 존재: {canvas != null}");
            m_Report.AppendLine($"- 오브젝트 파괴: {destroyed}");
            m_Report.AppendLine($"- 오브젝트 교체: {replaced}");
            m_Report.AppendLine($"- Screen.width / Screen.height: {Screen.width} / {Screen.height}");
            m_Report.AppendLine($"- Camera.rect: {FormatRect(cameraRect)}");
            m_Report.AppendLine($"- Camera.pixelRect: {FormatRect(cameraPixelRect)}");

            if (hasCorners)
            {
                m_Report.AppendLine($"- GetWorldCorners[0]: {corners[0]}");
                m_Report.AppendLine($"- GetWorldCorners[1]: {corners[1]}");
                m_Report.AppendLine($"- GetWorldCorners[2]: {corners[2]}");
                m_Report.AppendLine($"- GetWorldCorners[3]: {corners[3]}");
                m_Report.AppendLine($"- MainContent Screen Corner Min/Max: {minScreen} / {maxScreen}");
                m_Report.AppendLine($"- MainContent와 Camera.pixelRect 교차 영역: {FormatRect(intersection)}");
                m_Report.AppendLine($"- Camera.pixelRect 기준 normalized viewport: {FormatRect(manualNormalized)}");
            }
            else
            {
                m_Report.AppendLine("- GetWorldCorners 결과: 계산 불가");
                m_Report.AppendLine("- MainContent Screen Corner Min/Max: 계산 불가");
                m_Report.AppendLine("- MainContent와 Camera.pixelRect 교차 영역: 계산 불가");
                m_Report.AppendLine("- Camera.pixelRect 기준 normalized viewport: 계산 불가");
            }

            m_Report.AppendLine($"- ViewportProvider 최종 반환값: {FormatRect(providerViewport)}");
            m_Report.AppendLine($"- 직전 Phase/스냅샷과 좌표 동일: {(m_HasLastNormalizedViewport ? sameAsPrevious.ToString() : "N/A")}");
            m_Report.AppendLine($"- Camera.rect partial: {cameraRectPartial}");
            m_Report.AppendLine($"- Provider viewport partial: {providerPartial}");
            m_Report.AppendLine($"- 중복 영역 제한 후보: {cameraRectPartial && providerPartial} (진단값이며 구조 결정 아님)");

            if (TryCalculateMaxAllowed(providerViewport, out float maxAllowed, out string maxAllowedError))
                m_Report.AppendLine($"- verifier 계산 maxAllowed (shake 제외): {maxAllowed:F6}");
            else
                m_Report.AppendLine($"- verifier 계산 maxAllowed: 실패 ({maxAllowedError})");

            m_Report.AppendLine($"- Camera Position: {m_MainCamera.transform.position}");
            m_Report.AppendLine($"- Camera Rotation: {m_MainCamera.transform.eulerAngles}");
            m_Report.AppendLine($"- Current Orthographic Size: {m_MainCamera.orthographicSize:F6}");
            m_Report.AppendLine($"- Safe Area Effective Rect: {FormatRect(m_SafeArea.GetEffectiveLocalRect())}");
            if (TryCalculateFootprint(
                    providerViewport,
                    m_MainCamera.transform.position,
                    m_MainCamera.transform.rotation,
                    m_MainCamera.orthographicSize,
                    out Vector3[] footprintCorners,
                    out Vector2 footprintMin,
                    out Vector2 footprintMax,
                    out string footprintError))
            {
                Rect effective = m_SafeArea.GetEffectiveLocalRect();
                bool contained = Contains(effective, footprintMin) && Contains(effective, footprintMax);
                for (int i = 0; i < footprintCorners.Length; i++)
                    m_Report.AppendLine($"- Footprint World Corner[{i}]: {footprintCorners[i]}");
                m_Report.AppendLine($"- Footprint Local Min/Max: {footprintMin} / {footprintMax}");
                m_Report.AppendLine($"- Footprint 전체 포함: {contained}");
            }
            else
            {
                m_Report.AppendLine($"- Footprint 계산 실패: {footprintError}");
            }

            if (TryCalculateFootprint(
                    new Rect(0f, 0f, 1f, 1f),
                    m_MainCamera.transform.position,
                    m_MainCamera.transform.rotation,
                    m_MainCamera.orthographicSize,
                    out _,
                    out Vector2 fullFootprintMin,
                    out Vector2 fullFootprintMax,
                    out string fullFootprintError))
            {
                Rect effective = m_SafeArea.GetEffectiveLocalRect();
                bool fullContained = Contains(effective, fullFootprintMin) && Contains(effective, fullFootprintMax);
                m_Report.AppendLine($"- Full Camera Footprint Local Min/Max: {fullFootprintMin} / {fullFootprintMax}");
                m_Report.AppendLine($"- Full Camera Footprint 전체 포함: {fullContained}");
            }
            else
            {
                m_Report.AppendLine($"- Full Camera Footprint 계산 실패: {fullFootprintError}");
            }

            m_RecordedPhases.Add(phase);
            m_LastObservedPhase = phase;
            m_HasObservedPhase = true;
            m_LastNormalizedViewport = providerViewport;
            m_HasLastNormalizedViewport = true;
        }

        private bool TryCalculateMaxAllowed(Rect viewport, out float maxAllowed, out string error)
        {
            maxAllowed = 0f;
            error = null;

            if (m_MainCamera == null || m_SafeArea == null || m_SettingsClone == null)
            {
                error = "필수 참조 없음";
                return false;
            }

            Quaternion rotation = m_MainCamera.transform.rotation;
            Vector3 testCameraPosition = m_SafeArea.transform.position - rotation * Vector3.forward * 100f;
            if (!TryCalculateFootprint(
                    viewport,
                    testCameraPosition,
                    rotation,
                    1f,
                    out _,
                    out Vector2 minLocal,
                    out Vector2 maxLocal,
                    out error))
                return false;

            float footprintWidth = maxLocal.x - minLocal.x;
            float footprintHeight = maxLocal.y - minLocal.y;
            if (footprintWidth <= 0f || footprintHeight <= 0f)
            {
                error = "유효하지 않은 footprint";
                return false;
            }

            Rect effective = m_SafeArea.GetEffectiveLocalRect();
            float usableWidth = effective.width - ConstraintSafetyMargin * 2f;
            float usableHeight = effective.height - ConstraintSafetyMargin * 2f;
            maxAllowed = Mathf.Min(
                m_SettingsClone.MaxOrthographicSize,
                Mathf.Min(usableWidth / footprintWidth, usableHeight / footprintHeight));
            return true;
        }

        private bool TryCalculateFootprint(
            Rect viewport,
            Vector3 cameraPosition,
            Quaternion rotation,
            float orthographicSize,
            out Vector3[] worldCorners,
            out Vector2 minLocal,
            out Vector2 maxLocal,
            out string error)
        {
            worldCorners = new Vector3[4];
            minLocal = new Vector2(float.MaxValue, float.MaxValue);
            maxLocal = new Vector2(float.MinValue, float.MinValue);
            error = null;

            if (m_MainCamera == null || m_SafeArea == null || orthographicSize <= 0f)
            {
                error = "필수 참조 또는 Orthographic Size가 유효하지 않음";
                return false;
            }

            Plane plane = m_SafeArea.BackgroundPlane;
            float width = 2f * orthographicSize * m_MainCamera.aspect;
            float height = 2f * orthographicSize;
            Vector3 forward = rotation * Vector3.forward;

            for (int i = 0; i < 4; i++)
            {
                float vx = i % 2 == 0 ? viewport.xMin : viewport.xMax;
                float vy = i / 2 == 0 ? viewport.yMin : viewport.yMax;
                Vector3 origin = cameraPosition + rotation * new Vector3(
                    (vx - 0.5f) * width,
                    (vy - 0.5f) * height,
                    0f);

                if (!plane.Raycast(new Ray(origin, forward), out float enter))
                {
                    error = "viewport corner raycast 실패";
                    return false;
                }

                Vector3 world = origin + forward * enter;
                Vector2 local = m_SafeArea.GetLocalPoint(world);
                worldCorners[i] = world;
                minLocal = Vector2.Min(minLocal, local);
                maxLocal = Vector2.Max(maxLocal, local);
            }

            return true;
        }

        private RectTransform FindCurrentMainContent()
        {
            if (m_Controller == null)
                return null;

            UnityScene scene = m_Controller.gameObject.scene;
            GameObject bettingRoot = FindInSceneIncludingInactive(scene, BettingRootName);
            return bettingRoot != null
                ? bettingRoot.transform.Find(BettingContentName) as RectTransform
                : null;
        }

        private static bool TryGetScreenCorners(
            RectTransform rectTransform,
            Canvas canvas,
            Vector3[] corners,
            out Vector2 minScreen,
            out Vector2 maxScreen)
        {
            minScreen = new Vector2(float.MaxValue, float.MaxValue);
            maxScreen = new Vector2(float.MinValue, float.MinValue);
            rectTransform.GetWorldCorners(corners);

            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screenPoint;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    screenPoint = corners[i];
                }
                else
                {
                    UnityCamera uiCamera = canvas.worldCamera;
                    screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
                }

                if (!IsFinite(screenPoint.x) || !IsFinite(screenPoint.y))
                    return false;

                minScreen = Vector2.Min(minScreen, screenPoint);
                maxScreen = Vector2.Max(maxScreen, screenPoint);
            }

            return true;
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            return xMax > xMin && yMax > yMin
                ? Rect.MinMaxRect(xMin, yMin, xMax, yMax)
                : new Rect(xMin, yMin, 0f, 0f);
        }

        private void ResetReport(UnityScene scene)
        {
            m_Report.Clear();
            m_RecordedPhases.Clear();
            m_ActualFlowPhases.Clear();
            m_HasObservedPhase = false;
            m_HasLastNormalizedViewport = false;
            m_SceneName = scene.name;

            m_Report.AppendLine("# Phase 0-B 시각 검증 런타임 기록");
            m_Report.AppendLine($"- Report Path: {m_ReportPath}");
            m_Report.AppendLine($"- 실행 Scene: {scene.name}");
            m_Report.AppendLine($"- Scene Path: {scene.path}");
            m_Report.AppendLine($"- 시작 시각(UTC): {DateTime.UtcNow:O}");
            m_Report.AppendLine();
            m_Report.AppendLine("## 필수 참조 탐색");
        }

        private void AppendReferenceResult(string label, bool success, string detail)
        {
            m_Report.AppendLine($"- {label}: {(success ? "성공" : "실패")} ({detail})");
        }

        private void WriteFailureReport(string reason, Exception exception = null)
        {
            m_Report.AppendLine();
            m_Report.AppendLine("## 임시 구성: 실패");
            m_Report.AppendLine($"- Scene: {m_SceneName}");
            m_Report.AppendLine($"- 사유: {reason}");
            if (exception != null)
                m_Report.AppendLine($"- StackTrace: {exception.StackTrace}");

            WriteReport();
            Debug.LogError($"[Phase0BVisualVerifier] 실패: {reason}");
        }

        private void WriteReport()
        {
            try
            {
                StringBuilder output = new StringBuilder(m_Report.ToString());
                output.AppendLine();
                output.AppendLine("## Phase 기록 상태");
                output.AppendLine($"- Betting: {(m_RecordedPhases.Contains(CameraPhase.Betting) ? "기록됨" : "미관찰")}");
                output.AppendLine($"- Combat: {(m_RecordedPhases.Contains(CameraPhase.Combat) ? "기록됨" : "미관찰")}");
                output.AppendLine($"- Result: {(m_RecordedPhases.Contains(CameraPhase.Result) ? "기록됨" : "미관찰")}");
                output.AppendLine($"- 실제 게임 흐름 Betting: {(m_ActualFlowPhases.Contains(CameraPhase.Betting) ? "관찰됨" : "미관찰")}");
                output.AppendLine($"- 실제 게임 흐름 Combat: {(m_ActualFlowPhases.Contains(CameraPhase.Combat) ? "관찰됨" : "미관찰")}");
                output.AppendLine($"- 실제 게임 흐름 Result: {(m_ActualFlowPhases.Contains(CameraPhase.Result) ? "관찰됨" : "미관찰")}");
                output.AppendLine($"- First render Orthographic 45°: {(m_FirstRenderedFrameVerified ? "통과" : "실패/미검증")}");
                output.AppendLine("- activeInHierarchy=false만으로 Full Viewport fallback을 판정하지 않음");
                output.AppendLine("- Camera.rect가 공통 GameplayViewport인지 이번 단계에서 결정하지 않음");
                File.WriteAllText(m_ReportPath, output.ToString(), new UTF8Encoding(false));
                Debug.Log($"[Phase0BVisualVerifier] Runtime report updated: {m_ReportPath}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Phase0BVisualVerifier] 리포트 쓰기 실패: {exception}");
            }
        }

        private void CleanupTemporaryState()
        {
            m_IsConfigured = false;

            try
            {
                if (m_Controller != null)
                {
                    if (m_ControllerSettingsField != null && m_SettingsClone != null &&
                        ReferenceEquals(m_ControllerSettingsField.GetValue(m_Controller), m_SettingsClone))
                    {
                        m_ControllerSettingsField.SetValue(m_Controller, m_OriginalSettings);
                        if (m_OriginalSettings != null)
                            m_Controller.SetProjectionMode(m_OriginalSettings.UsePerspective);
                    }

                    if (m_ControllerSafeAreaField != null)
                        m_ControllerSafeAreaField.SetValue(m_Controller, m_OriginalSafeAreaReference);
                    if (m_ControllerViewportProviderField != null)
                        m_ControllerViewportProviderField.SetValue(m_Controller, m_OriginalViewportProviderReference);
                }

                if (m_ViewportProvider != null && !m_CreatedViewportProvider)
                {
                    if (m_ViewportRectField != null)
                        m_ViewportRectField.SetValue(m_ViewportProvider, m_OriginalViewportRect);
                    if (m_ViewportCameraField != null)
                        m_ViewportCameraField.SetValue(m_ViewportProvider, m_OriginalViewportCamera);
                }

                if (m_SafeArea != null && !m_CreatedSafeArea)
                {
                    if (m_SafeAreaCenterField != null)
                        m_SafeAreaCenterField.SetValue(m_SafeArea, m_OriginalSafeAreaCenter);
                    if (m_SafeAreaSizeField != null)
                        m_SafeAreaSizeField.SetValue(m_SafeArea, m_OriginalSafeAreaSize);
                    if (m_SafeAreaPaddingField != null)
                        m_SafeAreaPaddingField.SetValue(m_SafeArea, m_OriginalInnerPadding);
                }

                if (m_CreatedViewportProvider && m_ViewportProvider != null)
                    DestroyImmediate(m_ViewportProvider);
                if (m_CoverageRoot != null)
                    DestroyImmediate(m_CoverageRoot);
                else if (m_CreatedSafeArea && m_SafeArea != null)
                    DestroyImmediate(m_SafeArea);
                if (m_SettingsClone != null)
                    DestroyImmediate(m_SettingsClone);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Phase0BVisualVerifier] 임시 상태 정리 실패: {exception}");
            }

            m_Controller = null;
            m_MainCamera = null;
            m_Background = null;
            m_CoverageRoot = null;
            m_MainContent = null;
            m_MainContentCanvas = null;
            m_SafeArea = null;
            m_ViewportProvider = null;
            m_OriginalSettings = null;
            m_SettingsClone = null;
            m_OriginalSafeAreaReference = null;
            m_OriginalViewportProviderReference = null;
            m_CreatedSafeArea = false;
            m_CreatedViewportProvider = false;
            m_ConfiguredFromSceneLoaded = false;
            m_FirstRenderedFrameVerified = false;
            m_IsRunningSyntheticScenario = false;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            CleanupTemporaryState();
            if (s_Instance == this)
                s_Instance = null;
        }

        private void OnDrawGizmos()
        {
            if (!m_IsConfigured || m_SafeArea == null || m_MainCamera == null || m_ViewportProvider == null)
                return;

            Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
            DrawSafeAreaRect(m_SafeArea.GetEffectiveLocalRect());

            Rect viewport = m_ViewportProvider.GetEffectiveViewportRect();
            if (!TryCalculateFootprint(
                    viewport,
                    m_MainCamera.transform.position,
                    m_MainCamera.transform.rotation,
                    m_MainCamera.orthographicSize,
                    out Vector3[] corners,
                    out Vector2 minLocal,
                    out Vector2 maxLocal,
                    out _))
                return;

            Rect effective = m_SafeArea.GetEffectiveLocalRect();
            bool contained = Contains(effective, minLocal) && Contains(effective, maxLocal);
            Gizmos.color = contained
                ? new Color(1f, 0.85f, 0f, 1f)
                : new Color(1f, 0f, 0f, 1f);
            DrawLoop(corners);
        }

        private void OnGUI()
        {
            if (!m_IsConfigured || m_MainCamera == null || m_ViewportProvider == null)
                return;

            Rect viewport = m_ViewportProvider.GetEffectiveViewportRect();
            string maxText = TryCalculateMaxAllowed(viewport, out float maxAllowed, out string error)
                ? maxAllowed.ToString("F4")
                : $"error: {error}";
            string status =
                $"Phase 0-B/0-C Camera Verifier\n" +
                $"Phase: {m_Controller.CurrentPhase}\n" +
                $"Orthographic: {m_MainCamera.orthographic}\n" +
                $"Rotation: {m_MainCamera.transform.eulerAngles}\n" +
                $"Size: {m_MainCamera.orthographicSize:F4} / maxAllowed: {maxText}\n" +
                $"Viewport: {FormatRect(viewport)}";
            GUI.Box(new Rect(10f, 10f, 430f, 115f), status);
        }

        private void DrawSafeAreaRect(Rect rect)
        {
            Vector3[] corners =
            {
                m_SafeArea.GetWorldPoint(new Vector2(rect.xMin, rect.yMin)),
                m_SafeArea.GetWorldPoint(new Vector2(rect.xMax, rect.yMin)),
                m_SafeArea.GetWorldPoint(new Vector2(rect.xMin, rect.yMax)),
                m_SafeArea.GetWorldPoint(new Vector2(rect.xMax, rect.yMax))
            };
            DrawLoop(corners);
        }

        private static void DrawLoop(Vector3[] corners)
        {
            if (corners == null || corners.Length != 4)
                return;

            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[3]);
            Gizmos.DrawLine(corners[3], corners[2]);
            Gizmos.DrawLine(corners[2], corners[0]);
        }

        private static GameObject FindInSceneIncludingInactive(UnityScene scene, string objectName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                    return root;

                Transform child = FindInChildren(root.transform, objectName);
                if (child != null)
                    return child.gameObject;
            }

            return null;
        }

        private static Transform FindInChildren(Transform parent, string objectName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == objectName)
                    return child;

                Transform found = FindInChildren(child, objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static CameraController FindCameraControllerInScene(UnityScene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CameraController controller = root.GetComponentInChildren<CameraController>(true);
                if (controller != null)
                    return controller;
            }

            return null;
        }

        private static bool RectApproximately(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) <= RectEpsilon &&
                   Mathf.Abs(a.y - b.y) <= RectEpsilon &&
                   Mathf.Abs(a.width - b.width) <= RectEpsilon &&
                   Mathf.Abs(a.height - b.height) <= RectEpsilon;
        }

        private static bool Contains(Rect rect, Vector2 point)
        {
            return point.x >= rect.xMin - RectEpsilon &&
                   point.x <= rect.xMax + RectEpsilon &&
                   point.y >= rect.yMin - RectEpsilon &&
                   point.y <= rect.yMax + RectEpsilon;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string FormatRect(Rect rect)
        {
            return $"(x:{rect.x:F4}, y:{rect.y:F4}, width:{rect.width:F4}, height:{rect.height:F4})";
        }

        private static string GetPath(Transform current)
        {
            if (current == null)
                return "<null>";
            if (current.parent == null)
                return "/" + current.name;
            return GetPath(current.parent) + "/" + current.name;
        }
    }
}
#endif
