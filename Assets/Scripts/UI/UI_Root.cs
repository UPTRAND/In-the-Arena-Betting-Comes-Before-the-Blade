using DG.Tweening;
using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public class UI_Root : MonoBehaviour
{
    [SerializeField] private EUIObjectPoolingParent _type = EUIObjectPoolingParent.None;
    public EUIObjectPoolingParent Type => _type;

    public event Action<UI_Base> OnControlAdded;
    public event Action<UI_Base> OnControlRemoved;

    private CanvasGroup _group;
    public Canvas Canvas { get; private set; }
    public CanvasGroup CanvasGroup => _group;

    public bool IsVisible { get; private set; } = true;
    public float PPU { get; private set; }

    private void Awake()
    {
        Canvas = GetComponent<Canvas>();
        _group = GetComponent<CanvasGroup>();

        UpdatePPU();
    }

    /// <summary>
    /// 안드로이드 기기 회전(Landscape/Portrait) 및 해상도 변경 시 PPU를 자동으로 재계산합니다.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        UpdatePPU();
    }

    public void UpdatePPU()
    {
        Vector3 scale = transform.localScale;
        PPU = scale.x > 0f ? 1f / scale.x : 1f;
    }

    public void AddControl(UI_Base control)
    {
        if (control == null) return;
        OnControlAdded?.Invoke(control);
    }

    public void RemoveControl(UI_Base control)
    {
        if (control == null) return;
        OnControlRemoved?.Invoke(control);
    }

    /// <summary>
    /// 안드로이드 터치 필수 수정: 
    /// alpha만 0으로 만들면 투명한 상태로 화면 터치(Raycast)를 판정하여 뒷 배경 터치를 막아버립니다.
    /// blocksRaycasts와 interactable을 같이 제어해야 합니다.
    /// </summary>
    public void MakeVisible(bool visible)
    {
        IsVisible = visible;

        if (_group != null)
        {
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 메모리 누수 방지
        OnControlAdded = null;
        OnControlRemoved = null;
    }
}