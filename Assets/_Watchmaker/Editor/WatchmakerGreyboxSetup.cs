// Editor-only: builds the grey-box three-gear snap test (gate G1) in the sandbox scene.
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
using Watchmaker.Interaction;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerGreyboxSetup
    {
        const string ScenePath = "Assets/_Watchmaker/Scenes/Sandbox_Interaction.unity";
        const string MatDir = "Assets/_Watchmaker/Art/Materials";

        struct GearSpec
        {
            public string Id, Name; public float Diameter; public Vector3 SocketPos; public Vector3 SlotPos; public Color Color;
        }

        [MenuItem("Watchmaker/Setup/5 Build Grey-Box Gear Snap Test")]
        public static void Stage5()
        {
            var sb = new StringBuilder();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                foreach (string n in new[] { "GrabCube", "WorkspaceRoot" })
                {
                    var old = GameObject.Find(n);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old);
                }

                Material plateMat = GetMaterial("M_Grey_Plate", "Universal Render Pipeline/Lit", new Color(0.55f, 0.57f, 0.60f));
                Material trayMat = GetMaterial("M_Grey_Tray", "Universal Render Pipeline/Lit", new Color(0.30f, 0.22f, 0.16f));
                Material ghostMat = GetMaterial("M_Ghost", "Universal Render Pipeline/Unlit", new Color(0.23f, 0.82f, 0.44f));

                var workspace = new GameObject("WorkspaceRoot");
                workspace.transform.position = new Vector3(0f, 0.95f, 0.35f);

                var movement = new GameObject("Movement");
                movement.transform.SetParent(workspace.transform, false);

                MakeDisc("Plate", movement.transform, Vector3.zero, 0.09f, 0.002f, plateMat);

                var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tray.name = "Tray";
                UnityEngine.Object.DestroyImmediate(tray.GetComponent<Collider>());
                tray.transform.SetParent(workspace.transform, false);
                tray.transform.localPosition = new Vector3(-0.16f, -0.004f, 0f);
                tray.transform.localScale = new Vector3(0.15f, 0.008f, 0.07f);
                tray.GetComponent<Renderer>().sharedMaterial = trayMat;

                var specs = new[]
                {
                    new GearSpec { Id = "L1_great_wheel", Name = "Great wheel", Diameter = 0.036f, SocketPos = new Vector3(-0.022f, 0.003f, -0.008f), SlotPos = new Vector3(-0.205f, 0.004f, 0f), Color = new Color(0.79f, 0.64f, 0.36f) },
                    new GearSpec { Id = "L1_intermediate_wheel", Name = "Intermediate wheel", Diameter = 0.0288f, SocketPos = new Vector3(0.016f, 0.003f, -0.012f), SlotPos = new Vector3(-0.160f, 0.004f, 0f), Color = new Color(0.72f, 0.58f, 0.30f) },
                    new GearSpec { Id = "L1_escape_wheel", Name = "Escape wheel", Diameter = 0.020f, SocketPos = new Vector3(0.012f, 0.003f, 0.022f), SlotPos = new Vector3(-0.120f, 0.004f, 0f), Color = new Color(0.75f, 0.77f, 0.80f) }
                };

                var sockets = new SnapSocket[specs.Length];
                for (int i = 0; i < specs.Length; i++)
                {
                    GearSpec g = specs[i];
                    var socketGo = new GameObject("Socket_" + g.Id);
                    socketGo.transform.SetParent(movement.transform, false);
                    socketGo.transform.localPosition = g.SocketPos;
                    GameObject ghost = MakeDisc("Ghost", socketGo.transform, Vector3.zero, g.Diameter, 0.002f, ghostMat);
                    var socket = socketGo.AddComponent<SnapSocket>();
                    socket.Configure(g.Id, 0.006f, 0.0015f, i > 0 ? new[] { sockets[i - 1] } : null, ghost.GetComponent<Renderer>());
                    sockets[i] = socket;
                }

                var manager = movement.AddComponent<WatchAssemblyManager>();
                manager.Configure(movement.transform, sockets);

                var slots = new GameObject("TraySlots");
                slots.transform.SetParent(workspace.transform, false);
                var parts = new GameObject("Parts");
                parts.transform.SetParent(workspace.transform, false);

                int wired = 0;
                foreach (GearSpec g in specs)
                {
                    var slot = new GameObject("Slot_" + g.Id);
                    slot.transform.SetParent(slots.transform, false);
                    slot.transform.localPosition = g.SlotPos;

                    var part = new GameObject("Part_" + g.Id);
                    part.transform.SetParent(parts.transform, false);
                    part.transform.localPosition = g.SlotPos;
                    Material mat = GetMaterial("M_Grey_" + g.Id, "Universal Render Pipeline/Lit", g.Color);
                    MakeDisc("Visual", part.transform, Vector3.zero, g.Diameter, 0.002f, mat);

                    var col = part.AddComponent<SphereCollider>();
                    col.radius = g.Diameter * 0.5f;
                    var rb = part.AddComponent<Rigidbody>();
                    rb.isKinematic = true; rb.useGravity = false;

                    var watchPart = part.AddComponent<WatchPart>();
                    part.AddComponent<PinchReleaseStabilizer>();
                    var transformer = part.AddComponent<PrecisionGrabTransformer>();
                    var grabbable = part.AddComponent<Grabbable>();
                    var handGrab = part.AddComponent<HandGrabInteractable>();
                    var grab = part.AddComponent<GrabInteractable>();

                    var gso = new SerializedObject(grabbable);
                    bool ok = SetRef(gso, "_rigidbody", rb, sb) & SetRef(gso, "_targetTransform", part.transform, sb)
                              & SetRef(gso, "_oneGrabTransformer", transformer, sb);
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

                    watchPart.Configure(g.Id, g.Name, slot.transform, new MonoBehaviour[] { handGrab, grab });
                    EditorUtility.SetDirty(watchPart);
                    if (ok) wired++;
                }

                foreach (var s in sockets) EditorUtility.SetDirty(s);
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                Line(sb, wired == specs.Length, "Parts wired (Grabbable, HandGrab pinch, transformer)", wired + "/" + specs.Length);
                Line(sb, UnityEngine.Object.FindObjectsByType<SnapSocket>(FindObjectsSortMode.None).Length == 3, "3 sockets in scene");
                Line(sb, UnityEngine.Object.FindFirstObjectByType<WatchAssemblyManager>() != null, "WatchAssemblyManager in scene");
                Line(sb, GameObject.Find("OVRCameraRig") != null, "OVRCameraRig still present");
                Line(sb, true, "Scene saved", string.Join(", ", scene.GetRootGameObjects().Select(o => o.name)));
            }
            catch (Exception e) { sb.AppendLine("- FAIL | exception | " + e); }

            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_planning");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "setup-report.md"),
                "\n## Stage 5: grey-box gear snap test (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n" + sb + "\n");
            Debug.Log("[WatchmakerSetup] Stage 5\n" + sb);
        }

        static GameObject MakeDisc(string name, Transform parent, Vector3 localPos, float diameter, float thickness, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(diameter, thickness * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Material GetMaterial(string name, string shaderName, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find(shaderName));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void Line(StringBuilder sb, bool ok, string label, string detail = "")
        {
            sb.AppendLine("- " + (ok ? "PASS" : "FAIL") + " | " + label + (detail.Length > 0 ? " | " + detail : ""));
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
