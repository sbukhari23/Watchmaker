// Editor-only: reports size/orientation/triangles of the incoming v2 models and key scene objects.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerInspectV2
    {
        [MenuItem("Watchmaker/Setup/8 Inspect Incoming Models")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Watchmaker/Art/Models/Incoming" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var rs = go.GetComponentsInChildren<MeshFilter>(true);
                int tris = 0; Bounds b = new Bounds(); bool first = true;
                foreach (var mf in rs)
                {
                    if (mf.sharedMesh == null) continue;
                    tris += mf.sharedMesh.triangles.Length / 3;
                    Bounds mb = mf.sharedMesh.bounds;
                    Vector3 c = mf.transform.TransformPoint(mb.center);
                    Vector3 e = Vector3.Scale(mb.extents, mf.transform.lossyScale);
                    var wb = new Bounds(c, e * 2f);
                    if (first) { b = wb; first = false; } else b.Encapsulate(wb);
                }
                sb.AppendLine(Path.GetFileName(Path.GetDirectoryName(path)) + " | " + Path.GetFileName(path));
                sb.AppendLine("  tris " + tris + " | size " + b.size.ToString("F4") + " | center " + b.center.ToString("F4") + " | root scale " + go.transform.localScale + " rot " + go.transform.localEulerAngles + " meshes " + rs.Length);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp != null) sb.AppendLine("  importer scale " + imp.globalScale + " useFileScale " + imp.useFileScale);
            }
            GameObject ws = GameObject.Find("WorkspaceRoot");
            if (ws != null)
            {
                sb.AppendLine("WorkspaceRoot pos " + ws.transform.position + " scale " + ws.transform.lossyScale);
                foreach (string n in new[] { "Movement", "Crown", "Balance", "Art_Tray", "Art_Cushion", "Parts" })
                {
                    Transform t = ws.transform.Find(n);
                    if (t == null) { Transform[] all = ws.GetComponentsInChildren<Transform>(true); foreach (var x in all) if (x.name == n) { t = x; break; } }
                    if (t == null) { sb.AppendLine(n + ": not found"); continue; }
                    Bounds bb = new Bounds(t.position, Vector3.zero);
                    foreach (var r in t.GetComponentsInChildren<Renderer>()) bb.Encapsulate(r.bounds);
                    sb.AppendLine(n + " world pos " + t.position.ToString("F3") + " rot " + t.eulerAngles + " bounds size " + bb.size.ToString("F3") + " center " + bb.center.ToString("F3"));
                }
                foreach (var s in ws.GetComponentsInChildren<Watchmaker.Assembly.SnapSocket>(true))
                    sb.AppendLine("socket " + s.name + " world " + s.transform.position.ToString("F3"));
            }
            foreach (string n in new[] { "Environment", "Art_Screwdriver", "Art_LoupeLever", "LoupePivot" })
            { var o = GameObject.Find(n); if (o != null) sb.AppendLine(n + " pos " + o.transform.position.ToString("F3")); }
            Camera cam = Camera.main;
            if (cam != null) sb.AppendLine("MainCamera pos " + cam.transform.position.ToString("F3") + " fwd " + cam.transform.forward.ToString("F2"));
            File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_planning", "v2-inspect.md"), sb.ToString());
            Debug.Log("[InspectV2] written");
        }
    }
}
