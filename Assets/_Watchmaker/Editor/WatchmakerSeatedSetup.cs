// Editor-only: configures the sandbox for a seated experience (eye-level tracking, seated-height table in easy reach), then re-places props.
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerSeatedSetup
    {
        // Seated: eyes ~1.2 m above floor, table top 0.25 m below the eyes and 0.55 m in front (~24 deg gaze down).
        const float EyeHeight = 1.2f;
        static readonly Vector3 WorkspacePos = new Vector3(0f, 1.0f, 0.42f);

        [MenuItem("Watchmaker/Setup/5 Seated Layout")]
        public static void Apply()
        {
            var ws = GameObject.Find("WorkspaceRoot");
            var rig = GameObject.Find("OVRCameraRig");
            if (ws == null || rig == null) { Debug.LogError("[Watchmaker] WorkspaceRoot or OVRCameraRig missing"); return; }

            ws.transform.position = WorkspacePos;
            ws.transform.localScale = Vector3.one * 1.35f;
            rig.transform.SetPositionAndRotation(new Vector3(0f, EyeHeight, 0f), Quaternion.identity);

            var mgrType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType("OVRManager", false); } catch { return null; } })
                .FirstOrDefault(t => t != null);
            if (mgrType != null)
            {
                var mgr = UnityEngine.Object.FindFirstObjectByType(mgrType, FindObjectsInactive.Include);
                if (mgr != null)
                {
                    var so = new SerializedObject(mgr);
                    var p = so.FindProperty("_trackingOriginType");
                    if (p != null)
                    {
                        int i = Array.IndexOf(p.enumNames, "EyeLevel");
                        if (i >= 0) { p.enumValueIndex = i; so.ApplyModifiedPropertiesWithoutUndo(); Debug.Log("[Watchmaker] Tracking origin = EyeLevel"); }
                        else Debug.LogWarning("[Watchmaker] EyeLevel not in: " + string.Join(",", p.enumNames));
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(ws.scene);
            WatchmakerArtPlace.Place(); // re-fits bench, tray, cushion, props to the new height and saves the scene
            Debug.Log("[Watchmaker] Seated layout applied.");
        }
    }
}
