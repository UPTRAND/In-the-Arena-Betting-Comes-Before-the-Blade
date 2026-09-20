#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using InTheArena.Unit;
using UnityEditor;
using UnityEngine;

/// <summary>스킬 관리 참조를 코드 수정 없이 Inspector에서 선택하고 설정합니다.</summary>
[CustomPropertyDrawer(typeof(SkillBehaviorDefinition), true)]
[CustomPropertyDrawer(typeof(SkillTargetingDefinition), true)]
[CustomPropertyDrawer(typeof(SkillEffectDefinition), true)]
public sealed class SkillDefinitionDrawer : PropertyDrawer
{
    private sealed class Selection
    {
        public UnityEngine.Object[] Targets;
        public string PropertyPath;
        public Type DefinitionType;
    }

    /// <summary>현재 선택한 정의의 직렬화 필드 높이를 계산합니다.</summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded || property.managedReferenceValue == null)
        {
            return height;
        }

        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool hasChild = child.NextVisible(true);
        while (hasChild && !SerializedProperty.EqualContents(child, end))
        {
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
            hasChild = child.NextVisible(false);
        }

        return height;
    }

    /// <summary>타입 선택 버튼과 펼쳐진 설정 필드를 그립니다.</summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        Rect foldout = header;
        foldout.width = EditorGUIUtility.labelWidth;
        property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, label, true);

        Rect selector = header;
        selector.xMin += EditorGUIUtility.labelWidth;
        string typeName = "None";
        if (property.managedReferenceValue != null)
        {
            typeName = property.managedReferenceValue.GetType().Name;
        }

        if (EditorGUI.DropdownButton(selector, new GUIContent(typeName), FocusType.Keyboard))
        {
            ShowTypeMenu(property);
        }

        if (property.isExpanded && property.managedReferenceValue != null)
        {
            SerializedProperty child = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            bool hasChild = child.NextVisible(true);
            float y = header.yMax;
            EditorGUI.indentLevel++;

            while (hasChild && !SerializedProperty.EqualContents(child, end))
            {
                y += EditorGUIUtility.standardVerticalSpacing;
                float height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), child, true);
                y += height;
                hasChild = child.NextVisible(false);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    /// <summary>직렬화 가능한 구체 타입을 찾아 생성 메뉴에 추가합니다.</summary>
    private void ShowTypeMenu(SerializedProperty property)
    {
        Type baseType = fieldInfo.FieldType;
        if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(List<>))
        {
            baseType = baseType.GetGenericArguments()[0];
        }

        var menu = new GenericMenu();
        AddChoice(menu, property, null, "None");
        foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
        {
            if (type.IsAbstract || type.IsNested || type.ContainsGenericParameters ||
                !type.IsSerializable || type.GetConstructor(Type.EmptyTypes) == null)
            {
                continue;
            }

            AddChoice(menu, property, type, type.FullName.Replace('.', '/'));
        }

        menu.ShowAsContext();
    }

    /// <summary>현재 Inspector 대상을 보관하여 메뉴 선택 후에도 올바른 필드를 수정합니다.</summary>
    private static void AddChoice(GenericMenu menu, SerializedProperty property, Type type, string label)
    {
        var selection = new Selection
        {
            Targets = property.serializedObject.targetObjects,
            PropertyPath = property.propertyPath,
            DefinitionType = type
        };
        bool selected = property.managedReferenceValue == null && type == null;
        if (property.managedReferenceValue != null)
        {
            selected = property.managedReferenceValue.GetType() == type;
        }

        menu.AddItem(new GUIContent(label), selected, ApplySelection, selection);
    }

    /// <summary>각 에셋에 독립 정의를 생성하고 Undo 및 에셋 저장 상태를 갱신합니다.</summary>
    private static void ApplySelection(object userData)
    {
        var selection = (Selection)userData;
        Undo.RecordObjects(selection.Targets, "Select skill definition");

        foreach (UnityEngine.Object target in selection.Targets)
        {
            if (target == null)
            {
                continue;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(selection.PropertyPath);
            if (property == null)
            {
                continue;
            }

            object definition = null;
            if (selection.DefinitionType != null)
            {
                definition = Activator.CreateInstance(selection.DefinitionType);
            }

            property.managedReferenceValue = definition;
            property.isExpanded = true;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
}
#endif
