#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RoundManagerSceneBoundTests
{
    private GameObject _roundManagerGo;

    [SetUp]
    public void Setup()
    {
        _roundManagerGo = new GameObject("RoundManager");
        _roundManagerGo.AddComponent<RoundManager>();
    }

    [UnityTest]
    public IEnumerator RoundManager_CleansUp_Safely_On_Scene_Unload()
    {
        Assert.IsNotNull(RoundManager.Instance);

        // MainGame 씬 언로드 시뮬레이션 (오브젝트 파괴)
        Object.DestroyImmediate(_roundManagerGo);

        yield return null;

        // Persistent 매니저 참조 오염 없이 안전하게 null 반환 확인
        Assert.IsNull(RoundManager.Instance);
        LogAssert.NoUnexpectedReceived();
    }
}
#endif