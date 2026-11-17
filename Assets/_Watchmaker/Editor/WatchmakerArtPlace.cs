using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    /// <summary>Places the optimized art props into Sandbox_Interaction around WorkspaceRoot.</summary>
    public static class WatchmakerArtPlace
    {
        const string Prefabs = "Assets/_Watchmaker/Art/Prefabs/";

        [MenuItem("Watchmaker/Art/3 Place Props In Sandbox")]
        public static void Place()
        {
            var sb = new StringBuilder("\n# Art placement " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            var ws = GameObject.Find("WorkspaceRoot");
            if (ws == null) { Debug.LogError("[Watchmaker] WorkspaceRoot not found"); return; }
            var movement = ws.transform.Find("Movement");
            var tray = ws.transform.Find("Tray");

            var envGo = GameObject.Find("Environment");
            if (envGo == null) envGo = new GameObject("Environment");

            // Clean previous art placement (idempotent)
            RemoveByName(envGo.transform, "Art_");
            RemoveByName(ws.transform, "Art_");

            Vector3 wsPos = ws.transform.position;

            // ---- Workbench: top surface at WorkspaceRoot height, front edge ~15 cm in front of the player zone
            var bench = Spawn("P1_Workbench", "Art_Workbench", envGo.transform);
            if (bench != null)
            {
                var b = WB(bench);
                float topY = TopPlaneY(bench, b);
                Vector3 target = new Vector3(wsPos.x, wsPos.y, (wsPos.z - 0.20f) + b.extents.z);
                Vector3 delta = new Vector3(target.x - b.center.x, wsPos.y - topY, target.z - b.center.z);
                bench.transform.position += delta;
                sb.AppendLine("- workbench: bounds size " + b.size.ToString("F3") + ", detected top plane y=" + topY.ToString("F3") + ", moved by " + delta.ToString("F3"));
            }

            // ---- Tray: replace the grey-box visual, keep its centre
            var newTray = Spawn("P3_Tray", "Art_Tray", ws.transform);
            if (newTray != null && tray != null)
            {
                var oldRends = tray.GetComponentsInChildren<Renderer>(true);
                if (oldRends.Length > 0)
                {
                    Bounds ob = oldRends[0].bounds;
                    foreach (var r in oldRends) ob.Encapsulate(r.bounds);
                    foreach (var r in oldRends) r.enabled = false;
                    var nb = WB(newTray);
                    Vector3 delta = new Vector3(ob.center.x - nb.center.x, ob.min.y - nb.min.y, ob.center.z - nb.center.z);
                    newTray.transform.position += delta;
                    sb.AppendLine("- tray: old bounds " + ob.size.ToString("F3") + " centre " + ob.center.ToString("F3") + "; new bounds size " + nb.size.ToString("F3"));
                }
            }

            // ---- Cushion resting on the bench, movement raised onto its top
            var cushion = Spawn("P4_Cushion", "Art_Cushion", ws.transform);
            if (cushion != null && movement != null)
            {
                movement.localPosition = Vector3.zero; // reset so repeated runs don't stack
                var mr = movement.GetComponentsInChildren<Renderer>(true);
                if (mr.Length > 0)
                {
                    Bounds mb = mr[0].bounds;
                    foreach (var r in mr) mb.Encapsulate(r.bounds);
                    var cb = WB(cushion);
                    // cushion centred under the movement, bottom on the tabletop (WorkspaceRoot height)
                    Vector3 delta = new Vector3(mb.center.x - cb.center.x, wsPos.y - cb.min.y, mb.center.z - cb.center.z);
                    cushion.transform.position += delta;
                    // lift the movement (and its sockets) so it sits on top of the cushion
                    movement.position += Vector3.up * (cb.size.y + (wsPos.y - mb.min.y));
                    sb.AppendLine("- cushion: size " + cb.size.ToString("F3") + "; movement raised " + (cb.size.y + (wsPos.y - mb.min.y)).ToString("F3"));
                }
            }

            // ---- Screwdriver lying on the bench, to the right of the workspace
            var sd = Spawn("P5_Screwdriver", "Art_Screwdriver", envGo.transform);
            if (sd != null)
            {
                sd.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
                var b = WB(sd);
                Vector3 target = new Vector3(wsPos.x + 0.40f, wsPos.y, wsPos.z - 0.03f);
                sd.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
            }

            // ---- Loupe lever at the left side of the bench (visual only for now)
            var lever = Spawn("P8_LoupeLever", "Art_LoupeLever", envGo.transform);
            if (lever != null)
            {
                var b = WB(lever);
                Vector3 target = new Vector3(wsPos.x - 0.50f, wsPos.y, wsPos.z + 0.05f);
                lever.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
            }

            EditorSceneManager.MarkSceneDirty(ws.scene);
            EditorSceneManager.SaveScene(ws.scene);

            string root = Directory.GetParent(Application.dataPath).FullName;
            File.AppendAllText(Path.Combine(root, "_planning", "art-report.md"), sb.ToString());
            Debug.Log("[Watchmaker] Props placed and scene saved.");
        }

        static GameObject Spawn(string prefabName, string instanceName, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefabName + ".prefab");
            if (prefab == null) { Debug.LogError("[Watchmaker] prefab missing: " + prefabName); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = instanceName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        static Bounds WB(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // Finds the height of the main upward-facing tabletop plane (ignoring the raised back lip).
        static float TopPlaneY(GameObject go, Bounds b)
        {
            var mf = go.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning("[Watchmaker] TopPlaneY: no mesh on " + go.name + " (mf=" + (mf != null) + "), using bounds top");
                return b.max.y;
            }
            var verts = mf.sharedMesh.vertices;
            var l2w = mf.transform.localToWorldMatrix;
            const float bin = 0.005f;
            int bins = Mathf.CeilToInt(b.size.y / bin) + 1;
            var counts = new int[bins];
            foreach (var v in verts)
            {
                float y = l2w.MultiplyPoint3x4(v).y;
                int i = Mathf.Clamp((int)((y - b.min.y) / bin), 0, bins - 1);
                counts[i]++;
            }
            int lowest = (int)(bins * 0.6f);
            int max = 0;
            for (int i = lowest; i < bins; i++) if (counts[i] > max) max = counts[i];
            for (int i = bins - 1; i >= lowest; i--)
                if (counts[i] > max * 0.5f) return b.min.y + (i + 0.5f) * bin;
            return b.max.y;
        }

        static void RemoveByName(Transform parent, string prefix)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                if (c.name.StartsWith(prefix)) Object.DestroyImmediate(c.gameObject);
            }
        }
    }
}
