/*
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using InTheArena.MainGame;

namespace InTheArena.Editor.MainGame
{
    /// <summary>
    /// RoundData의 Custom PropertyDrawer - 2x3 그리드 바둑판 레이아웃을 인스펙터에 시각화
    /// Red팀(좌측)과 Blue팀(우측) 각각 설정 가능
    /// </summary>
    [CustomPropertyDrawer(typeof(RoundData))]
    public class RoundDataPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.style.paddingBottom = 10;

            // 스크립트 참조 표시 (읽기 전용)
            var scriptProp = property.FindPropertyRelative("m_Script");
            if (scriptProp != null)
            {
                var scriptField = new PropertyField(scriptProp);
                scriptField.SetEnabled(false);
                root.Add(scriptField);
            }

            // 라운드 번호
            var roundNumberProp = property.FindPropertyRelative("m_RoundNumber");
            root.Add(new PropertyField(roundNumberProp));

            // 팀 A 그리드 (Red Team - 좌측)
            var teamAGridProp = property.FindPropertyRelative("m_TeamAGrid");
            root.Add(CreateTeamGridSection("Team A (Red - 좌측 2x3)", teamAGridProp, new Color(1f, 0.3f, 0.3f, 0.1f)));

            // 팀 B 그리드 (Blue Team - 우측)
            var teamBGridProp = property.FindPropertyRelative("m_TeamBGrid");
            root.Add(CreateTeamGridSection("Team B (Blue - 우측 2x3)", teamBGridProp, new Color(0.3f, 0.5f, 1f, 0.1f)));

            // 특별 규칙
            var specialRuleProp = property.FindPropertyRelative("m_SpecialRule");
            root.Add(new PropertyField(specialRuleProp));

            return root;
        }

        private VisualElement CreateTeamGridSection(string title, SerializedProperty gridProp, Color backgroundColor)
        {
            var section = new VisualElement();
            section.style.marginTop = 10;
            section.style.marginBottom = 10;
            section.style.paddingLeft = 10;
            section.style.paddingRight = 10;
            section.style.paddingTop = 5;
            section.style.paddingBottom = 5;
            section.style.backgroundColor = backgroundColor;
            section.style.borderTopWidth = 1;
            section.style.borderBottomWidth = 1;
            section.style.borderLeftWidth = 1;
            section.style.borderRightWidth = 1;
            section.style.borderTopColor = Color.gray;
            section.style.borderBottomColor = Color.gray;
            section.style.borderLeftColor = Color.gray;
            section.style.borderRightColor = Color.gray;

            // 제목
            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.marginBottom = 5;
            section.Add(titleLabel);

            // 그리드 컨테이너 (2행 x 3열)
            var gridContainer = new VisualElement();
            gridContainer.style.flexDirection = FlexDirection.Column;
            gridContainer.style.alignItems = Align.Center;
            
            for (int row = 0; row < 2; row++)
            {
                var rowContainer = new VisualElement();
                rowContainer.style.flexDirection = FlexDirection.Row;
                rowContainer.style.marginBottom = 2;

                for (int col = 0; col < 3; col++)
                {
                    int index = row * 3 + col;
                    var cellProp = gridProp.GetArrayElementAtIndex(index);
                    var cellElement = CreateGridCellElement(cellProp, index, row, col);
                    rowContainer.Add(cellElement);
                }

                gridContainer.Add(rowContainer);
            }

            section.Add(gridContainer);

            // 범례
            var legend = new Label("■ 고정(고정 유닛/수량)  □ 가변(랜덤 풀/추가 0~2)");
            legend.style.fontSize = 9;
            legend.style.color = Color.gray;
            legend.style.unityTextAlign = TextAnchor.MiddleCenter;
            legend.style.marginTop = 5;
            section.Add(legend);

            return section;
        }

        private VisualElement CreateGridCellElement(SerializedProperty cellProp, int index, int row, int col)
        {
            var cellContainer = new VisualElement();
            cellContainer.style.width = 100;
            cellContainer.style.height = 80;
            cellContainer.style.marginRight = 4;
            cellContainer.style.marginLeft = 4;
            cellContainer.style.paddingLeft = 5;
            cellContainer.style.paddingRight = 5;
            cellContainer.style.paddingTop = 3;
            cellContainer.style.paddingBottom = 3;
            cellContainer.style.borderTopWidth = 2;
            cellContainer.style.borderBottomWidth = 2;
            cellContainer.style.borderLeftWidth = 2;
            cellContainer.style.borderRightWidth = 2;
            cellContainer.style.borderTopColor = Color.gray;
            cellContainer.style.borderBottomColor = Color.gray;
            cellContainer.style.borderLeftColor = Color.gray;
            cellContainer.style.borderRightColor = Color.gray;
            cellContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            cellContainer.style.flexDirection = FlexDirection.Column;
            cellContainer.style.alignItems = Align.Center;
            cellContainer.style.justifyContent = Justify.Center;

            // 칸 번호 라벨
            var indexLabel = new Label($"[{row},{col}]");
            indexLabel.style.fontSize = 10;
            indexLabel.style.color = Color.cyan;
            indexLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            indexLabel.style.marginBottom = 2;
            cellContainer.Add(indexLabel);

            // 고정/가변 토글
            var isFixedProp = cellProp.FindPropertyRelative("m_IsFixed");
            var isFixedField = new PropertyField(isFixedProp, "고정");
            isFixedField.style.marginBottom = 2;
            cellContainer.Add(isFixedField);

            // 고정 유닛 필드 (고정일 때만 표시)
            var fixedUnitProp = cellProp.FindPropertyRelative("m_FixedUnit");
            var fixedUnitField = new PropertyField(fixedUnitProp, "유닛");
            fixedUnitField.style.marginBottom = 2;
            cellContainer.Add(fixedUnitField);

            // 고정 수량
            var fixedCountProp = cellProp.FindPropertyRelative("m_FixedCount");
            var fixedCountField = new PropertyField(fixedCountProp, "수량");
            fixedCountField.style.marginBottom = 2;
            cellContainer.Add(fixedCountField);

            // 가변 유닛 풀
            var variablePoolProp = cellProp.FindPropertyRelative("m_VariableUnitPool");
            var variablePoolField = new PropertyField(variablePoolProp, "풀");
            variablePoolField.style.marginBottom = 2;
            cellContainer.Add(variablePoolField);

            // 추가 수량 범위
            var extraCountProp = cellProp.FindPropertyRelative("m_ExtraCountRange");
            var extraCountField = new PropertyField(extraCountProp, "추가");
            cellContainer.Add(extraCountField);

            // 생성 확률
            var spawnProbProp = cellProp.FindPropertyRelative("m_SpawnProbability");
            var spawnProbField = new PropertyField(spawnProbProp, "확률");
            cellContainer.Add(spawnProbField);

            // 고정/가변에 따른 필드 표시/숨김 바인딩
            isFixedField.RegisterValueChangeCallback(evt =>
            {
                bool isFixed = evt.changedProperty.boolValue;
                fixedUnitField.style.display = isFixed ? DisplayStyle.Flex : DisplayStyle.None;
                fixedCountField.style.display = isFixed ? DisplayStyle.Flex : DisplayStyle.None;
                variablePoolField.style.display = isFixed ? DisplayStyle.None : DisplayStyle.Flex;
                extraCountField.style.display = isFixed ? DisplayStyle.None : DisplayStyle.Flex;
            });

            // 초기 상태 설정
            bool initialFixed = isFixedProp.boolValue;
            fixedUnitField.style.display = initialFixed ? DisplayStyle.Flex : DisplayStyle.None;
            fixedCountField.style.display = initialFixed ? DisplayStyle.Flex : DisplayStyle.None;
            variablePoolField.style.display = initialFixed ? DisplayStyle.None : DisplayStyle.Flex;
            extraCountField.style.display = initialFixed ? DisplayStyle.None : DisplayStyle.Flex;

            return cellContainer;
        }
    }

    /// <summary>
    /// GridCellData의 PropertyDrawer (인라인 표시용)
    /// </summary>
    [CustomPropertyDrawer(typeof(GridCellData))]
    public class GridCellDataPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.style.paddingLeft = 5;
            root.style.paddingRight = 5;
            root.style.paddingTop = 2;
            root.style.paddingBottom = 2;

            var isFixedProp = property.FindPropertyRelative("m_IsFixed");
            var fixedUnitProp = property.FindPropertyRelative("m_FixedUnit");
            var fixedCountProp = property.FindPropertyRelative("m_FixedCount");
            var variablePoolProp = property.FindPropertyRelative("m_VariableUnitPool");
            var extraCountProp = property.FindPropertyRelative("m_ExtraCountRange");
            var spawnProbProp = property.FindPropertyRelative("m_SpawnProbability");

            // 첫 번째 줄: 고정/가변 토글 + 확률
            var line1 = new VisualElement();
            line1.style.flexDirection = FlexDirection.Row;
            line1.style.marginBottom = 3;
            
            var isFixedField = new PropertyField(isFixedProp, "고정");
            isFixedField.style.flexGrow = 1;
            line1.Add(isFixedField);

            var spawnProbField = new PropertyField(spawnProbProp, "확률");
            spawnProbField.style.width = 80;
            line1.Add(spawnProbField);
            root.Add(line1);

            // 두 번째 줄: 고정 유닛 + 수량 (고정일 때)
            var fixedLine = new VisualElement();
            fixedLine.style.flexDirection = FlexDirection.Row;
            fixedLine.style.marginBottom = 3;
            
            var fixedUnitField = new PropertyField(fixedUnitProp, "유닛");
            fixedUnitField.style.flexGrow = 1;
            fixedLine.Add(fixedUnitField);

            var fixedCountField = new PropertyField(fixedCountProp, "수량");
            fixedCountField.style.width = 60;
            fixedLine.Add(fixedCountField);
            root.Add(fixedLine);

            // 세 번째 줄: 가변 풀 + 추가 수량 (가변일 때)
            var variableLine = new VisualElement();
            variableLine.style.flexDirection = FlexDirection.Row;
            variableLine.style.marginBottom = 3;
            
            var variablePoolField = new PropertyField(variablePoolProp, "풀");
            variablePoolField.style.flexGrow = 1;
            variableLine.Add(variablePoolField);

            var extraCountField = new PropertyField(extraCountProp, "추가");
            extraCountField.style.width = 60;
            variableLine.Add(extraCountField);
            root.Add(variableLine);

            // 고정/가변에 따른 표시/숨김 동기화
            void UpdateVisibility()
            {
                bool isFixed = isFixedProp.boolValue;
                fixedUnitField.style.display = isFixed ? DisplayStyle.Flex : DisplayStyle.None;
                fixedCountField.style.display = isFixed ? DisplayStyle.Flex : DisplayStyle.None;
                fixedLine.style.display = isFixed ? DisplayStyle.Flex : DisplayStyle.None;
                variablePoolField.style.display = isFixed ? DisplayStyle.None : DisplayStyle.Flex;
                extraCountField.style.display = isFixed ? DisplayStyle.None : DisplayStyle.Flex;
                variableLine.style.display = isFixed ? DisplayStyle.None : DisplayStyle.Flex;
            }

            isFixedField.RegisterValueChangeCallback(evt => UpdateVisibility());
            UpdateVisibility();

            return root;
        }
    }
}
#endif
*/
