#if UNITY_6000_0_OR_NEWER
using System;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ScreenFader가 씬에 배치되지 않은 경우에도 전환용 오버레이를 준비하고 호출합니다.
/// </summary>
public static class ScreenFaderTransition
{
    private static readonly FieldInfo CanvasGroupField = typeof(ScreenFader).GetField(
        "m_CanvasGroup", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo FadeImageField = typeof(ScreenFader).GetField(
        "m_FadeScreenImage", BindingFlags.Instance | BindingFlags.NonPublic);

    public static async Awaitable FadeOutAsync(float duration, CancellationToken token = default)
    {
        if (duration <= 0f) return;

        ScreenFader fader = GetOrCreateFader();
        if (fader == null || fader.IsFading)
        {
            Debug.LogWarning("[ScreenFaderTransition] 페이더를 준비할 수 없거나 이미 전환 중입니다.");
            return;
        }

        var completionSource = new AwaitableCompletionSource();
        try
        {
            // 알파 0 -> 1이 되도록 설정합니다.
            fader.FadeOut(() => completionSource.TrySetResult(), false, 0f, 1f / duration);
            using (token.Register(() => completionSource.TrySetResult()))
            {
                await completionSource.Awaitable;
            }
            token.ThrowIfCancellationRequested();
        }
        finally
        {
        }
    }

    public static async Awaitable FadeInAsync(float duration, CancellationToken token = default)
    {
        if (duration <= 0f) return;

        ScreenFader fader = GetOrCreateFader();
        if (fader == null) return;
        if (fader.FadingState == ScreenFader.EFadingState.None) return;

        var completionSource = new AwaitableCompletionSource();
        void OnFadeClosed() => completionSource.TrySetResult();

        fader.OnClosed += OnFadeClosed;
        try
        {
            fader.FadeIn(1f / duration);
            using (token.Register(() => completionSource.TrySetResult()))
            {
                await completionSource.Awaitable;
            }
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            fader.OnClosed -= OnFadeClosed;
        }
    }

    private static ScreenFader GetOrCreateFader()
    {
        if (ScreenFader.Instance != null)
        {
            ConfigureOverlayCanvas(ScreenFader.Instance.gameObject);
            return ScreenFader.Instance;
        }
        if (CanvasGroupField == null || FadeImageField == null)
        {
            Debug.LogError("[ScreenFaderTransition] ScreenFader 필드를 찾을 수 없습니다.");
            return null;
        }

        var root = new GameObject(
            "[UI] ScreenFader",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(Image));
        UnityEngine.Object.DontDestroyOnLoad(root);

        var rectTransform = root.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        ConfigureOverlayCanvas(root);

        var canvasGroup = root.GetComponent<CanvasGroup>();
        var image = root.GetComponent<Image>();
        image.color = new Color32(0, 0, 0, 255);
        image.raycastTarget = true;

        var fader = root.AddComponent<ScreenFader>();
        CanvasGroupField.SetValue(fader, canvasGroup);
        FadeImageField.SetValue(fader, image);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        return fader;
    }

    private static void ConfigureOverlayCanvas(GameObject faderObject)
    {
        Canvas canvas = faderObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = faderObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32767;

        RectTransform rectTransform = faderObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
#endif
