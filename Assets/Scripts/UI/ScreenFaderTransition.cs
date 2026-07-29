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

    public static async Awaitable PlayAsync(float duration, CancellationToken token = default)
    {
        if (duration <= 0f) return;

        ScreenFader fader = GetOrCreateFader();
        if (fader == null || fader.IsFading)
        {
            Debug.LogWarning("[ScreenFaderTransition] 페이더를 준비할 수 없거나 이미 전환 중입니다.");
            return;
        }

        var completionSource = new AwaitableCompletionSource();
        void OnFadeClosed() => completionSource.TrySetResult();

        fader.OnClosed += OnFadeClosed;
        try
        {
            // 알파 0 -> 1 -> 0의 전체 시간이 duration이 되도록 설정합니다.
            fader.FadeOut(null, true, 0f, 2f / duration);
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
        if (ScreenFader.Instance != null) return ScreenFader.Instance;
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

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        var canvasGroup = root.GetComponent<CanvasGroup>();
        var image = root.GetComponent<Image>();
        image.color = new Color32(34, 32, 52, 255);
        image.raycastTarget = true;

        var fader = root.AddComponent<ScreenFader>();
        CanvasGroupField.SetValue(fader, canvasGroup);
        FadeImageField.SetValue(fader, image);
        canvasGroup.alpha = 0f;
        return fader;
    }
}
#endif
