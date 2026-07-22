using UnityEngine;

public enum EUIObjectPoolingParent
{
    None, // 패널, 팝업 등
    HUD, // 화면 고정 UI
    DynamicHUD, // 화면 고정 UI ( 동적 생성 )
    World, // 월드 공간 UI
    GroundWorld, // 월드 공간 UI ( 지면에 붙는 UI )
}
