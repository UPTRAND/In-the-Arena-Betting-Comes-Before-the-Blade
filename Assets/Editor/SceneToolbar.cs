#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

[InitializeOnLoad]
public static class SceneToolbar
{
    private const string ToolbarPath = "In The Arena/Scene Toolbar";
    private const string StartElementPath = ToolbarPath + "/Start";
    private const string ScenesElementPath = ToolbarPath + "/Scenes";

    private static readonly MethodInfo ShowAllMethod = typeof(MainToolbar).GetMethod(
        "ShowAll",
        BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly PropertyInfo WindowExistsProperty = typeof(MainToolbar).GetProperty(
        "windowExists",
        BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo TryGetOverlayMethod = typeof(MainToolbar).GetMethod(
        "TryGetOverlay",
        BindingFlags.Static | BindingFlags.NonPublic);

    private static List<EditorBuildSettingsScene> s_EnabledScenes = new List<EditorBuildSettingsScene>();
    private static string s_StartText;
    private static bool s_ControlsEnabled;
    private static bool s_VisibilityApplied;
    private static bool s_Warned;

    static SceneToolbar()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorBuildSettings.sceneListChanged -= RefreshScenes;
        EditorBuildSettings.sceneListChanged += RefreshScenes;
        RefreshScenes();
    }

    [MainToolbarElement(
        StartElementPath,
        defaultDockPosition = MainToolbarDockPosition.Middle,
        defaultDockIndex = -1)]
    private static MainToolbarElement CreateStartButton()
    {
        return new MainToolbarButton(new MainToolbarContent(s_StartText), StartFirstScene)
        {
            enabled = s_ControlsEnabled
        };
    }

    [MainToolbarElement(
        ScenesElementPath,
        defaultDockPosition = MainToolbarDockPosition.Middle,
        defaultDockIndex = 1)]
    private static MainToolbarElement CreateSceneDropdown()
    {
        return new MainToolbarDropdown(new MainToolbarContent("Scenes"), ShowSceneMenu)
        {
            enabled = s_ControlsEnabled
        };
    }

    private static void Update()
    {
        bool enabled = s_EnabledScenes.Count > 0 &&
            !EditorApplication.isCompiling &&
            !EditorApplication.isPlayingOrWillChangePlaymode;

        if (enabled != s_ControlsEnabled)
        {
            s_ControlsEnabled = enabled;
            RefreshToolbar();
        }

        EnsureVisible();
    }

    private static void EnsureVisible()
    {
        if (s_VisibilityApplied)
            return;

        if (ShowAllMethod == null || WindowExistsProperty == null || TryGetOverlayMethod == null)
        {
            if (!s_Warned)
            {
                s_Warned = true;
                Debug.LogWarning(
                    "[SceneToolbar] Unity main toolbar visibility API was not found. " +
                    "Scene toolbar registration was skipped.");
            }
            return;
        }

        if (!(bool)WindowExistsProperty.GetValue(null))
            return;

        ShowAllMethod.Invoke(null, new object[] { ToolbarPath });
        s_VisibilityApplied = HasOverlay(StartElementPath) && HasOverlay(ScenesElementPath);
    }

    private static bool HasOverlay(string path)
    {
        object[] arguments = { path, null };
        return (bool)TryGetOverlayMethod.Invoke(null, arguments);
    }

    private static void RefreshScenes()
    {
        s_EnabledScenes = new List<EditorBuildSettingsScene>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                s_EnabledScenes.Add(scene);
        }

        s_StartText = s_EnabledScenes.Count == 0
            ? "Start None"
            : $"Start '{Path.GetFileNameWithoutExtension(s_EnabledScenes[0].path)}'";
        s_ControlsEnabled = s_EnabledScenes.Count > 0 &&
            !EditorApplication.isCompiling &&
            !EditorApplication.isPlayingOrWillChangePlaymode;
        RefreshToolbar();
    }

    private static void RefreshToolbar()
    {
        MainToolbar.Refresh(StartElementPath);
        MainToolbar.Refresh(ScenesElementPath);
    }

    private static void StartFirstScene()
    {
        RefreshScenes();
        if (!CanChangeScene() || s_EnabledScenes.Count == 0)
            return;

        OpenScene(s_EnabledScenes[0].path, true);
    }

    private static void ShowSceneMenu(Rect dropdownRect)
    {
        RefreshScenes();
        if (!CanChangeScene() || s_EnabledScenes.Count == 0)
            return;

        var menu = new GenericMenu();
        string currentPath = EditorSceneManager.GetActiveScene().path;
        foreach (EditorBuildSettingsScene scene in s_EnabledScenes)
        {
            string path = scene.path;
            menu.AddItem(
                new GUIContent(GetDisplayPath(path)),
                string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase),
                () => OpenScene(path, false));
        }
        menu.DropDown(dropdownRect);
    }

    private static bool CanChangeScene()
    {
        return !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void OpenScene(string path, bool enterPlayMode)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        try
        {
            EditorSceneManager.OpenScene(path);
            if (enterPlayMode)
                EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SceneToolbar] Failed to open scene '{path}': {exception.Message}");
        }
    }

    private static string GetDisplayPath(string path)
    {
        string displayPath = path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            ? path.Substring("Assets/".Length)
            : path;
        return displayPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
            ? displayPath.Substring(0, displayPath.Length - ".unity".Length)
            : displayPath;
    }
}
#endif
