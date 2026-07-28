using UnityEditor;

public class ForceCompile
{
    [MenuItem("Hermes/Force Compile")]
    public static void DoForceCompile()
    {
        UnityEditor.AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }
}
