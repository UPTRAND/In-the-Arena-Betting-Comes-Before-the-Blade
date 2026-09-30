#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using InTheArena.Quests;
using InTheArena.Save;
using InTheArena.UI;
using UnityEditor;
using UnityEngine;

namespace InTheArena.Editor
{
    /// <summary>
    /// Play Mode의 퀘스트 일정, 배정, 복귀 처리와 발송 재시도를 확인하는 개발 창입니다.
    /// </summary>
    public sealed class QuestDebugWindow : EditorWindow
    {
        private Vector2 m_ScrollPosition;
        private string m_LastActionMessage;

        /// <summary>
        /// Unity 메뉴에서 퀘스트·우편 개발 창을 엽니다.
        /// </summary>
        [MenuItem("Tools/In The Arena/Quest and Mail Debugger")]
        public static void OpenWindow()
        {
            QuestDebugWindow window = GetWindow<QuestDebugWindow>("Quest & Mail");
            window.minSize = new Vector2(470f, 520f);
            window.Show();
        }

        /// <summary>
        /// Play Mode의 실시간 상태를 표시하기 위해 에디터 갱신 이벤트를 연결합니다.
        /// </summary>
        private void OnEnable()
        {
            EditorApplication.update -= RepaintForPlayMode;
            EditorApplication.update += RepaintForPlayMode;
        }

        /// <summary>
        /// 창이 닫힐 때 에디터 갱신 이벤트 구독을 정리합니다.
        /// </summary>
        private void OnDisable()
        {
            EditorApplication.update -= RepaintForPlayMode;
        }

        /// <summary>
        /// Play Mode에서만 창을 주기적으로 다시 그립니다.
        /// </summary>
        private void RepaintForPlayMode()
        {
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        /// <summary>
        /// 서비스, 일정, 인스턴스와 안전한 개발 명령을 한 화면에 표시합니다.
        /// </summary>
        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode에서 현재 저장 상태와 개발 명령을 사용할 수 있습니다.", MessageType.Info);
                return;
            }

            EventManager manager = EventManager.Instance;
            if (manager == null || !manager.IsReady || manager.Quests == null)
            {
                EditorGUILayout.HelpBox("EventManager의 퀘스트 서비스 초기화를 기다리는 중입니다.", MessageType.Warning);
                return;
            }

