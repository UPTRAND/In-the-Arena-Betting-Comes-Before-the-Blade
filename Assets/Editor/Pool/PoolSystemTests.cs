#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PoolSystemTests
{
    private GameObject m_Root;
    private GameObject m_Prefab;
    private ObjectPoolingFactory<TestPoolComponent> m_Factory;

    [SetUp]
    public void SetUp()
    {
        TestPoolComponent.ThrowOnNextRent = false;
        m_Root = new GameObject("PoolTestRoot");
        m_Prefab = new GameObject("PoolTestPrefab");
        m_Prefab.AddComponent<TestPoolComponent>();
        m_Prefab.AddComponent<PoolMember>();
        m_Factory = new ObjectPoolingFactory<TestPoolComponent>(m_Root.transform);
    }

    [TearDown]
    public void TearDown()
    {
        m_Factory?.Clear();
        if (m_Root != null) Object.DestroyImmediate(m_Root);
        if (m_Prefab != null) Object.DestroyImmediate(m_Prefab);
    }

    [Test]
    public void Factory_ReusesReturnedObject_AndInvokesLifecycle()
    {
        Assert.IsTrue(m_Factory.Register(m_Prefab, new PoolPolicy(1, 1, PoolScope.Stage)));
        Assert.IsTrue(m_Factory.TryRent(m_Prefab, PoolSpawnContext.At(Vector3.one), out TestPoolComponent first));
        Assert.AreEqual(1, first.RentCount);
        Assert.IsTrue(m_Factory.Return(first));
        Assert.AreEqual(1, first.ReturnCount);
        Assert.IsTrue(m_Factory.TryRent(m_Prefab, PoolSpawnContext.At(Vector3.zero), out TestPoolComponent second));
        Assert.AreSame(first, second);
    }

    [Test]
    public void Factory_AtMaximum_ReturnsFailureWithoutReclaimingActiveObject()
    {
        m_Factory.Register(m_Prefab, new PoolPolicy(0, 1, PoolScope.Stage));
        Assert.IsTrue(m_Factory.TryRent(m_Prefab, PoolSpawnContext.At(Vector3.zero), out TestPoolComponent active));
        Assert.IsFalse(m_Factory.TryRent(m_Prefab, PoolSpawnContext.At(Vector3.zero), out _));
        Assert.IsTrue(active.gameObject.activeSelf);
        PoolStats stats = m_Factory.GetStats(m_Prefab);
        Assert.AreEqual(1, stats.InUse);
        Assert.AreEqual(1, stats.FailedRentCount);
    }

    [Test]
    public void Factory_RejectsDoubleReturn_AndClearsOnlyMatchingScope()
    {
        m_Factory.Register(m_Prefab, new PoolPolicy(1, 2, PoolScope.Stage));
        m_Factory.TryRent(m_Prefab, PoolSpawnContext.At(Vector3.zero), out TestPoolComponent item);
        Assert.IsTrue(m_Factory.Return(item));
        Assert.IsFalse(m_Factory.Return(item));
        m_Factory.ClearScope(PoolScope.Scene, true);
        Assert.AreEqual(1, m_Factory.GetStats(m_Prefab).Created);
        m_Factory.ClearScope(PoolScope.Stage, true);
        Assert.AreEqual(0, m_Factory.GetStats(m_Prefab).Created);
    }

    [Test]
    public void Factory_UsesPrefabReferenceInKey_WhenNamesAndTypesMatch()
    {
        var secondPrefab = new GameObject(m_Prefab.name);
        secondPrefab.AddComponent<TestPoolComponent>();
        secondPrefab.AddComponent<PoolMember>();
        try
        {
            Assert.IsTrue(m_Factory.Register(m_Prefab, new PoolPolicy(1, 1, PoolScope.Stage)));
            Assert.IsTrue(m_Factory.Register(secondPrefab, new PoolPolicy(1, 1, PoolScope.Stage)));
            Assert.AreEqual(2, m_Factory.RegisteredPoolCount);
            Assert.AreEqual(1, m_Factory.GetStats(m_Prefab).Created);
            Assert.AreEqual(1, m_Factory.GetStats(secondPrefab).Created);
        }
        finally
        {
            Object.DestroyImmediate(secondPrefab);
        }
    }

    [Test]
    public void Factory_RollsBackRent_WhenLifecycleThrows()
    {
        Assert.IsTrue(m_Factory.Register(m_Prefab, new PoolPolicy(1, 1, PoolScope.Stage)));
        TestPoolComponent.ThrowOnNextRent = true;

        LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: rent failure"));
        Assert.IsFalse(m_Factory.TryRent(
            m_Prefab,
            PoolSpawnContext.At(Vector3.zero),
            out TestPoolComponent failed));
        Assert.IsNull(failed);

        PoolStats stats = m_Factory.GetStats(m_Prefab);
        Assert.AreEqual(0, stats.InUse);
        Assert.AreEqual(1, stats.Available);
        Assert.IsTrue(m_Factory.TryRent(
            m_Prefab,
            PoolSpawnContext.At(Vector3.zero),
            out TestPoolComponent recovered));
        Assert.AreEqual(1, recovered.ReturnCount);
    }

    [Test]
    public void Factory_RejectsObjectOwnedByAnotherFactory()
    {
        var secondRoot = new GameObject("SecondPoolRoot");
        var secondFactory = new ObjectPoolingFactory<TestPoolComponent>(secondRoot.transform);
        try
        {
            var policy = new PoolPolicy(0, 1, PoolScope.Stage);
            m_Factory.Register(m_Prefab, policy);
            secondFactory.Register(m_Prefab, policy);
            m_Factory.TryRent(m_Prefab, PoolSpawnContext.At(Vector3.zero), out TestPoolComponent active);

            Assert.IsFalse(secondFactory.Return(active));
            Assert.AreEqual(1, m_Factory.GetStats(m_Prefab).InUse);
        }
        finally
        {
            secondFactory.Clear();
            Object.DestroyImmediate(secondRoot);
        }
    }

    [Test]
    public void Factory_RejectsDestroyedReturn_AndCanReplaceDestroyedObject()
    {
        m_Factory.Register(m_Prefab, new PoolPolicy(0, 1, PoolScope.Stage));
        m_Factory.TryRent(
            m_Prefab,
            PoolSpawnContext.At(Vector3.zero),
            out TestPoolComponent destroyed);

        Object.DestroyImmediate(destroyed.gameObject);
        Assert.IsFalse(m_Factory.Return(destroyed));
        Assert.IsTrue(m_Factory.TryRent(
            m_Prefab,
            PoolSpawnContext.At(Vector3.zero),
            out TestPoolComponent replacement));
        Assert.IsNotNull(replacement);
    }

    public sealed class TestPoolComponent : MonoBehaviour, IPoolLifecycle
    {
        public static bool ThrowOnNextRent;
        public int RentCount { get; private set; }
        public int ReturnCount { get; private set; }
        public void OnPoolRent(in PoolSpawnContext context)
        {
            RentCount++;
            if (!ThrowOnNextRent) return;
            ThrowOnNextRent = false;
            throw new System.InvalidOperationException("rent failure");
        }
        public void OnPoolReturn() => ReturnCount++;
    }
}
#endif
