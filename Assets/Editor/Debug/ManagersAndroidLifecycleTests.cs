#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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

    [Test]
    public void Managers_OnApplicationPause_KeepsManagersInitialized()
    {
        var child = new GameObject("LifecycleManager");
        child.transform.SetParent(_managersGo.transform, false);
        var manager = child.AddComponent<LifecycleTestManager>();
        Assert.IsTrue(manager.TryInitialize());

        FieldInfo field = typeof(Managers).GetField("_allManagers", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(_managers, new List<Manager_Base> { manager });
        MethodInfo pause = typeof(Managers).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic);
        pause.Invoke(_managers, new object[] { true });

        Assert.IsTrue(manager.IsInitialized);
        Assert.AreEqual(1, manager.PauseCount);
    }

    [TearDown]
    public void TearDown()
    {
        if (_managersGo != null)
        {
            Object.DestroyImmediate(_managersGo);
        }
    }

    private sealed class LifecycleTestManager : Manager_Base
    {
        public int PauseCount { get; private set; }
        protected override bool Init() => true;
        public override void OnApplicationPauseChanged(bool paused)
        {
            if (paused) PauseCount++;
        }
    }
}
#endif
