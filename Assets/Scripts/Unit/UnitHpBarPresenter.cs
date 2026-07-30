#if UNITY_6000_0_OR_NEWER
using InTheArena.UI;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 유닛 피격 시 UI_UnitHPBar.prefab을 인스턴스화하고 [UI] World Canvas 하위에서 관리하는 헬퍼 클래스입니다.
    /// </summary>
    public sealed class UnitHpBarPresenter : MonoBehaviour
    {
        private const string PrefabPath = "Assets/Prefabs/UI/World/UI_UnitHPBar.prefab";

        private static UnitHpBarPresenter s_Instance;
        [SerializeField] private GameObject m_HpBarPrefab;
        private Transform m_WorldUiParent;

        public static UnitHpBarPresenter Instance => s_Instance;

        public static UnitHpBarPresenter EnsureExists()
        {
            if (s_Instance != null)
            {
                return s_Instance;
            }

            GameObject presenterObj = new GameObject("[UnitHpBarPresenter]");
            s_Instance = presenterObj.AddComponent<UnitHpBarPresenter>();
            return s_Instance;
        }

        public static UnitHpBarPresenter EnsureExists(Transform parent)
        {
            UnitHpBarPresenter instance = EnsureExists();
            if (parent != null && instance.transform.parent == null)
            {
                instance.transform.SetParent(parent, false);
            }
            return instance;
        }

        public static void NotifyDamaged(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            EnsureExists().ShowHpBarForUnit(unit, 1.5f);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            LoadPrefabIfNeeded();
            FindWorldUiParent();
        }

        private void LoadPrefabIfNeeded()
        {
            if (m_HpBarPrefab != null)
            {
                return;
            }

#if UNITY_EDITOR
            m_HpBarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
            if (m_HpBarPrefab == null)
            {
                m_HpBarPrefab = Resources.Load<GameObject>("UI/World/UI_UnitHPBar");
            }
        }

        /// <summary>
        /// MainGame Scene 하이어라키에서 "[UI] World" 오브젝트를 탐색하여 부모로 저장합니다.
        /// </summary>
        private void FindWorldUiParent()
        {
            if (UIManager.Instance != null)
            {
                UI_Root worldRoot = UIManager.Instance.GetRootFromType(EUIObjectPoolingParent.World);
                if (worldRoot != null)
                {
                    m_WorldUiParent = worldRoot.transform;
                    return;
                }
            }

            GameObject worldObj = GameObject.Find("[UI] World");
            if (worldObj != null)
            {
                m_WorldUiParent = worldObj.transform;
            }
            else
            {
                m_WorldUiParent = transform;
            }
        }

        /// <summary>
        /// Assets/Prefabs/UI/World/UI_UnitHPBar.prefab을 인스턴스화하여 [UI] World 하위에 바인딩합니다.
        /// </summary>
        public void ShowHpBarForUnit(Unit unit, float duration = 1.5f)
        {
            if (unit == null || unit.IsDead)
            {
                return;
            }

            if (m_WorldUiParent == null)
            {
                FindWorldUiParent();
            }

            if (m_HpBarPrefab == null)
            {
                LoadPrefabIfNeeded();
            }

            UI_UnitHPBar hpBar = unit.HpBar;
            if (hpBar == null)
            {
                GameObject barObj = null;
                if (m_HpBarPrefab != null)
                {
                    barObj = Instantiate(m_HpBarPrefab, m_WorldUiParent);
                }
                else
                {
                    barObj = new GameObject("HpBar_" + unit.name, typeof(RectTransform));
                    if (m_WorldUiParent != null)
                    {
                        barObj.transform.SetParent(m_WorldUiParent, false);
                    }
                    else
                    {
                        barObj.transform.SetParent(transform, false);
                    }
                }

                barObj.name = "HpBar_" + unit.name;
                hpBar = barObj.GetComponent<UI_UnitHPBar>();
                if (hpBar == null)
                {
                    hpBar = barObj.AddComponent<UI_UnitHPBar>();
                }

                hpBar.SetTarget(unit);
                unit.HpBar = hpBar;
            }

            hpBar.ShowHpBar(duration);
        }

        public void Refresh(UnityEngine.Camera camera = null)
        {
            // 각 UI_UnitHPBar의 LateUpdate에서 개별 갱신되므로 호환성용 유지
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }
    }
}
#endif
