#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ManagersAndroidLifecycleTests
{
    private GameObject _managersGo;
    private Managers _managers;

    [SetUp]
    public void Setup()
    {
        _managersGo = new GameObject("Managers");
        _managers = _managersGo.AddComponent<Managers>();
    }

    [UnityTest]
    public IEnumerator Managers_OnApplicationPause_Triggers_Release_Safely()
    {
        // OnApplicationPause 메시지를 강제로 전송하여 안드로이드 백그라운드 진입 시뮬레이션
        _managersGo.SendMessage("OnApplicationPause", true);

        // 함수가 예외 없이 통과했는지 확인
        Assert.IsFalse(_managers.IsInitialized);
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        if (_managersGo != null)
        {
            Object.DestroyImmediate(_managersGo);
        }
    }
}
#endif