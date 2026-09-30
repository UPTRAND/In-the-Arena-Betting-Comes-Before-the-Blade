#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using InTheArena.Battlefield;
using InTheArena.MainGame;
using InTheArena.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CombatItemTargetingTests
{
    [Test]
    public void BattlefieldArea_RaycastAcceptsOnlyColliderAndNormalizesY()
    {
        GameObject areaObject = new GameObject("TestBattlefield");
        GameObject cameraObject = new GameObject("TestCamera");

        try
        {
            BoxCollider collider = areaObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0f, 0f);
            collider.size = new Vector3(10f, 0.2f, 4f);
            areaObject.transform.position = new Vector3(0f, 2f, 0f);
            BattlefieldArea area = areaObject.AddComponent<BattlefieldArea>();

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.aspect = 1f;
            camera.pixelRect = new Rect(0f, 0f, 100f, 100f);
            camera.transform.position = new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Assert.That(
                area.TryGetGroundPosition(
                    camera,
                    new Vector2(50f, 50f),
                    out Vector3 inside),
                Is.True);
            Assert.That(inside.y, Is.EqualTo(2f).Within(0.001f));
            Assert.That(inside.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(inside.z, Is.EqualTo(0f).Within(0.001f));

            Assert.That(
                area.TryGetGroundPosition(
                    camera,
                    new Vector2(99f, 50f),
                    out _),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(areaObject);
        }
    }

    [Test]
    public void BattlefieldArea_FormationPaddingKeepsAllThreeMercenariesInside()
    {
        GameObject areaObject = new GameObject("TestBattlefield");

        try
        {
            BoxCollider collider = areaObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(14f, 0.2f, 6f);
            BattlefieldArea area = areaObject.AddComponent<BattlefieldArea>();

            Vector3 center = area.ClampPosition(
                new Vector3(100f, 0f, 100f),
                1f);
            Vector3 knight = center;
            Vector3 archer = center + new Vector3(0.5f, 0f, -0.5f);
            Vector3 wizard = center + new Vector3(-0.5f, 0f, -0.5f);

            Assert.That(area.ContainsPosition(knight, 0.5f), Is.True);
            Assert.That(area.ContainsPosition(archer, 0.5f), Is.True);
            Assert.That(area.ContainsPosition(wizard, 0.5f), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(areaObject);
        }
    }

    [Test]
    public void CombatPhase_ItemCastingSlowMotionBlocksToggleAndRestoresSelectedSpeed()
    {
        GameObject phaseObject = new GameObject("TestCombatPhase");
        float previousTimeScale = Time.timeScale;

        try
        {
            CombatPhase phase = phaseObject.AddComponent<CombatPhase>();
            typeof(CombatPhase)
                .GetField("m_CurrentSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(phase, 2f);
            Time.timeScale = 2f;

            Assert.That(phase.BeginItemCastingSlowMotion(), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(phase.DisplaySpeed, Is.EqualTo(0.25f).Within(0.0001f));

            phase.ToggleCombatSpeed();
            Assert.That(Time.timeScale, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(phase.CurrentSpeed, Is.EqualTo(2f).Within(0.0001f));

            phase.EndItemCastingSlowMotion();
            Assert.That(Time.timeScale, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(phase.DisplaySpeed, Is.EqualTo(2f).Within(0.0001f));
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(phaseObject);
        }
    }

    [TestCase(FactionPrediction.Red, ItemType.Meteor, Team.Blue)]
    [TestCase(FactionPrediction.Blue, ItemType.Meteor, Team.Red)]
    [TestCase(FactionPrediction.Red, ItemType.Mercenary, Team.Red)]
    [TestCase(FactionPrediction.Blue, ItemType.Mercenary, Team.Blue)]
    [TestCase(FactionPrediction.Draw, ItemType.Meteor, Team.None)]
    [TestCase(FactionPrediction.Draw, ItemType.Mercenary, Team.None)]
    public void TeamResolver_UsesMainBet(FactionPrediction faction, ItemType item, Team expected)
    {
        var ticket = new RoundBetTicket();
        ticket.SetFaction(faction);
        Assert.That(CombatItemTeamResolver.Resolve(ticket, item), Is.EqualTo(expected));
    }

    [Test]
    public void Meteor_StunsOnlyTargetTeamAtAnyDistanceForThreeSeconds()
    {
        CombatPhase phase = CreateCombat(out RoundContext context);
        var stun = UnityEditor.AssetDatabase.LoadAssetAtPath<InTheArena.Unit.StatusEffectData>(
            "Assets/ScriptableObject/Unit/Unit_Effect/Examples/Status_Stun.asset");
        Assert.That(stun, Is.Not.Null);
        SetField(phase, "m_MeteorStunEffect", stun);
        var red = CreateUnit(Team.Red);
        var near = CreateUnit(Team.Blue);
        var far = CreateUnit(Team.Blue);
        var dead = CreateUnit(Team.Blue);
        near.transform.position = Vector3.zero;
        far.transform.position = new Vector3(100f, 0f, 100f);
        SetField(dead, "m_CurrentHp", 0f);
        context.TeamAUnits.Add(red);
        context.TeamBUnits.AddRange(new[] { near, far, dead });

        Assert.That(phase.TryApplyMeteorEffect(Team.Blue, out string message), Is.True, message);
        Assert.That(red.ActiveDataEffects, Is.Empty);
        Assert.That(near.IsStunned, Is.True);
        Assert.That(far.IsStunned, Is.True);
        Assert.That(near.ActiveDataEffects[0].RemainingTime, Is.EqualTo(3f));
        Assert.That(far.ActiveDataEffects[0].RemainingTime, Is.EqualTo(3f));
        Assert.That(dead.ActiveDataEffects, Is.Empty);
    }

    [TestCase(Team.Red, -6f)]
    [TestCase(Team.Blue, 6f)]
    public void Mercenary_AutomaticPositionIsRearCenterAndFitsFormation(Team team, float x)
    {
        CombatPhase phase = CreateCombat(out _);
        GameObject areaObject = Track(new GameObject("AutomaticMercenaryArea"));
        var collider = areaObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(14f, 0.2f, 6f);
        var area = areaObject.AddComponent<BattlefieldArea>();
        Vector3 center = phase.GetMercenarySpawnPosition(team);
        Assert.That(center.x, Is.EqualTo(x).Within(0.001f));
        Assert.That(center.z, Is.EqualTo(0f).Within(0.001f));
        Assert.That(area.ContainsPosition(center, 0.5f), Is.True);
        Assert.That(area.ContainsPosition(center + new Vector3(0.5f, 0f, -0.5f), 0.5f), Is.True);
        Assert.That(area.ContainsPosition(center + new Vector3(-0.5f, 0f, -0.5f), 0.5f), Is.True);
    }

    [Test]
    public void FirstEliminatedColumn_RecordsEachTeamAfterOpposingTeamGoesFirst()
    {
        CombatPhase phase = CreateCombat(out RoundContext context);
        var redFront = CreateUnit(Team.Red);
        var redBack = CreateUnit(Team.Red);
        var blueFront = CreateUnit(Team.Blue);
        var blueBack = CreateUnit(Team.Blue);
        context.TeamAUnits.AddRange(new[] { redFront, redBack });
        context.TeamBUnits.AddRange(new[] { blueFront, blueBack });
        var register = typeof(CombatPhase).GetMethod("RegisterUnitSlot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        register.Invoke(phase, new object[] { redFront, Team.Red, 1 });
        register.Invoke(phase, new object[] { redBack, Team.Red, 0 });
        register.Invoke(phase, new object[] { blueFront, Team.Blue, 0 });
        register.Invoke(phase, new object[] { blueBack, Team.Blue, 1 });
        blueFront.ApplyDamage(10000f);
        redFront.ApplyDamage(10000f);
        var build = typeof(CombatPhase).GetMethod("BuildCombatResult", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var result = (CombatResultSnapshot)build.Invoke(phase, new object[] { Team.Red });
        Assert.That(result.FirstEliminatedColumn, Is.EqualTo(FirstEliminatedColumnPrediction.BlueFront));
        Assert.That(result.GetFirstEliminatedColumn(Team.Red), Is.EqualTo(FirstEliminatedColumnPrediction.RedFront));
        Assert.That(result.GetFirstEliminatedColumn(Team.Blue), Is.EqualTo(FirstEliminatedColumnPrediction.BlueFront));
    }

    [TestCase(ItemType.Meteor)]
    [TestCase(ItemType.Mercenary)]
    public void TeamSelection_ConfirmOrCancelRestoresSpeedAndIsOnceOnly(ItemType item)
    {
        CombatPhase phase = CreateCombat(out RoundContext context);
        context.TeamAUnits.Add(CreateUnit(Team.Red));
        context.TeamBUnits.Add(CreateUnit(Team.Blue));
        SetField(phase, "m_CurrentSpeed", 2f);
        Time.timeScale = 2f;
        GameObject root = Track(new GameObject("TeamPicker", typeof(RectTransform)));
        var picker = root.AddComponent<UI_CombatItemTeamSelectionController>();
        int confirms = 0;
        int cancels = 0;
        picker.TeamConfirmed += team => { Assert.That(team, Is.EqualTo(Team.Blue)); confirms++; };
        picker.SelectionCanceled += () => cancels++;
        Assert.That(picker.BeginSelection(item, phase), Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(0.25f));
        picker.ChooseTeam(Team.Blue);
        picker.ChooseTeam(Team.Red);
        Assert.That(confirms, Is.EqualTo(1));
        Assert.That(Time.timeScale, Is.EqualTo(2f));
        Assert.That(picker.BeginSelection(item, phase), Is.True);
        picker.CancelSelection();
        picker.CancelSelection();
        Assert.That(cancels, Is.EqualTo(1));
        Assert.That(Time.timeScale, Is.EqualTo(2f));
        Assert.That(picker.BeginSelection(item, phase), Is.True);
        root.SetActive(false);
        // EditMode에서는 런타임 비활성화 콜백을 명시적으로 전달한다.
        typeof(UI_CombatItemTeamSelectionController).GetMethod("OnDisable",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(picker, null);
        Assert.That(cancels, Is.EqualTo(2));
        Assert.That(phase.IsItemCastingSlowMotion, Is.False);
    }

    [Test]
    public void TeamSelection_CombatEndRejectsLateChoiceAndRestoresTime()
    {
        CombatPhase phase = CreateCombat(out RoundContext context);
        context.TeamAUnits.Add(CreateUnit(Team.Red));
        context.TeamBUnits.Add(CreateUnit(Team.Blue));
        GameObject root = Track(new GameObject("EndedTeamPicker", typeof(RectTransform)));
        var picker = root.AddComponent<UI_CombatItemTeamSelectionController>();
        int confirms = 0;
        int cancels = 0;
        picker.TeamConfirmed += _ => confirms++;
        picker.SelectionCanceled += () => cancels++;
        Assert.That(picker.BeginSelection(ItemType.Meteor, phase), Is.True);
        SetField(phase, "m_IsCombatEnded", true);
        picker.ChooseTeam(Team.Blue);
        Assert.That(confirms, Is.Zero);
        Assert.That(cancels, Is.EqualTo(1));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(picker.IsSelecting, Is.False);
    }

    [Test]
    public void TimeExtension_RejectsEndedCombatBeforeApplyingOrConsuming()
    {
        CombatPhase phase = CreateCombat(out _);
        SetField(phase, "m_IsCombatEnded", true);
        var item = Track(ScriptableObject.CreateInstance<ItemData>());
        SetField(item, "m_ItemType", ItemType.TimeExtension);
        var executor = new CombatTimeExtensionUseExecutor(phase);
        Assert.That(executor.CanExecute(item, out _), Is.False);
        Assert.That(executor.TryExecute(item, out _), Is.False);
        Assert.That(phase.RemainingCombatTime, Is.EqualTo(30f));
    }

    private readonly System.Collections.Generic.List<Object> m_Created = new System.Collections.Generic.List<Object>();
    private float m_PreviousSpeed;

    [SetUp]
    public void SetUp()
    {
        m_PreviousSpeed = Time.timeScale;
        InTheArena.Unit.BattleSimulation.PrepareBattle();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = m_Created.Count - 1; i >= 0; i--)
            if (m_Created[i] != null) Object.DestroyImmediate(m_Created[i]);
        m_Created.Clear();
        InTheArena.Unit.UnitRegistry.Clear();
        Time.timeScale = m_PreviousSpeed;
    }

    private T Track<T>(T obj) where T : Object
    {
        m_Created.Add(obj);
        return obj;
    }

    private CombatPhase CreateCombat(out RoundContext context)
    {
        var phase = Track(new GameObject("TeamItemCombat")).AddComponent<CombatPhase>();
        context = new RoundContext { CurrentRound = 1 };
        phase.InitializePhase(context);
        SetField(phase, "m_RemainingCombatTime", 30f);
        return phase;
    }

    private InTheArena.Unit.Unit CreateUnit(Team team)
    {
        var data = Track(ScriptableObject.CreateInstance<InTheArena.Unit.UnitData>());
        SetField(data, "m_BaseStat", InTheArena.Unit.UnitStat.Default);
        var root = Track(new GameObject("TeamItemUnit"));
        root.AddComponent<BoxCollider>();
        var unit = root.AddComponent<InTheArena.Unit.Unit>();
        unit.Initialize(data, (int)team);
        return unit;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
    }
}
#endif
