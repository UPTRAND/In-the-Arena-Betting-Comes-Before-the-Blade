#if UNITY_6000_0_OR_NEWER
using System.Threading;
using DG.Tweening;
using UnityEngine;

namespace InTheArena.MainGame
{
    /// <summary>Continuous camera framing and panel slides that preserve authored prefab layouts.</summary>
    public static class ContinuousPhasePresentation
    {
        public static readonly Rect ArenaViewport = new Rect(0f, 0.54f, 1f, 0.36f);
        public const float SlideDuration = 0.28f;

        public static RectTransform GetResultPanel(Transform ui)
        {
            if (ui == null) return null;
            foreach (RectTransform rect in ui.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "ResultWin") return rect;
            return null;
        }

        public static async Awaitable SlideAsync(
            RectTransform panel, Vector2 restingPosition, bool reveal, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (panel == null) return;
            Vector2 hiddenPosition = GetHiddenPosition(panel, restingPosition);
            panel.DOKill();
            if (reveal) panel.anchoredPosition = hiddenPosition;
            Tween tween = panel.DOAnchorPos(reveal ? restingPosition : hiddenPosition, SlideDuration)
                .SetEase(reveal ? Ease.OutCubic : Ease.InCubic).SetUpdate(true);
            using (token.Register(() => tween.Kill()))
                await tween.AsyncWaitForCompletion();
            token.ThrowIfCancellationRequested();
        }

        private static Vector2 GetHiddenPosition(RectTransform panel, Vector2 restingPosition)
        {
            float distance = panel.rect.height * panel.localScale.y + 120f;
            if (panel.parent is RectTransform parent)
            {
                // A centered panel can be shorter than its canvas. Clear the canvas bottom as well.
                var corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                float top = float.MinValue;
                foreach (Vector3 corner in corners)
                    top = Mathf.Max(top, parent.InverseTransformPoint(corner).y);
                float restingOffset = restingPosition.y - panel.anchoredPosition.y;
                distance = Mathf.Max(distance, top + restingOffset - parent.rect.yMin + 120f);
            }
            return restingPosition + Vector2.down * distance;
        }
    }
}
#endif
