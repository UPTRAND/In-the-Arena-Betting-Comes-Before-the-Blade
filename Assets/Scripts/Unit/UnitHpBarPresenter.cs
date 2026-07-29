#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.Unit
{
    /// <summary>
    /// 개별 World Space Canvas 대신 최대 24개의 Screen Space HP 바를 재사용합니다.
    /// </summary>
    public sealed class UnitHpBarPresenter : MonoBehaviour
    {
        private const int Capacity = 24;
        private const float VisibleDuration = 1.5f;

        private sealed class Slot
        {
            public Unit Unit;
            public RectTransform Root;
            public Image Fill;
            public float VisibleUntil;
        }

        private static UnitHpBarPresenter s_Instance;
        private readonly Slot[] m_Slots = new Slot[Capacity];
        private RectTransform m_CanvasRect;

        public static UnitHpBarPresenter Instance => s_Instance;

        public static UnitHpBarPresenter EnsureExists(Transform parent)
        {
            if (s_Instance != null) return s_Instance;

            var root = new GameObject("[UnitHpBars]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            s_Instance = root.AddComponent<UnitHpBarPresenter>();
            return s_Instance;
        }

        public static void NotifyDamaged(Unit unit)
        {
            if (unit == null) return;
            EnsureExists(UnitSimulationSystem.EnsureExists().transform).Show(unit, VisibleDuration);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            m_CanvasRect = (RectTransform)transform;
            BuildSlots();
        }

        private void BuildSlots()
        {
            for (int i = 0; i < Capacity; i++)
            {
                var root = new GameObject("HpBar_" + i, typeof(RectTransform), typeof(Image));
                root.transform.SetParent(transform, false);
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(54f, 6f);
                root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

                var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillObject.transform.SetParent(root.transform, false);
                var fillRect = (RectTransform)fillObject.transform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = new Vector2(1f, 1f);
                fillRect.offsetMax = new Vector2(-1f, -1f);
                Image fill = fillObject.GetComponent<Image>();
                fill.color = new Color(0.2f, 0.9f, 0.25f, 1f);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;

                m_Slots[i] = new Slot { Root = rect, Fill = fill };
                root.SetActive(false);
            }
        }

        private void Show(Unit unit, float duration)
        {
            Slot selected = null;
            for (int i = 0; i < m_Slots.Length; i++)
            {
                Slot slot = m_Slots[i];
                if (slot.Unit == unit)
                {
                    selected = slot;
                    break;
                }
                if (selected == null && (slot.Unit == null || slot.VisibleUntil <= Time.unscaledTime))
                    selected = slot;
            }

            if (selected == null) selected = m_Slots[0];
            selected.Unit = unit;
            selected.VisibleUntil = Time.unscaledTime + duration;
            selected.Root.gameObject.SetActive(true);
        }

        public void Refresh(UnityEngine.Camera camera)
        {
            if (camera == null || m_CanvasRect == null) return;

            for (int i = 0; i < m_Slots.Length; i++)
            {
                Slot slot = m_Slots[i];
                Unit unit = slot.Unit;
                if (unit == null || unit.IsDead || !unit.gameObject.activeInHierarchy ||
                    slot.VisibleUntil <= Time.unscaledTime)
                {
                    slot.Unit = null;
                    slot.Root.gameObject.SetActive(false);
                    continue;
                }

                Vector3 screen = camera.WorldToScreenPoint(unit.HitPosition);
                bool visible = screen.z > 0f;
                slot.Root.gameObject.SetActive(visible);
                if (!visible) continue;

                slot.Root.position = screen + Vector3.up * 12f;
                slot.Fill.fillAmount = unit.MaxHp > 0f ? unit.CurrentHp / unit.MaxHp : 0f;
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }
    }
}
#endif
