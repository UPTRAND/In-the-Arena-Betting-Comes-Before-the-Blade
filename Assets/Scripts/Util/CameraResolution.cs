using UnityEngine;

/// <summary>
/// Renders the Main Camera inside a centered 9:16 viewport on every device.
/// </summary>
public static class CameraResolution
{
    private const float TargetAspect = 9f / 16f;
    private const float Epsilon = 0.0001f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        Application.onBeforeRender -= Apply;
        Application.onBeforeRender += Apply;
        Apply();
    }

    private static void Apply()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        float screenAspect = (float)Screen.width / Screen.height;
        Rect targetRect;

        if (screenAspect < TargetAspect)
        {
            float height = screenAspect / TargetAspect;
            targetRect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }
        else
        {
            float width = TargetAspect / screenAspect;
            targetRect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        if (Approximately(mainCamera.rect, targetRect))
            return;

        mainCamera.rect = targetRect;
    }

    private static bool Approximately(Rect lhs, Rect rhs)
    {
        return Mathf.Abs(lhs.x - rhs.x) < Epsilon &&
               Mathf.Abs(lhs.y - rhs.y) < Epsilon &&
               Mathf.Abs(lhs.width - rhs.width) < Epsilon &&
               Mathf.Abs(lhs.height - rhs.height) < Epsilon;
    }
}
