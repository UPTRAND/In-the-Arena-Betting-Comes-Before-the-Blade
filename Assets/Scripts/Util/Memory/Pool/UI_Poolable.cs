#if UNITY_6000_0_OR_NEWER
using UnityEngine;

[DisallowMultipleComponent]
public abstract class UI_Poolable : Poolable, IUIBase
{
    [SerializeField] private EUIObjectPoolingParent m_ParentType = EUIObjectPoolingParent.HUD;

    public UI_Root ParentRoot { get; private set; }
    public bool BIsOpened => gameObject.activeSelf;
    public virtual bool BIsSearchedByTypeHash => true;

    public EUIObjectPoolingParent GetParent() => m_ParentType;

    public void SetRoot(UI_Root parent)
    {
        ParentRoot = parent;
        if (parent != null)
        {
            transform.SetParent(parent.transform, false);
        }
    }

    public virtual void Open()
    {
        gameObject.SetActive(true);
    }

    public virtual void Close()
    {
        Despawn();
    }
}
#endif