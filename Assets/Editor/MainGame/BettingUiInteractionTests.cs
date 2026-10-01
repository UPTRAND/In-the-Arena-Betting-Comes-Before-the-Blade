#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System.Reflection;
using InTheArena.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InTheArena.MainGame.Editor
{
    public sealed class BettingUiInteractionTests
    {
        private const string BettingPrefabPath = "Assets/Prefabs/UI/Panel/UI_BettingPhase.prefab";

        [TestCase(true)]
        [TestCase(false)]
        public void WagerChangesFromHandleDragAndTrackPress(bool dragHandle)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BettingPrefabPath);
            GameObject eventObject = new GameObject("BettingInputTest", typeof(EventSystem));
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(eventObject, root.scene);
                root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                foreach (CanvasGroup group in root.GetComponentsInChildren<CanvasGroup>(true))
                {
                    group.interactable = true;
                    group.blocksRaycasts = true;
                }

                Slider slider = root.GetComponentInChildren<Slider>(true);
                Assert.That(slider, Is.Not.Null);
                slider.interactable = true;
                slider.minValue = 1f;
                slider.maxValue = 100f;
                slider.wholeNumbers = true;
                slider.SetValueWithoutNotify(25f);
                Canvas.ForceUpdateCanvases();

                GameObject target = dragHandle
                    ? slider.handleRect.gameObject
                    : slider.transform.Find("Background").gameObject;
                GameObject pointerHandler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(target);
                Assert.That(pointerHandler, Is.EqualTo(slider.gameObject));

                RectTransform slideArea = slider.handleRect.parent as RectTransform;
                Vector2 endPosition = RectTransformUtility.WorldToScreenPoint(null,
                    slideArea.TransformPoint(new Vector3(
                        Mathf.Lerp(slideArea.rect.xMin, slideArea.rect.xMax, 0.9f),
                        slideArea.rect.center.y)));
                var pointer = new PointerEventData(eventObject.GetComponent<EventSystem>())
                {
                    button = PointerEventData.InputButton.Left,
                    position = dragHandle
                        ? RectTransformUtility.WorldToScreenPoint(null,
                            slider.handleRect.TransformPoint(slider.handleRect.rect.center))
                        : endPosition
                };
                ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);

                if (dragHandle)
                {
                    GameObject dragHandler = ExecuteEvents.GetEventHandler<IDragHandler>(target);
                    Assert.That(dragHandler, Is.EqualTo(slider.gameObject));
                    ExecuteEvents.Execute(dragHandler, pointer, ExecuteEvents.initializePotentialDrag);
                    pointer.position = endPosition;
                    ExecuteEvents.Execute(dragHandler, pointer, ExecuteEvents.dragHandler);
                }

                Assert.That(slider.value, Is.InRange(85f, 95f));
                ExecuteEvents.Execute(pointerHandler, pointer, ExecuteEvents.pointerUpHandler);
            }
            finally
            {
                Object.DestroyImmediate(eventObject);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ItemCountKeepsAuthoredFontSizeAndLayoutWhenPresenterInitializes()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BettingPrefabPath);
            try
            {
                Button slot = System.Array.Find(root.GetComponentsInChildren<Button>(true),
                    button => button.name == "ItemSlot_1");
                TMP_Text count = slot.transform.Find("ItemCount_Text").GetComponent<TMP_Text>();
                float fontSize = count.fontSize;
                Vector2 size = count.rectTransform.sizeDelta;
                Vector2 position = count.rectTransform.anchoredPosition;
                Vector2 anchor = count.rectTransform.anchorMin;

                UI_ItemSlotPresenter presenter = slot.gameObject.AddComponent<UI_ItemSlotPresenter>();
                typeof(UI_ItemSlotPresenter).GetMethod("Awake",
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(presenter, null);

                Assert.That(count.fontSize, Is.EqualTo(fontSize));
                Assert.That(count.rectTransform.sizeDelta, Is.EqualTo(size));
                Assert.That(count.rectTransform.anchoredPosition, Is.EqualTo(position));
                Assert.That(count.rectTransform.anchorMin, Is.EqualTo(anchor));
                Assert.That(count.raycastTarget, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
