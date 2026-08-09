#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>Creates short-lived UI reward icons that scatter, then fly into a target UI element.</summary>
    public static class UI_FlyingRewardEffect
    {
        private const float ScatterDuration = 0.18f;
        private const float FlightDuration = 0.55f;
        private const float LaunchInterval = 0.06f;
        private const float ScatterRadius = 60f;
        private const float AppearDuration = 0.25f;
        private const float FlightStartDelay = 2f;
        private const string PreviewPrefabPath = "UI/UI_FlyingRewardPreview";
        private static readonly List<GameObject> ActiveIcons = new List<GameObject>(16);
        private static readonly Dictionary<RectTransform, TargetPulse> ActiveTargetPulses = new Dictionary<RectTransform, TargetPulse>(4);
        private static Canvas s_Canvas;
        private static UI_FlyingRewardPreviewView s_PreviewPrefab;
        private static bool s_Initialized;

        public static void Play(
            RectTransform source,
            RectTransform target,
            Sprite sprite,
            int amount,
            Action<int> onValueChanged,
            Action onCompleted,
            int maxIconCount = 8,
            string previewText = null,
            float previewDuration = 1.5f)
        {
            if (source == null || target == null || sprite == null || amount <= 0)
            {
                onCompleted?.Invoke();
                return;
            }

            Play(GetScreenPosition(source), target, sprite, amount, onValueChanged, onCompleted, maxIconCount, previewText, previewDuration);
        }

        public static void PlayFromScreenPoint(
            Vector2 sourceScreenPosition,
            RectTransform target,
            Sprite sprite,
            int amount,
            Action<int> onValueChanged,
            Action onCompleted,
            int maxIconCount = 8,
            string previewText = null,
            float previewDuration = 1.5f)
        {
            if (target == null || sprite == null || amount <= 0)
            {
                onCompleted?.Invoke();
                return;
            }

            Play(sourceScreenPosition, target, sprite, amount, onValueChanged, onCompleted, maxIconCount, previewText, previewDuration);
        }

        public static void CancelAll()
        {
            for (int i = ActiveIcons.Count - 1; i >= 0; i--)
            {
                GameObject icon = ActiveIcons[i];
                if (icon == null) continue;
                icon.transform.DOKill();
                UnityEngine.Object.Destroy(icon);
            }
            ActiveIcons.Clear();

            foreach (KeyValuePair<RectTransform, TargetPulse> pair in ActiveTargetPulses)
            {
                if (pair.Key == null) continue;
                if (pair.Value.Tween != null && pair.Value.Tween.IsActive()) pair.Value.Tween.Kill(false);
                pair.Key.localScale = pair.Value.BaseScale;
            }
            ActiveTargetPulses.Clear();
        }

        private static void Play(
            Vector2 sourceScreenPosition,
            RectTransform target,
            Sprite sprite,
            int amount,
            Action<int> onValueChanged,
            Action onCompleted,
            int maxIconCount,
            string previewText,
            float previewDuration)
        {
            Canvas canvas = GetCanvas();
            if (canvas == null)
            {
                onCompleted?.Invoke();
                return;
            }

            int iconCount = Mathf.Clamp(Mathf.Min(amount, Mathf.Max(1, maxIconCount)), 1, Mathf.Max(1, amount));
            Vector2 start = ScreenToCanvasPoint(canvas.transform as RectTransform, sourceScreenPosition);
            Vector2 end = ScreenToCanvasPoint(canvas.transform as RectTransform, GetScreenPosition(target));
            int baseValue = amount / iconCount;
            int remainder = amount % iconCount;
            int pendingIcons = iconCount;

            float finalArrivalTime = FlightStartDelay + (iconCount - 1) * LaunchInterval + FlightDuration;
            if (!string.IsNullOrWhiteSpace(previewText))
            {
                CreatePreview(canvas, start, sprite, previewText, finalArrivalTime);
            }

            for (int i = 0; i < iconCount; i++)
            {
                int rewardPart = baseValue + (i < remainder ? 1 : 0);
                CreateIcon(canvas, sprite, start, end, i, () =>
                {
                    onValueChanged?.Invoke(rewardPart);
                    EnsureTargetVisible(target);
                    PulseTarget(target);

                    pendingIcons--;
                    if (pendingIcons == 0) onCompleted?.Invoke();
                });
            }
        }

        private static void CreateIcon(Canvas canvas, Sprite sprite, Vector2 start, Vector2 end, int index, Action onArrived)
        {
            GameObject root = new GameObject("FlyingRewardIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.sizeDelta = new Vector2(84f, 84f);
            rect.anchoredPosition = start;
            rect.localScale = Vector3.one * 0.8f;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            ActiveIcons.Add(root);

            Vector2 scatter = start + UnityEngine.Random.insideUnitCircle * ScatterRadius;
            Vector2 control = Vector2.Lerp(scatter, end, 0.45f) + UnityEngine.Random.insideUnitCircle * 45f;
            Sequence sequence = DOTween.Sequence().SetTarget(root).SetUpdate(true);
            sequence.Append(group.DOFade(1f, AppearDuration));
            sequence.Join(rect.DOAnchorPos(scatter, AppearDuration).SetEase(Ease.OutQuad));
            sequence.Join(rect.DOScale(1f, AppearDuration).SetEase(Ease.OutBack));
            sequence.AppendInterval(Mathf.Max(0f, FlightStartDelay - AppearDuration) + index * LaunchInterval);
            sequence.Append(DOTween.To(() => 0f, t =>
            {
                rect.anchoredPosition = QuadraticBezier(scatter, control, end, t);
                float disappearProgress = Mathf.InverseLerp(0.82f, 1f, t);
                rect.localScale = Vector3.one * (1f - disappearProgress);
            }, 1f, FlightDuration).SetEase(Ease.InQuad));
            sequence.OnComplete(() =>
            {
                ActiveIcons.Remove(root);
                if (root != null) UnityEngine.Object.Destroy(root);
                onArrived?.Invoke();
            });
        }

        private static void CreatePreview(Canvas canvas, Vector2 position, Sprite sprite, string message, float disappearAt)
        {
            s_PreviewPrefab ??= Resources.Load<UI_FlyingRewardPreviewView>(PreviewPrefabPath);
            if (s_PreviewPrefab == null)
            {
                Debug.LogWarning($"[UI_FlyingRewardEffect] Preview prefab was not found at Resources/{PreviewPrefabPath}.");
                return;
            }

            UI_FlyingRewardPreviewView preview = UnityEngine.Object.Instantiate(s_PreviewPrefab, canvas.transform);
            GameObject root = preview.gameObject;
            RectTransform rect = preview.transform as RectTransform;
            rect.anchoredPosition = position;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            rect.localScale = Vector3.one * 0.85f;
            preview.SetReward(sprite, message);
            ActiveIcons.Add(root);
            Sequence sequence = DOTween.Sequence().SetTarget(root).SetUpdate(true);
            sequence.Append(group.DOFade(1f, AppearDuration));
            sequence.Join(rect.DOScale(1f, AppearDuration).SetEase(Ease.OutBack));
            sequence.AppendInterval(Mathf.Max(0f, disappearAt - AppearDuration * 2f));
            sequence.Append(group.DOFade(0f, AppearDuration));
            sequence.OnComplete(() =>
            {
                ActiveIcons.Remove(root);
                if (root != null) UnityEngine.Object.Destroy(root);
            });
        }

        private static Canvas GetCanvas()
        {
            if (s_Canvas != null) return s_Canvas;

            GameObject root = new GameObject("FlyingRewardEffectCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            s_Canvas = root.GetComponent<Canvas>();
            s_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            s_Canvas.overrideSorting = true;
            s_Canvas.sortingOrder = short.MaxValue;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            root.GetComponent<GraphicRaycaster>().enabled = false;
            UnityEngine.Object.DontDestroyOnLoad(root);

            if (!s_Initialized)
            {
                s_Initialized = true;
                SceneManager.activeSceneChanged += (_, __) => CancelAll();
            }
            return s_Canvas;
        }

        private static Vector2 GetScreenPosition(RectTransform rect)
        {
            Canvas parentCanvas = rect.GetComponentInParent<Canvas>();
            UnityEngine.Camera camera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? parentCanvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }

        private static Vector2 ScreenToCanvasPoint(RectTransform canvas, Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPosition, null, out Vector2 result);
            return result;
        }

        private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
        }

        private static void PulseTarget(RectTransform target)
        {
            if (target == null) return;

            if (ActiveTargetPulses.TryGetValue(target, out TargetPulse active))
            {
                if (active.Tween != null && active.Tween.IsActive()) active.Tween.Kill(false);
                target.localScale = active.BaseScale;
            }

            Vector3 baseScale = target.localScale;
            Sequence sequence = DOTween.Sequence().SetTarget(target).SetUpdate(true);
            sequence.Append(target.DOScale(baseScale * 1.1f, 0.08f).SetEase(Ease.OutQuad));
            sequence.Append(target.DOScale(baseScale, 0.12f).SetEase(Ease.InQuad));
            sequence.OnComplete(() =>
            {
                if (target == null) return;
                target.localScale = baseScale;
                ActiveTargetPulses.Remove(target);
            });
            ActiveTargetPulses[target] = new TargetPulse(baseScale, sequence);
        }

        private static void EnsureTargetVisible(RectTransform target)
        {
            if (target == null) return;
            if (!target.gameObject.activeSelf) target.gameObject.SetActive(true);
            foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                graphic.enabled = true;
                Color color = graphic.color;
                color.a = 1f;
                graphic.color = color;
            }
        }

        private readonly struct TargetPulse
        {
            public readonly Vector3 BaseScale;
            public readonly Tween Tween;

            public TargetPulse(Vector3 baseScale, Tween tween)
            {
                BaseScale = baseScale;
                Tween = tween;
            }
        }
    }
}
#endif
