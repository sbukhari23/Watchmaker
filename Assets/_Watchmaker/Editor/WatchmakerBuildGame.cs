// Editor-only: adds bridge, screws, crown, balance, loupe lever rig, game flow, audio and UI to the sandbox scene (idempotent).
using System;
using System.IO;
using System.Linq;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Watchmaker.Assembly;
using Watchmaker.Core;
using Watchmaker.Interaction;
using Watchmaker.Mechanics;
using Watchmaker.UI;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerBuildGame
    {
        const string MatDir = "Assets/_Watchmaker/Art/Materials";

        [MenuItem("Watchmaker/Setup/6 Build Game Systems")]
        public static void Build()
        {
            var sb = new StringBuilder();
            try
            {
                var ws = GameObject.Find("WorkspaceRoot");
                if (ws == null) { Debug.LogError("[Watchmaker] WorkspaceRoot missing"); return; }
                Transform movement = ws.transform.Find("Movement");
                Transform slots = ws.transform.Find("TraySlots");
                Transform parts = ws.transform.Find("Parts");

                foreach (string n in new[] { "Socket_L2_bridge", "Socket_L3_screw_a", "Socket_L3_screw_b", "Crown", "Balance" }) Destroy(movement, n);
                foreach (string n in new[] { "Slot_L2_bridge", "Slot_L3_screw_a", "Slot_L3_screw_b" }) Destroy(slots, n);
                foreach (string n in new[] { "Part_L2_bridge", "Part_L3_screw_a", "Part_L3_screw_b" }) Destroy(parts, n);
                foreach (string n in new[] { "GameSystems", "InstructionPanel", "RestartButton", "LoupePivot" })
                {
                    var old = GameObject.Find(n);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old);
                }

                Material ghostMat = Mat("M_Ghost", "Universal Render Pipeline/Unlit", new Color(0.23f, 0.82f, 0.44f));
                Material bridgeMat = Mat("M_Grey_L2_bridge", "Universal Render Pipeline/Lit", new Color(0.62f, 0.64f, 0.68f));
                Material screwMat = Mat("M_Grey_L3_screw", "Universal Render Pipeline/Lit", new Color(0.35f, 0.36f, 0.40f));
                Material brassMat = Mat("M_Brass", "Universal Render Pipeline/Lit", new Color(0.79f, 0.64f, 0.36f));
                Material steelMat = Mat("M_Steel", "Universal Render Pipeline/Lit", new Color(0.75f, 0.77f, 0.80f));

                SnapSocket escape = movement.Find("Socket_L1_escape_wheel").GetComponent<SnapSocket>();

                // ---- sockets (movement local space, real metres)
                SnapSocket bridgeSocket = MakeSocket("L2_bridge", movement, new Vector3(0f, 0.008f, 0.004f), PrimitiveType.Cube, new Vector3(0.07f, 0.002f, 0.016f), ghostMat, 0.008f, 0.002f, new[] { escape });
                SnapSocket screwA = MakeSocket("L3_screw_a", movement, new Vector3(-0.03f, 0.0105f, 0.004f), PrimitiveType.Cylinder, new Vector3(0.007f, 0.002f, 0.007f), ghostMat, 0.006f, 0.0015f, new[] { bridgeSocket });
                SnapSocket screwB = MakeSocket("L3_screw_b", movement, new Vector3(0.03f, 0.0105f, 0.004f), PrimitiveType.Cylinder, new Vector3(0.007f, 0.002f, 0.007f), ghostMat, 0.006f, 0.0015f, new[] { bridgeSocket });

                // ---- parts (greybox visuals; wired like the wheels)
                int wired = 0;
                wired += MakePart("L2_bridge", "Bridge", slots, parts, new Vector3(-0.16f, 0.004f, 0.040f), PrimitiveType.Cube, new Vector3(0.07f, 0.002f, 0.016f), bridgeMat, 0.03f, sb);
                wired += MakePart("L3_screw_a", "Screw A", slots, parts, new Vector3(-0.215f, 0.004f, -0.040f), PrimitiveType.Cylinder, new Vector3(0.007f, 0.002f, 0.007f), screwMat, 0.014f, sb);
                wired += MakePart("L3_screw_b", "Screw B", slots, parts, new Vector3(-0.195f, 0.004f, -0.040f), PrimitiveType.Cylinder, new Vector3(0.007f, 0.002f, 0.007f), screwMat, 0.014f, sb);
                foreach (string id in new[] { "L3_screw_a", "L3_screw_b" })
                    if (parts.Find("Part_" + id).GetComponent<ScrewTarget>() == null) parts.Find("Part_" + id).gameObject.AddComponent<ScrewTarget>();

                // ---- re-register all sockets with the assembly manager
                var manager = movement.GetComponent<WatchAssemblyManager>();
                SnapSocket[] all = movement.GetComponentsInChildren<SnapSocket>(true);
                manager.Configure(movement, all);
                EditorUtility.SetDirty(manager);

                // ---- crown + balance on the movement
                GameObject crown = Disc("Crown", movement, new Vector3(0.062f, 0.004f, 0f), 0.014f, 0.008f, brassMat);
                crown.AddComponent<CrownWinder>();
                GameObject balance = Disc("Balance", movement, new Vector3(0.026f, 0.0125f, -0.022f), 0.018f, 0.0015f, steelMat);
                var spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spoke.name = "Spoke";
                UnityEngine.Object.DestroyImmediate(spoke.GetComponent<Collider>());
                spoke.transform.SetParent(balance.transform, false);
                spoke.transform.localPosition = new Vector3(0f, 1f, 0f);
                spoke.transform.localScale = new Vector3(1f, 0.6f, 0.14f);
                spoke.GetComponent<Renderer>().sharedMaterial = brassMat;
                if (movement.GetComponent<MovementDriver>() == null) movement.gameObject.AddComponent<MovementDriver>();

                // ---- loupe lever: pivot at the base end, poke anywhere along it
                GameObject lever = GameObject.Find("Art_LoupeLever");
                if (lever != null)
                {
                    Bounds b = Bnd(lever);
                    bool alongX = b.size.x >= b.size.z;
                    Vector3 basePt = b.center + (alongX ? Vector3.left * b.extents.x : Vector3.back * b.extents.z);
                    var pivot = new GameObject("LoupePivot");
                    pivot.transform.SetParent(GameObject.Find("Environment").transform, true);
                    pivot.transform.position = new Vector3(basePt.x, b.min.y, basePt.z);
                    lever.transform.SetParent(pivot.transform, true);
                    var handle = new GameObject("Handle");
                    handle.transform.SetParent(pivot.transform, true);
                    handle.transform.position = b.center;
                    var ll = pivot.AddComponent<LoupeLever>();
                    ll.Configure(handle.transform, Mathf.Max(b.extents.x, b.extents.z) + 0.03f);
                    EditorUtility.SetDirty(ll);
                    sb.AppendLine("- loupe lever pivot at " + pivot.transform.position.ToString("F3") + ", poke radius " + (Mathf.Max(b.extents.x, b.extents.z) + 0.03f).ToString("F3"));
                }
                else sb.AppendLine("- NOTE | Art_LoupeLever not found (run menu Art/3 first)");

                // ---- systems
                var systems = new GameObject("GameSystems");
                systems.AddComponent<GameFlow>();
                systems.AddComponent<AudioManager>();
                systems.AddComponent<LoupeZoom>();
                systems.AddComponent<AutoPlayTest>();

                Vector3 wsPos = ws.transform.position;
                var panel = new GameObject("InstructionPanel");
                panel.transform.position = new Vector3(wsPos.x, wsPos.y + 0.30f, wsPos.z + 0.30f);
                panel.AddComponent<InstructionPanel>();
                panel.AddComponent<FaceCamera>();

                var button = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                button.name = "RestartButton";
                UnityEngine.Object.DestroyImmediate(button.GetComponent<Collider>());
                button.transform.position = new Vector3(wsPos.x, wsPos.y + 0.006f, wsPos.z - 0.16f);
                button.transform.localScale = new Vector3(0.05f, 0.006f, 0.05f);
                button.GetComponent<Renderer>().sharedMaterial = brassMat;
                button.AddComponent<PokeButton>();

                EditorSceneManager.MarkSceneDirty(ws.scene);
                EditorSceneManager.SaveScene(ws.scene);
                AssetDatabase.SaveAssets();
                sb.AppendLine("- parts wired: " + wired + "/3; sockets registered: " + all.Length + "; scene saved");
            }
            catch (Exception e) { sb.AppendLine("- FAIL | exception | " + e); Debug.LogError(e); }

            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_planning");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "setup-report.md"), "\n## Game systems build (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n" + sb + "\n");
            Debug.Log("[WatchmakerBuildGame]\n" + sb);
        }

        [MenuItem("Watchmaker/Debug/Run AutoPlay (Play mode)")]
        public static void RunAutoPlay()
        {
            var ap = UnityEngine.Object.FindFirstObjectByType<AutoPlayTest>();
            if (ap == null || !Application.isPlaying) { Debug.LogWarning("[Watchmaker] Enter Play mode first."); return; }
            ap.Run();
        }

        static void Destroy(Transform parent, string name)
        {
            if (parent == null) return;
            var t = parent.Find(name);
            if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        static Bounds Bnd(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // cylinders are 2 units tall: halve Y so "size" means real thickness
            go.transform.localScale = type == PrimitiveType.Cylinder ? new Vector3(scale.x, scale.y * 0.5f, scale.z) : scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Disc(string name, Transform parent, Vector3 localPos, float diameter, float thickness, Material mat) =>
            Prim(PrimitiveType.Cylinder, name, parent, localPos, new Vector3(diameter, thickness, diameter), mat);

        static SnapSocket MakeSocket(string id, Transform movement, Vector3 pos, PrimitiveType prim, Vector3 size, Material ghostMat, float capture, float tolerance, SnapSocket[] prereq)
        {
            var go = new GameObject("Socket_" + id);
            go.transform.SetParent(movement, false);
            go.transform.localPosition = pos;
            GameObject ghost = Prim(prim, "Ghost", go.transform, Vector3.zero, size, ghostMat);
            var s = go.AddComponent<SnapSocket>();
            s.Configure(id, capture, tolerance, prereq, ghost.GetComponent<Renderer>());
            EditorUtility.SetDirty(s);
            return s;
        }

        static int MakePart(string id, string displayName, Transform slots, Transform parts, Vector3 slotPos, PrimitiveType prim, Vector3 size, Material mat, float colliderRadius, StringBuilder sb)
        {
            var slot = new GameObject("Slot_" + id);
            slot.transform.SetParent(slots, false);
            slot.transform.localPosition = slotPos;

            var part = new GameObject("Part_" + id);
            part.transform.SetParent(parts, false);
            part.transform.localPosition = slotPos;
            Prim(prim, "Visual", part.transform, Vector3.zero, size, mat);

            var col = part.AddComponent<SphereCollider>();
            col.radius = colliderRadius;
            var rb = part.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;

            var watchPart = part.AddComponent<WatchPart>();
            part.AddComponent<PinchReleaseStabilizer>();
            var transformer = part.AddComponent<PrecisionGrabTransformer>();
            var grabbable = part.AddComponent<Grabbable>();
            var handGrab = part.AddComponent<HandGrabInteractable>();
            var grab = part.AddComponent<GrabInteractable>();

            var gso = new SerializedObject(grabbable);
            bool ok = SetRef(gso, "_rigidbody", rb, sb) & SetRef(gso, "_targetTransform", part.transform, sb) & SetRef(gso, "_oneGrabTransformer", transformer, sb);
            SetInt(gso, "_maxGrabPoints", 1, sb);
            SetBool(gso, "_throwWhenUnselected", false, sb);
            gso.ApplyModifiedPropertiesWithoutUndo();

            var hso = new SerializedObject(handGrab);
            ok &= SetRef(hso, "_rigidbody", rb, sb) & SetRef(hso, "_pointableElement", grabbable, sb);
            SetInt(hso, "_supportedGrabTypes", 1, sb);
            hso.ApplyModifiedPropertiesWithoutUndo();

            var cso = new SerializedObject(grab);
            ok &= SetRef(cso, "_rigidbody", rb, sb) & SetRef(cso, "_pointableElement", grabbable, sb);
            cso.ApplyModifiedPropertiesWithoutUndo();

            watchPart.Configure(id, displayName, slot.transform, new MonoBehaviour[] { handGrab, grab });
            EditorUtility.SetDirty(watchPart);
            return ok ? 1 : 0;
        }

        static Material Mat(string name, string shaderName, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find(shaderName));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static bool SetRef(SerializedObject so, string prop, UnityEngine.Object value, StringBuilder sb)
        {
            var p = so.FindProperty(prop);
            if (p == null) { sb.AppendLine("- NOTE | property not found: " + so.targetObject.GetType().Name + "." + prop); return false; }
            p.objectReferenceValue = value;
            return true;
        }

        static void SetInt(SerializedObject so, string prop, int value, StringBuilder sb)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.intValue = value; else sb.AppendLine("- NOTE | property not found: " + prop);
        }

        static void SetBool(SerializedObject so, string prop, bool value, StringBuilder sb)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = value; else sb.AppendLine("- NOTE | property not found: " + prop);
        }
    }
}
