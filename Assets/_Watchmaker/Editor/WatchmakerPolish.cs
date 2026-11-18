// Editor-only: replaces primitive placeholder visuals with procedurally built gear / screw / bridge meshes and metal materials (idempotent).
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerPolish
    {
        const string GenDir = "Assets/_Watchmaker/Art/Generated";
        const string MatDir = "Assets/_Watchmaker/Art/Materials";

        sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<int> T = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var t = b; b = d; d = t; }
                int i = V.Count; V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var t = b; b = c; c = t; }
                int i = V.Count; V.Add(a); V.Add(b); V.Add(c); T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(V); m.SetTriangles(T, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }

        // ---- toothed ring gear, axis = Y, diameter = 2R
        static Mesh Gear(string name, float R, int teeth, float thickness, float holeR)
        {
            var pts = new List<Vector2>(); // (radius, angle)
            float step = Mathf.PI * 2f / teeth, root = R * 0.85f;
            for (int i = 0; i < teeth; i++)
            {
                float a = i * step;
                pts.Add(new Vector2(root, a + 0.05f * step));
                pts.Add(new Vector2(R, a + 0.22f * step));
                pts.Add(new Vector2(R, a + 0.50f * step));
                pts.Add(new Vector2(root, a + 0.67f * step));
            }
            var b = new Builder(); float h = thickness * 0.5f;
            Vector3 P(Vector2 p, float y, float r = -1f) { float rr = r < 0f ? p.x : r; return new Vector3(Mathf.Cos(p.y) * rr, y, Mathf.Sin(p.y) * rr); }
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p0 = pts[i], p1 = pts[(i + 1) % pts.Count];
                Vector3 mid = (P(p0, 0f) + P(p1, 0f)).normalized;
                b.Quad(P(p0, h), P(p1, h), P(p1, h, holeR), P(p0, h, holeR), Vector3.up);
                b.Quad(P(p0, -h), P(p1, -h), P(p1, -h, holeR), P(p0, -h, holeR), Vector3.down);
                b.Quad(P(p0, h), P(p1, h), P(p1, -h), P(p0, -h), mid);
                b.Quad(P(p0, h, holeR), P(p1, h, holeR), P(p1, -h, holeR), P(p0, -h, holeR), -mid);
            }
            return b.ToMesh(name);
        }

        // ---- lathe around Y; profile = (radius, y) bottom to top
        static Mesh Lathe(string name, Vector2[] profile, int seg)
        {
            var b = new Builder();
            for (int s = 0; s < seg; s++)
            {
                float a0 = s * Mathf.PI * 2f / seg, a1 = (s + 1) * Mathf.PI * 2f / seg;
                for (int i = 0; i < profile.Length - 1; i++)
                {
                    Vector2 p = profile[i], q = profile[i + 1];
                    Vector3 A = new Vector3(Mathf.Cos(a0) * p.x, p.y, Mathf.Sin(a0) * p.x);
                    Vector3 B = new Vector3(Mathf.Cos(a1) * p.x, p.y, Mathf.Sin(a1) * p.x);
                    Vector3 C = new Vector3(Mathf.Cos(a1) * q.x, q.y, Mathf.Sin(a1) * q.x);
                    Vector3 D = new Vector3(Mathf.Cos(a0) * q.x, q.y, Mathf.Sin(a0) * q.x);
                    Vector3 mid = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                    Vector3 outward = Mathf.Abs(q.y - p.y) > Mathf.Abs(q.x - p.x) ? mid : (q.y > p.y ? Vector3.up : Vector3.down);
                    if (p.x < 0.00001f && q.x < 0.00001f) continue;
                    if (p.x < 0.00001f) b.Tri(new Vector3(0, p.y, 0), C, D, outward);
                    else if (q.x < 0.00001f) b.Tri(A, B, new Vector3(0, q.y, 0), outward);
                    else b.Quad(A, B, C, D, outward);
                }
            }
            return b.ToMesh(name);
        }

        // ---- rounded-rectangle plate (convex), axis = Y
        static Mesh RoundedPlate(string name, float w, float d, float r, float thickness)
        {
            var o = new List<Vector3>(); float h = thickness * 0.5f;
            Vector2[] centers = { new Vector2(w / 2 - r, d / 2 - r), new Vector2(-(w / 2 - r), d / 2 - r), new Vector2(-(w / 2 - r), -(d / 2 - r)), new Vector2(w / 2 - r, -(d / 2 - r)) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= 8; i++)
                {
                    float a = (c * 90f + i * 90f / 8f) * Mathf.Deg2Rad;
                    o.Add(new Vector3(centers[c].x + Mathf.Cos(a) * r, 0f, centers[c].y + Mathf.Sin(a) * r));
                }
            var b = new Builder();
            for (int i = 0; i < o.Count; i++)
            {
                Vector3 p0 = o[i], p1 = o[(i + 1) % o.Count];
                Vector3 t0 = p0 + Vector3.up * h, t1 = p1 + Vector3.up * h, b0 = p0 - Vector3.up * h, b1 = p1 - Vector3.up * h;
                b.Tri(Vector3.up * h, t0, t1, Vector3.up);
                b.Tri(-Vector3.up * h, b0, b1, Vector3.down);
                b.Quad(t0, t1, b1, b0, (p0 + p1).normalized);
            }
            return b.ToMesh(name);
        }

        static Mesh Save(Mesh m)
        {
            Directory.CreateDirectory(GenDir);
            string path = GenDir + "/" + m.name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static Material Mat(string name, Color c, float metallic, float smooth)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", c); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetVisual(GameObject part, Mesh mesh, Material mat)
        {
            Transform v = part.transform.Find("Visual");
            if (v == null) return;
            v.localScale = Vector3.one;
            v.GetComponent<MeshFilter>().sharedMesh = mesh;
            v.GetComponent<MeshRenderer>().sharedMaterial = mat;
            for (int i = v.childCount - 1; i >= 0; i--) Object.DestroyImmediate(v.GetChild(i).gameObject);
        }

        static GameObject Child(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false); g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        [MenuItem("Watchmaker/Setup/7 Polish Parts And Movement")]
        public static void Polish()
        {
            Material brass = Mat("M_Gear_Brass", new Color(0.80f, 0.58f, 0.22f), 0.95f, 0.62f);
            Material brassDark = Mat("M_Gear_BrassDark", new Color(0.62f, 0.42f, 0.16f), 0.95f, 0.5f);
            Material steel = Mat("M_Bridge_Steel", new Color(0.72f, 0.74f, 0.78f), 1f, 0.72f);
            Material screwSteel = Mat("M_Screw_Steel", new Color(0.55f, 0.57f, 0.62f), 1f, 0.68f);
            Material slot = Mat("M_Slot_Dark", new Color(0.05f, 0.05f, 0.06f), 0f, 0.2f);
            Material ruby = Mat("M_Jewel_Ruby", new Color(0.65f, 0.05f, 0.1f), 0f, 0.9f);

            GameObject ws = GameObject.Find("WorkspaceRoot");
            if (ws == null) { Debug.LogError("[Polish] WorkspaceRoot missing"); return; }
            Transform parts = ws.transform.Find("Parts");

            void Wheel(string id, float diameter, int teeth, Material m)
            {
                Transform p = parts.Find("Part_" + id); if (p == null) return;
                SetVisual(p.gameObject, Save(Gear("Gear_" + id, diameter * 0.5f, teeth, 0.0022f, diameter * 0.16f)), m);
            }
            Wheel("L1_great_wheel", 0.036f, 28, brass);
            Wheel("L1_intermediate_wheel", 0.0288f, 22, brassDark);
            Wheel("L1_escape_wheel", 0.020f, 15, steel);

            Transform bridge = parts.Find("Part_L2_bridge");
            if (bridge != null)
            {
                SetVisual(bridge.gameObject, Save(RoundedPlate("Bridge_Plate", 0.07f, 0.016f, 0.006f, 0.0022f)), steel);
                Transform v = bridge.Find("Visual");
                Mesh hole = Save(Lathe("Bridge_Hole", new[] { new Vector2(0f, 0f), new Vector2(0.0032f, 0f), new Vector2(0.0032f, 0.0003f), new Vector2(0f, 0.0003f) }, 16));
                Mesh jewel = Save(Lathe("Bridge_Jewel", new[] { new Vector2(0f, 0f), new Vector2(0.0022f, 0f), new Vector2(0.0022f, 0.0006f), new Vector2(0.0012f, 0.0010f), new Vector2(0f, 0.0010f) }, 16));
                Child(v, "HoleA", hole, slot, new Vector3(-0.03f, 0.0011f, 0.0f));
                Child(v, "HoleB", hole, slot, new Vector3(0.03f, 0.0011f, 0.0f));
                Child(v, "Jewel", jewel, ruby, new Vector3(0f, 0.0011f, 0f));
            }

            Mesh screwMesh = Save(Lathe("Screw_Head", new[] {
                new Vector2(0f, 0f), new Vector2(0.0030f, 0f), new Vector2(0.0035f, 0.0006f), new Vector2(0.0035f, 0.0016f),
                new Vector2(0.0030f, 0.0022f), new Vector2(0f, 0.0022f) }, 20));
            Mesh slotMesh = Save(RoundedPlate("Screw_Slot", 0.0058f, 0.0008f, 0.0002f, 0.0004f));
            foreach (string id in new[] { "L3_screw_a", "L3_screw_b" })
            {
                Transform p = parts.Find("Part_" + id); if (p == null) continue;
                SetVisual(p.gameObject, screwMesh, screwSteel);
                Child(p.Find("Visual"), "Slot", slotMesh, slot, new Vector3(0f, 0.0021f, 0f));
            }

            // movement details
            Transform movement = ws.transform.Find("Movement");
            if (movement != null)
            {
                Transform crown = movement.Find("Crown");
                if (crown != null)
                {
                    var mf = crown.GetComponent<MeshFilter>();
                    mf.sharedMesh = Save(Lathe("Crown_Knurled", new[] {
                        new Vector2(0f, -0.004f), new Vector2(0.0058f, -0.004f), new Vector2(0.0070f, -0.0030f), new Vector2(0.0070f, 0.0030f),
                        new Vector2(0.0058f, 0.0040f), new Vector2(0f, 0.0040f) }, 24));
                    crown.GetComponent<MeshRenderer>().sharedMaterial = brass;
                    crown.localScale = Vector3.one;
                }
                Transform bal = movement.Find("Balance");
                if (bal != null)
                {
                    bal.GetComponent<MeshFilter>().sharedMesh = Save(Gear("Balance_Wheel", 0.009f, 12, 0.0015f, 0.0075f));
                    bal.GetComponent<MeshRenderer>().sharedMaterial = steel;
                    bal.localScale = Vector3.one;
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(ws.scene);
            EditorSceneManager.SaveScene(ws.scene);
            Debug.Log("[Polish] gears, bridge, screws, crown, balance rebuilt with finished geometry and metal materials");
        }
    }
}
