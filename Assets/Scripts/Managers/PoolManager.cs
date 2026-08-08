#if UNITY_6000_0_OR_NEWER
using InTheArena.Unit;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PoolManager : Manager_Base
{
    private const ushort PoolInitializationOrder = 5;
    public static PoolManager Instance { get; private set; }

    [SerializeField] private PoolCatalog m_Catalog;

    private ObjectPoolingFactory<Unit> m_UnitFactory;
    private ObjectPoolingFactory<Projectile> m_ProjectileFactory;
    private ObjectPoolingFactory<UI_Poolable> m_UIFactory;

    public override ushort InitializationOrder => PoolInitializationOrder;
    public UnitPoolService Units { get; private set; }
    public ProjectilePoolService Projectiles { get; private set; }
    public UIObjectPoolingFactory UI { get; private set; }
    internal ObjectPoolingFactory<UI_Poolable> UIFactory => m_UIFactory;

    public override bool Setup()
    {
        if (Instance != null && Instance != this) return false;
        Instance = this;

        Transform root = new GameObject("[ObjectPools]").transform;
        root.SetParent(transform, false);
        m_UnitFactory = new ObjectPoolingFactory<Unit>(CreateDomainRoot(root, "Units"));
        m_ProjectileFactory = new ObjectPoolingFactory<Projectile>(CreateDomainRoot(root, "Projectiles"));
        m_UIFactory = new ObjectPoolingFactory<UI_Poolable>(CreateDomainRoot(root, "UI"));
        Units = new UnitPoolService(m_UnitFactory);
        Projectiles = new ProjectilePoolService(m_ProjectileFactory);
        UI = new UIObjectPoolingFactory(m_UIFactory);

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Application.lowMemory += OnLowMemory;
        return true;
    }

    protected override bool Init()
    {
        RegisterCatalog();
        return true;
    }

    public static PoolManager Require()
    {
        if (Instance != null) return Instance;
        PoolManager existing = FindAnyObjectByType<PoolManager>();
        if (existing != null)
        {
            existing.TryInitialize();
            return existing;
        }

        var owner = new GameObject("PoolManager");
        if (Managers.Instance != null) owner.transform.SetParent(Managers.Instance.transform, false);
        else DontDestroyOnLoad(owner);
        PoolManager created = owner.AddComponent<PoolManager>();
        created.TryInitialize();
        return created;
    }

    public void ClearScope(PoolScope scope, bool returnActive = true)
    {
        if (scope == PoolScope.Stage)
        {
            SkillVfxPresenter.ClearAllActive();
            Units?.ClearStage();
            Projectiles?.ClearStage();
            m_UIFactory?.ClearScope(scope, returnActive);
            return;
        }
        m_UnitFactory?.ClearScope(scope, returnActive);
        m_ProjectileFactory?.ClearScope(scope, returnActive);
        m_UIFactory?.ClearScope(scope, returnActive);
    }

    public void ClearRound()
    {
        SkillVfxPresenter.ClearAllActive();
        Projectiles?.ClearRound();
        ClearScope(PoolScope.Round);
    }
    public void ClearStage() => ClearScope(PoolScope.Stage);

    public override void Release()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Application.lowMemory -= OnLowMemory;
        m_UnitFactory?.Clear();
        m_ProjectileFactory?.Clear();
        m_UIFactory?.Clear();
        Units = null;
        Projectiles = null;
        UI = null;
        if (Instance == this) Instance = null;
        base.Release();
    }

    protected override void OnDestroy()
    {
        Release();
        base.OnDestroy();
    }

    private static Transform CreateDomainRoot(Transform parent, string name)
    {
        Transform root = new GameObject(name).transform;
        root.SetParent(parent, false);
        return root;
    }

    private void RegisterCatalog()
    {
        if (m_Catalog == null || m_Catalog.Entries == null) return;
        for (int i = 0; i < m_Catalog.Entries.Count; i++)
        {
            PoolCatalog.Entry entry = m_Catalog.Entries[i];
            if (entry == null || entry.Prefab == null) continue;
            if (entry.Domain == PoolDomain.UI) UI.Register(entry.Prefab, entry.Policy);
            else Projectiles.Register(entry.Prefab, entry.Policy);
        }
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
        => ClearScope(PoolScope.Scene);

    private void OnLowMemory()
    {
        m_UnitFactory?.Trim(PoolScope.Stage);
        m_ProjectileFactory?.Trim(PoolScope.Stage);
        m_UIFactory?.Trim(PoolScope.Scene);
        m_UIFactory?.Trim(PoolScope.Persistent);
    }
}
#endif
