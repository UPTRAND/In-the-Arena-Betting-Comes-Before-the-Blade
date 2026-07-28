#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace InTheArena.MainGame.Editor
{
    [CustomEditor(typeof(RoundData))]
    public class RoundDataEditor : UnityEditor.Editor
    {
        private SerializedProperty m_RoundNumberProp;
        private SerializedProperty m_TeamAGridProp;
        private SerializedProperty m_TeamBGridProp;
        private SerializedProperty m_DefaultBetRatioAProp;
        private SerializedProperty m_SpecialRuleProp;

        // 팀별 접기/펼치기(Foldout) 상태
        private bool m_ShowTeamAGrid = true;
        private bool m_ShowTeamBGrid = true;

        private void OnEnable()
        {
            m_RoundNumberProp = serializedObject.FindProperty("m_RoundNumber");
            m_TeamAGridProp = serializedObject.FindProperty("m_TeamAGrid");
            m_TeamBGridProp = serializedObject.FindProperty("m_TeamBGrid");
            m_DefaultBetRatioAProp = serializedObject.FindProperty("m_DefaultBetRatioA");
            m_SpecialRuleProp = serializedObject.FindProperty("m_SpecialRule");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. 라운드 기본 정보
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("라운드 기본 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_RoundNumberProp, new GUIContent("Round Number"));
            EditorGUILayout.PropertyField(m_SpecialRuleProp, new GUIContent("Special Rule"));

            EditorGUILayout.Space(10);

            // 2. 팀 A 유닛 배치 (2x3 그리드)
            DrawTeamSection("팀 A 유닛 배치 (좌측 2x3 그리드)", m_TeamAGridProp, ref m_ShowTeamAGrid, new Color(0.9f, 0.4f, 0.4f, 0.3f));

            EditorGUILayout.Space(10);

            // 3. 팀 B 유닛 배치 (2x3 그리드)
            DrawTeamSection("팀 B 유닛 배치 (우측 2x3 그리드)", m_TeamBGridProp, ref m_ShowTeamBGrid, new Color(0.4f, 0.6f, 0.9f, 0.3f));

            EditorGUILayout.Space(10);

            // 4. 베팅 설정
            EditorGUILayout.LabelField("베팅 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_DefaultBetRatioAProp, new GUIContent("Default Bet Ratio A (%)"));

            float ratioA = m_DefaultBetRatioAProp.floatValue;
            float ratioB = 100f - ratioA;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.FloatField("Calculated Bet Ratio B (%)", ratioB);
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 팀 섹션 및 2x3 그리드 출력
        /// </summary>
        private void DrawTeamSection(string title, SerializedProperty gridProp, ref bool foldoutState, Color headerColor)
        {
            // 배경 강조 상자
            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = headerColor;
            EditorGUILayout.BeginVertical("HelpBox");
            GUI.backgroundColor = originalBg;

            foldoutState = EditorGUILayout.Foldout(foldoutState, title, true, EditorStyles.foldoutHeader);

            if (foldoutState)
            {
                // 배열 크기 보정 (6개 고정)
                if (gridProp.arraySize != 6)
                {
                    gridProp.arraySize = 6;
                }

                EditorGUILayout.Space(5);

                // 2x3 그리드 그리기 (3 행 x 2 열)
                int rows = 3;
                int cols = 2;

                for (int r = 0; r < rows; r++)
                {
                    EditorGUILayout.BeginHorizontal();

                    for (int c = 0; c < cols; c++)
                    {
                        int index = (r * cols) + c;
                        SerializedProperty cellProp = gridProp.GetArrayElementAtIndex(index);

                        DrawGridCell(cellProp, r, c, index);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 개별 그리드 셀 (GridCellData) 그리기
        /// </summary>
        private void DrawGridCell(SerializedProperty cellProp, int row, int col, int index)
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.46f));

            // 셀 헤더
            EditorGUILayout.LabelField($"[{row},{col}] Cell (#{index + 1})", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            SerializedProperty isFixedProp = cellProp.FindPropertyRelative("m_IsFixed");
            SerializedProperty fixedUnitProp = cellProp.FindPropertyRelative("m_FixedUnit");
            SerializedProperty fixedCountProp = cellProp.FindPropertyRelative("m_FixedCount");
            SerializedProperty variablePoolProp = cellProp.FindPropertyRelative("m_VariableUnitPool");
            SerializedProperty extraRangeProp = cellProp.FindPropertyRelative("m_ExtraCountRange");
            SerializedProperty spawnProbProp = cellProp.FindPropertyRelative("m_SpawnProbability");

            // 고정 여부 및 확률
            EditorGUILayout.PropertyField(isFixedProp, new GUIContent("고정 배치"));
            EditorGUILayout.PropertyField(spawnProbProp, new GUIContent("생성 확률"));

            EditorGUILayout.Space(4);

            // 고정 / 가변 조건부 노출
            if (isFixedProp.boolValue)
            {
                EditorGUILayout.HelpBox("고정 배치 모드", MessageType.None);
                EditorGUILayout.PropertyField(fixedUnitProp, new GUIContent("고정 유닛"), true);
                EditorGUILayout.PropertyField(fixedCountProp, new GUIContent("유닛 수 (1~9)"));
            }
            else
            {
                EditorGUILayout.HelpBox("가변/랜덤 배치 모드", MessageType.None);
                EditorGUILayout.PropertyField(variablePoolProp, new GUIContent("유닛 Pool"), true);
                EditorGUILayout.PropertyField(extraRangeProp, new GUIContent("추가 수 범위 (0~2)"));
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif