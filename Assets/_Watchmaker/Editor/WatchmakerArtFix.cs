using UnityEditor;
using UnityEngine;

public static class WatchmakerArtFix
{
    [MenuItem("Watchmaker/Art/4 Fix Prefab Mesh Refs")]
    public static void Fix()
    {
        string[] names = { "P1_Workbench", "P3_Tray", "P4_Cushion", "P5_Screwdriver", "P8_LoupeLever" };
        foreach (var n in names)
        {
            string pp = "Assets/_Watchmaker/Art/Prefabs/" + n + ".prefab";
            string mp = "Assets/_Watchmaker/Art/Models/Optimized/" + n + "_opt.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
            if (mesh == null) { Debug.LogWarning("[Fix] no mesh " + mp); continue; }
            var root = PrefabUtility.LoadPrefabContents(pp);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) mf.sharedMesh = mesh;
            PrefabUtility.SaveAsPrefabAsset(root, pp);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[Fix] " + n + " mesh set: " + mesh.triangles.Length / 3 + " tris");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