            PlayerProgressState state = SaveManager.Instance?.CreateSnapshot();
            if (state == null)
            {
                EditorGUILayout.HelpBox("저장 상태를 읽을 수 없습니다.", MessageType.Error);
                return;
            }

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
            DrawSchedule(manager, state);
            DrawStageReturn(manager, state);
            DrawAssignments(manager);
            DrawQuests(manager);
            DrawDeliveryTools(manager);
            DrawEmergencyTools(manager);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 현재 UTC와 일일·긴급 일정 정보를 표시합니다.
        /// </summary>
        private static void DrawSchedule(EventManager manager, PlayerProgressState state)
        {
            EditorGUILayout.LabelField("시간과 일정", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("현재 UTC", SaveManager.Instance.UtcNow.ToString("O"));
            EditorGUILayout.LabelField("일일 날짜", state.DailyQuestDateUtc ?? "-");
            EditorGUILayout.LabelField("다음 일일 갱신", SaveManager.Instance.UtcNow.Date.AddDays(1).ToString("O"));
            EditorGUILayout.LabelField("긴급 확률", $"{manager.Quests.EmergencyChance:P0}");
            EditorGUILayout.LabelField("오늘 긴급 생성", $"{state.EmergencyGenerationCount} / {manager.Quests.EmergencyDailyLimit}");
            EditorGUILayout.Space();
        }

        /// <summary>
        /// 현재 도전과 저장된 로비 복귀 영수증의 처리 상태를 표시합니다.
        /// </summary>
        private static void DrawStageReturn(EventManager manager, PlayerProgressState state)
        {
            EditorGUILayout.LabelField("도전과 복귀", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("현재 StageRunId", manager.CurrentStageRunId ?? "-");

            string returnId = "-";
            if (state.PendingStageReturn != null)
            {
                returnId = state.PendingStageReturn.returnId;
            }

            EditorGUILayout.LabelField("대기 ReturnId", returnId ?? "-");
            EditorGUILayout.LabelField("긴급 최초 표시 대기", state.EmergencyIntroPending.ToString());
            EditorGUILayout.Space();
        }

        /// <summary>
        /// 현재 광장 프로필과 실제 생성된 주민 ID를 표시합니다.
        /// </summary>
        private static void DrawAssignments(EventManager manager)
        {
            EditorGUILayout.LabelField("광장과 NPC", EditorStyles.boldLabel);
            LobbyPlazaController plaza = FindAnyObjectByType<LobbyPlazaController>(FindObjectsInactive.Exclude);
            if (plaza == null || plaza.CurrentProfile == null)
            {
                EditorGUILayout.LabelField("현재 ProfileId", "-");
                EditorGUILayout.LabelField("실제 NPC", "-");
                EditorGUILayout.Space();
                return;
            }

            EditorGUILayout.LabelField("현재 ProfileId", plaza.CurrentProfile.StableId);
            for (int i = 0; i < plaza.Residents.Count; i++)
            {
                LobbyNpcView resident = plaza.Residents[i];
                if (resident != null)
                {
                    EditorGUILayout.LabelField($"NPC {i + 1}", resident.NpcId);
                }
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// 모든 일일 인스턴스와 현재 긴급 인스턴스의 스냅샷을 표시합니다.
        /// </summary>
        private static void DrawQuests(EventManager manager)
        {
            EditorGUILayout.LabelField("퀘스트 인스턴스", EditorStyles.boldLabel);
            IReadOnlyList<QuestView> daily = manager.Quests.GetDailyQuests();
            for (int i = 0; i < daily.Count; i++)
            {
                DrawQuest(daily[i]);
            }

            QuestView emergency = manager.Quests.GetEmergencyQuest();
            if (emergency != null)
            {
                DrawQuest(emergency);
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// 퀘스트 하나의 정의, 목표, 진행, 상태, 기한과 NPC 배정을 표시합니다.
        /// </summary>
        private static void DrawQuest(QuestView quest)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{quest.Kind} · {quest.DefinitionId}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("InstanceId", quest.InstanceId);
            EditorGUILayout.LabelField("등급·대상", $"{quest.Tier} · {quest.Parameter}");
            EditorGUILayout.LabelField("진행", $"{quest.Progress} / {quest.Target}");
            EditorGUILayout.LabelField("상태", quest.Status.ToString());
            EditorGUILayout.LabelField("기한 UTC", new DateTime(quest.ExpiresAtUtcTicks, DateTimeKind.Utc).ToString("O"));
            EditorGUILayout.LabelField("NPC 배정", $"{quest.AssignedProfileId ?? "-"} / {quest.AssignedNpcId ?? "미배정"}");
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 저장 실패한 완료 발송을 수동으로 다시 시도하는 명령을 제공합니다.
        /// </summary>
        private void DrawDeliveryTools(EventManager manager)
        {
            EditorGUILayout.LabelField("발송 대기", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("대기 개수", manager.Quests.PendingDeliveryCount.ToString());
            if (GUILayout.Button("실패한 발송 다시 시도"))
            {
                manager.Quests.RetryPendingDeliveries(true);
                m_LastActionMessage = "발송 재시도를 요청했습니다.";
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// 실제 슬롯과 일일 상한을 지키는 고정·랜덤 긴급 생성 명령을 제공합니다.
        /// </summary>
        private void DrawEmergencyTools(EventManager manager)
        {
            EditorGUILayout.LabelField("긴급 퀘스트 개발 호출", EditorStyles.boldLabel);
            if (GUILayout.Button("랜덤 긴급 풀 호출"))
            {
                ExecuteEmergencyCreation(manager, null);
            }

            IReadOnlyList<string> definitionIds = manager.Quests.GetEmergencyDefinitionIds();
            for (int i = 0; i < definitionIds.Count; i++)
            {
                string definitionId = definitionIds[i];
                if (GUILayout.Button($"고정 호출 · {definitionId}"))
                {
                    ExecuteEmergencyCreation(manager, definitionId);
                }
            }

            if (!string.IsNullOrWhiteSpace(m_LastActionMessage))
            {
                EditorGUILayout.HelpBox(m_LastActionMessage, MessageType.Info);
            }
        }

        /// <summary>
        /// 개발용 긴급 생성 요청을 실행하고 결과 메시지를 보관합니다.
        /// </summary>
        private void ExecuteEmergencyCreation(EventManager manager, string definitionId)
        {
            manager.Quests.TryCreateEmergencyForDebug(definitionId, out m_LastActionMessage);
        }
    }
}
#endif
