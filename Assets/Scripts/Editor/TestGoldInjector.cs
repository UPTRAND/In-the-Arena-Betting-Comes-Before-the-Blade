using System.Reflection;
using System.Runtime.CompilerServices;
using InTheArena.MainGame;
using InTheArena.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TestGoldInjector
{
    private const int TestGold = 5000;

    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Test/Phase 1-C/Inject 5000 Gold Into Active Stage")]
    public static void InjectGold()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Phase1C Test] 먼저 Play Mode에 진입하세요.");
            return;
        }

        StageManager stageManager = StageManager.Instance;

        if (stageManager == null)
        {
            Debug.LogError("[Phase1C Test] StageManager가 없습니다.");
            return;
        }

        // 절대로 여기서 새로운 StagePlayerState를 만들지 않는다.
        if (!stageManager.IsStageRunning || stageManager.PlayerState == null)
        {
            Debug.LogError(
                "[Phase1C Test] 활성 스테이지 임시 데이터가 아직 없습니다.\n" +
                $"Scene: {SceneManager.GetActiveScene().name}\n" +
                $"IsStageRunning: {stageManager.IsStageRunning}\n" +
                "Title → Lobby → MainGame으로 진입하고 Battle HUD가 열린 뒤 실행하세요.");
            return;
        }

        StagePlayerState activeState = stageManager.PlayerState;
        int beforeGold = activeState.Gold;

        activeState.Gold = TestGold;

        RoundManager roundManager = RoundManager.Instance;
        StagePlayerState roundState =
            ReadPrivateField<StagePlayerState>(roundManager, "m_PlayerState");

        UI_BattlePhaseHUD[] huds =
            Object.FindObjectsByType<UI_BattlePhaseHUD>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        int matchingHudCount = 0;

        for (int i = 0; i < huds.Length; i++)
        {
            StagePlayerState hudState =
                ReadPrivateField<StagePlayerState>(huds[i], "m_PlayerState");

            bool sameState = ReferenceEquals(activeState, hudState);

            if (sameState)
            {
                matchingHudCount++;
            }

            Debug.Log(
                $"[Phase1C Test] HUD[{i}] " +
                $"Name={huds[i].name}, " +
                $"Active={huds[i].gameObject.activeInHierarchy}, " +
                $"SameStageState={sameState}, " +
                $"HUD Gold={(hudState != null ? hudState.Gold : -1)}");
        }

        int saveGold =
            SaveManager.Instance != null
                ? SaveManager.Instance.Gold
                : -1;

        Debug.Log(
            "[Phase1C Test] 골드 주입 완료\n" +
            $"Scene: {SceneManager.GetActiveScene().name}\n" +
            $"SaveData Gold: {saveGold}\n" +
            $"Stage Gold: {beforeGold} -> {activeState.Gold}\n" +
            $"StageState ID: {RuntimeHelpers.GetHashCode(activeState)}\n" +
            $"RoundManager SameState: {ReferenceEquals(activeState, roundState)}\n" +
            $"HUD Count: {huds.Length}\n" +
            $"Matching HUD Count: {matchingHudCount}");

        if (roundManager == null ||
            !ReferenceEquals(activeState, roundState) ||
            matchingHudCount != 1)
        {
            Debug.LogError(
                "[Phase1C Test] StagePlayerState 참조 연결이 일치하지 않습니다. " +
                "이 상태에서는 아이템 테스트를 진행하지 마세요.");
        }
    }

    private static T ReadPrivateField<T>(object target, string fieldName)
        where T : class
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field = target
            .GetType()
            .GetField(fieldName, PrivateInstance);

        return field?.GetValue(target) as T;
    }
}
