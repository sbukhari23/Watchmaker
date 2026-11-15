// Editor-only: builds the hands-only sandbox scene (gate G0). Uses reflection for Meta SDK types so it compiles on any SDK version.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerSandboxSetup
    {
        const string ScenePath = "Assets/_Watchmaker/Scenes/Sandbox_Interaction.unity";

        static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType(fullName, false); } catch { return null; } })
                .FirstOrDefault(t => t != null);
        }

        static void Line(StringBuilder sb, bool ok, string label, string detail = "")
        {
            sb.AppendLine("- " + (ok ? "PASS" : "FAIL") + " | " + label + (detail.Length > 0 ? " | " + detail : ""));
        }

        [MenuItem("Watchmaker/Setup/4 Build Hands-Only Sandbox Scene")]
        public static void Stage4()
        {
            var sb = new StringBuilder();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                ConfigureProject(sb);

                foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (cam.gameObject.name == "Main Camera") UnityEngine.Object.DestroyImmediate(cam.gameObject);

                var legacy = GameObject.Find("OVRCameraRigInteraction");
                if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);

                const string rigName = "OVRComprehensiveInteractionRig";
                GameObject rig = GameObject.Find(rigName);
                if (rig == null)
                {
                    string path = AssetDatabase.FindAssets(rigName + " t:Prefab")
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == rigName && !x.Contains("/Legacy/"));
                    if (path != null)
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        rig.transform.position = Vector3.zero;
                        sb.AppendLine("- INFO | rig prefab | " + path);
                    }
                }
                Line(sb, rig != null, "OVRComprehensiveInteractionRig in scene");

                var ovrManagerType = FindType("OVRManager");
                if (rig != null && ovrManagerType != null)
                {
                    var mgr = UnityEngine.Object.FindFirstObjectByType(ovrManagerType, FindObjectsInactive.Include);
                    if (mgr != null)
                    {
                        var so = new SerializedObject(mgr);
                        var origin = so.FindProperty("_trackingOriginType");
                        if (origin != null)
                        {
                            int floor = Array.IndexOf(origin.enumNames, "FloorLevel");
                            if (floor >= 0) origin.enumValueIndex = floor;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            Line(sb, floor >= 0, "Tracking origin FloorLevel", string.Join(",", origin.enumNames));
                        }
                        else sb.AppendLine("- NOTE | _trackingOriginType not found");
                    }
                    else Line(sb, false, "OVRManager on rig");
                }

                GameObject cube = GameObject.Find("GrabCube");
                if (cube == null)
                {
                    cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "GrabCube";
                }
                cube.transform.position = new Vector3(0f, 1.0f, 0.4f);
                cube.transform.localScale = Vector3.one * 0.05f;
                SetupGrab(cube, sb);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Line(sb, true, "Scene saved", ScenePath);
                sb.AppendLine("- INFO | root objects | " + string.Join(", ", scene.GetRootGameObjects().Select(g => g.name)));
            }
            catch (Exception e) { sb.AppendLine("- FAIL | exception | " + e); }

            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_planning");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "setup-report.md"),
                "\n## Stage 4: hands-only sandbox scene (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n" + sb + "\n");
            Debug.Log("[WatchmakerSetup] Stage 4\n" + sb);
        }

        static void ConfigureProject(StringBuilder sb)
        {
            var t = FindType("OVRProjectConfig");
            if (t == null) { Line(sb, false, "OVRProjectConfig type found"); return; }
            var cached = t.GetProperty("CachedProjectConfig", BindingFlags.Public | BindingFlags.Static);
            object cfg = cached != null ? cached.GetValue(null) : null;
            if (cfg == null) { Line(sb, false, "OVRProjectConfig instance"); return; }

            SetEnumField(t, cfg, "handTrackingSupport", "ControllersAndHands", sb);
            SetEnumField(t, cfg, "handTrackingFrequency", "HIGH", sb);

            var devField = t.GetField("targetDeviceTypes");
            if (devField != null)
            {
                var list = (IList)devField.GetValue(cfg);
                Type enumType = devField.FieldType.GetGenericArguments()[0];
                list.Clear();
                foreach (string n in new[] { "Quest3", "Quest3S" })
                    if (Enum.IsDefined(enumType, n)) list.Add(Enum.Parse(enumType, n));
                Line(sb, list.Count == 2, "Target devices", string.Join(",", list.Cast<object>()) + " (available: " + string.Join(",", Enum.GetNames(enumType)) + ")");
            }
            else sb.AppendLine("- NOTE | targetDeviceTypes field not found");

            var commit = t.GetMethod("CommitProjectConfig", BindingFlags.Public | BindingFlags.Static);
            if (commit != null) commit.Invoke(null, new[] { cfg });
            Line(sb, commit != null, "OVRProjectConfig committed");
        }

        static void SetEnumField(Type t, object cfg, string field, string value, StringBuilder sb)
        {
            var f = t.GetField(field);
            if (f == null) { sb.AppendLine("- NOTE | field not found: " + field); return; }
            bool ok = Enum.IsDefined(f.FieldType, value);
            if (ok) f.SetValue(cfg, Enum.Parse(f.FieldType, value));
            Line(sb, ok, field + " = " + value, "available: " + string.Join(",", Enum.GetNames(f.FieldType)));
        }

        static void SetupGrab(GameObject cube, StringBuilder sb)
        {
            var rb = cube.GetComponent<Rigidbody>();
            if (rb == null) rb = cube.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            Type grabbableType = FindType("Oculus.Interaction.Grabbable");
            Type handGrabType = FindType("Oculus.Interaction.HandGrab.HandGrabInteractable");
            Type grabType = FindType("Oculus.Interaction.GrabInteractable");
            Line(sb, grabbableType != null && handGrabType != null, "ISDK types found");
            if (grabbableType == null || handGrabType == null) return;

            var grabbable = cube.GetComponent(grabbableType);
            if (grabbable == null) grabbable = cube.AddComponent(grabbableType);
            var handGrab = cube.GetComponent(handGrabType);
            if (handGrab == null) handGrab = cube.AddComponent(handGrabType);

            var gso = new SerializedObject(grabbable);
            SetRef(gso, "_rigidbody", rb, sb, "Grabbable");
            SetRef(gso, "_targetTransform", cube.transform, sb, "Grabbable");
            gso.ApplyModifiedPropertiesWithoutUndo();

            var hso = new SerializedObject(handGrab);
            SetRef(hso, "_rigidbody", rb, sb, "HandGrabInteractable");
            SetRef(hso, "_pointableElement", grabbable, sb, "HandGrabInteractable");
            var types = hso.FindProperty("_supportedGrabTypes");
            if (types != null) { types.intValue = 1; sb.AppendLine("- INFO | grab types set to Pinch (flag 1) | options: " + string.Join(",", types.enumNames)); }
            else sb.AppendLine("- NOTE | _supportedGrabTypes not found");
            hso.ApplyModifiedPropertiesWithoutUndo();

            if (grabType != null)
            {
                var grab = cube.GetComponent(grabType);
                if (grab == null) grab = cube.AddComponent(grabType);
                var so = new SerializedObject(grab);
                SetRef(so, "_rigidbody", rb, sb, "GrabInteractable");
                SetRef(so, "_pointableElement", grabbable, sb, "GrabInteractable");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Line(sb, true, "GrabCube configured (kinematic, no gravity)");
        }

        static void SetRef(SerializedObject so, string prop, UnityEngine.Object value, StringBuilder sb, string owner)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else sb.AppendLine("- NOTE | " + owner + " property not found: " + prop);
        }
    }
}
