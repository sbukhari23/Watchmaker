using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    /// <summary>Reports triangle count, real-world size, materials and importer scale of each prop FBX.</summary>
    public static class WatchmakerArtReport
    {
        static readonly string[] Models =
        {
            "Assets/_Watchmaker/Art/Models/Environment/P1_Workbench/P1_Workbench.fbx",
            "Assets/_Watchmaker/Art/Models/Environment/P3_Tray/P3_Tray.fbx",
            "Assets/_Watchmaker/Art/Models/Environment/P4_Cushion/P4_Cushion.fbx",
            "Assets/_Watchmaker/Art/Models/Tools/P5_Screwdriver/P5_Screwdriver.fbx",
            "Assets/_Watchmaker/Art/Models/Tools/P8_LoupeLever/P8_LoupeLever.fbx",
        };

        [MenuItem("Watchmaker/Art/1 Report Imported Models")]
        public static void Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Art report (models)");
            sb.AppendLine("Generated: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine();
            foreach (var path in Models)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { sb.AppendLine("- FAIL | not importable: " + path); continue; }

                int tris = 0, verts = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh;
                    if (m == null) continue;
                    verts += m.vertexCount;
                    for (int i = 0; i < m.subMeshCount; i++) tris += (int)(m.GetIndexCount(i) / 3);
                }

                Bounds b = default; bool first = true; int mats = 0; string shaders = "";
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                    foreach (var mat in r.sharedMaterials)
                    {
                        mats++;
                        if (mat != null && mat.shader != null) shaders += mat.shader.name + "; ";
                    }
                }

                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                string impInfo = imp == null ? "no importer" :
                    "globalScale=" + imp.globalScale + ", useFileScale=" + imp.useFileScale +
                    ", meshCompression=" + imp.meshCompression + ", readable=" + imp.isReadable;

                sb.AppendLine("## " + Path.GetFileName(path));
                sb.AppendLine("- tris: " + tris + " | verts: " + verts);
                sb.AppendLine("- size (m): " + b.size.x.ToString("F3") + " x " + b.size.y.ToString("F3") + " x " + b.size.z.ToString("F3"));
                sb.AppendLine("- materials: " + mats + " | shaders: " + shaders);
                sb.AppendLine("- importer: " + impInfo);
                sb.AppendLine();
            }

            string root = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(root, "_planning");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "art-report.md"), sb.ToString());
            Debug.Log("[Watchmaker] Art report written to _planning/art-report.md");
        }
    }
}
