// Editor-only project setup automation for Watchmaker.
// Each menu item applies one setup stage and appends a verification report to _planning/setup-report.md.
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerSetup
    {
        const string Root = "Assets/_Watchmaker";
        const string ScenePath = Root + "/Scenes/Sandbox_Interaction.unity";
        const string RpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
        const string RendererPath = "Assets/Settings/Mobile_Renderer.asset";

        static readonly string[] Folders =
        {
            "Art/Models/Environment", "Art/Models/Movement_L1", "Art/Models/Movement_L2", "Art/Models/Tools", "Art/Models/Props",
            "Art/Materials", "Art/Textures", "Art/Shaders", "Art/VFX",
            "Audio/SFX", "Audio/Ambience", "Audio/Music",
            "Animations", "Prefabs/Core", "Prefabs/Parts", "Prefabs/Sockets", "Prefabs/Tools", "Prefabs/Environment", "Prefabs/UI",
            "Scenes",
            "Scripts/Core", "Scripts/Interaction", "Scripts/Workspace", "Scripts/Assembly", "Scripts/Mechanics",
            "Scripts/Feedback", "Scripts/UI", "Scripts/Tutorial", "Scripts/Data", "Scripts/Debug",
            "ScriptableObjects/Levels", "ScriptableObjects/Parts", "ScriptableObjects/Feedback", "ScriptableObjects/Tutorial",
            "Settings"
        };

        static string ReportPath
        {
            get
            {
                string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_planning");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "setup-report.md");
            }
        }

        static void Report(string title, StringBuilder body)
        {
            File.AppendAllText(ReportPath, "\n## " + title + " (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n" + body + "\n");
            Debug.Log("[WatchmakerSetup] " + title + "\n" + body);
        }

        static void Check(StringBuilder sb, string label, bool ok, string detail = "")
        {
            sb.AppendLine("- " + (ok ? "PASS" : "FAIL") + " | " + label + (detail.Length > 0 ? " | " + detail : ""));
        }

        [MenuItem("Watchmaker/Setup/1 Clean Template And Create Folders")]
        public static void Stage1()
        {
            var sb = new StringBuilder();
            try
            {
                EnsureFolder(Root);
                foreach (string f in Folders) EnsureFolder(Root + "/" + f);

                if (AssetDatabase.IsValidFolder("Assets/TutorialInfo")) AssetDatabase.DeleteAsset("Assets/TutorialInfo");
                if (File.Exists("Assets/Readme.asset")) AssetDatabase.DeleteAsset("Assets/Readme.asset");

                if (File.Exists("Assets/Scenes/SampleScene.unity") && !File.Exists(ScenePath))
                {
                    EditorSceneManager.SaveOpenScenes();
                    string err = AssetDatabase.MoveAsset("Assets/Scenes/SampleScene.unity", ScenePath);
                    if (!string.IsNullOrEmpty(err)) sb.AppendLine("- NOTE | MoveAsset: " + err);
                }
                if (AssetDatabase.IsValidFolder("Assets/Scenes") &&
                    Directory.GetFileSystemEntries("Assets/Scenes").Length == 0)
                    AssetDatabase.DeleteAsset("Assets/Scenes");

                if (File.Exists(ScenePath))
                    EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception e) { sb.AppendLine("- FAIL | exception | " + e.Message); }

            Check(sb, "TutorialInfo removed", !AssetDatabase.IsValidFolder("Assets/TutorialInfo"));
            Check(sb, "Readme.asset removed", !File.Exists("Assets/Readme.asset"));
            Check(sb, "Sandbox_Interaction scene exists", File.Exists(ScenePath));
            Check(sb, "Old Assets/Scenes removed", !AssetDatabase.IsValidFolder("Assets/Scenes"));
            int made = Folders.Count(f => AssetDatabase.IsValidFolder(Root + "/" + f));
            Check(sb, "Folder tree", made == Folders.Length, made + "/" + Folders.Length);
            Check(sb, "Build scene list", EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path == ScenePath,
                string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path)));
            Check(sb, "Assets/Settings kept", AssetDatabase.IsValidFolder("Assets/Settings"));
            Report("Stage 1: clean template and folders", sb);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("Watchmaker/Setup/2 Apply Android And URP Settings")]
        public static void Stage2()
        {
            var sb = new StringBuilder();
            bool androidSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
            Check(sb, "Android Build Support module installed", androidSupported);
            if (!androidSupported)
            {
                sb.AppendLine("- STOP | Install Android Build Support (OpenJDK, SDK & NDK) for 6000.3.28f1 in Unity Hub, then rerun.");
                Report("Stage 2: Android and URP settings", sb);
                return;
            }
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

                var android = NamedBuildTarget.Android;
                PlayerSettings.companyName = "Atelier";
                PlayerSettings.productName = "Watchmaker";
                PlayerSettings.bundleVersion = "0.1.0";
                PlayerSettings.Android.bundleVersionCode = 1;
                PlayerSettings.SetApplicationIdentifier(android, "com.atelier.watchmaker");
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
                PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

                EditorSettings.serializationMode = SerializationMode.ForceText;
                Time.fixedDeltaTime = 1f / 72f;

                var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
                if (rp != null)
                {
                    rp.msaaSampleCount = 4;
                    rp.supportsHDR = false;
                    rp.renderScale = 1f;
                    rp.supportsCameraDepthTexture = false;
                    rp.supportsCameraOpaqueTexture = false;
                    rp.useSRPBatcher = true;
                    var so = new SerializedObject(rp);
                    SetBool(so, "m_MainLightShadowsSupported", false, sb);
                    SetInt(so, "m_AdditionalLightsRenderingMode", 0, sb);
                    SetBool(so, "m_AdditionalLightShadowsSupported", false, sb);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(rp);

                    GraphicsSettings.defaultRenderPipeline = rp;
                    int current = QualitySettings.GetQualityLevel();
                    for (int i = 0; i < QualitySettings.names.Length; i++)
                    {
                        QualitySettings.SetQualityLevel(i, false);
                        QualitySettings.renderPipeline = rp;
                    }
                    QualitySettings.SetQualityLevel(current, false);
                }

                var rd = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
                if (rd != null)
                {
                    var so = new SerializedObject(rd);
                    var pp = so.FindProperty("postProcessData");
                    if (pp != null) { pp.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(rd); }
                    else sb.AppendLine("- NOTE | postProcessData property not found on renderer");
                }

                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { sb.AppendLine("- FAIL | exception | " + e.Message); }

            VerifyStage2(sb);
            Report("Stage 2: Android and URP settings", sb);
        }

        static void VerifyStage2(StringBuilder sb)
        {
            var android = NamedBuildTarget.Android;
            Check(sb, "Active build target Android", EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                EditorUserBuildSettings.activeBuildTarget.ToString());
            Check(sb, "Package name", PlayerSettings.GetApplicationIdentifier(android) == "com.atelier.watchmaker",
                PlayerSettings.GetApplicationIdentifier(android));
            Check(sb, "Company/Product", PlayerSettings.companyName == "Atelier" && PlayerSettings.productName == "Watchmaker");
            Check(sb, "Version 0.1.0 / code 1", PlayerSettings.bundleVersion == "0.1.0" && PlayerSettings.Android.bundleVersionCode == 1);
            Check(sb, "Linear colour space", PlayerSettings.colorSpace == ColorSpace.Linear);
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            Check(sb, "Vulkan only", !PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) && apis.Length == 1 && apis[0] == GraphicsDeviceType.Vulkan,
                string.Join(",", apis.Select(a => a.ToString())));
            Check(sb, "IL2CPP", PlayerSettings.GetScriptingBackend(android) == ScriptingImplementation.IL2CPP);
            Check(sb, "ARM64 only", PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64,
                PlayerSettings.Android.targetArchitectures.ToString());
            Check(sb, "Min SDK (info)", true, PlayerSettings.Android.minSdkVersion.ToString());
            Check(sb, "Force Text serialization", EditorSettings.serializationMode == SerializationMode.ForceText);
            Check(sb, "Fixed timestep 1/72", Mathf.Abs(Time.fixedDeltaTime - 1f / 72f) < 0.0001f, Time.fixedDeltaTime.ToString("F6"));

            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
            Check(sb, "Mobile_RPAsset found", rp != null);
            if (rp != null)
            {
                Check(sb, "MSAA 4x", rp.msaaSampleCount == 4, rp.msaaSampleCount.ToString());
                Check(sb, "HDR off", !rp.supportsHDR);
                Check(sb, "Render scale 1", Mathf.Approximately(rp.renderScale, 1f));
                Check(sb, "Depth/Opaque textures off", !rp.supportsCameraDepthTexture && !rp.supportsCameraOpaqueTexture);
                Check(sb, "Main light shadows off", !rp.supportsMainLightShadows);
                Check(sb, "Additional lights disabled", rp.additionalLightsRenderingMode == LightRenderingMode.Disabled,
                    rp.additionalLightsRenderingMode.ToString());
                Check(sb, "SRP Batcher on", rp.useSRPBatcher);
                Check(sb, "Default render pipeline = Mobile_RPAsset", GraphicsSettings.defaultRenderPipeline == rp);
                bool allQ = true;
                int current = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    if (QualitySettings.renderPipeline != rp) allQ = false;
                }
                QualitySettings.SetQualityLevel(current, false);
                Check(sb, "All quality levels use Mobile_RPAsset", allQ, string.Join(",", QualitySettings.names));
            }
        }

        static void SetBool(SerializedObject so, string name, bool v, StringBuilder sb)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = v; else sb.AppendLine("- NOTE | property not found: " + name);
        }

        static void SetInt(SerializedObject so, string name, int v, StringBuilder sb)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = v; else sb.AppendLine("- NOTE | property not found: " + name);
        }

        static AddAndRemoveRequest _addRequest;
        static ListRequest _listRequest;

        [MenuItem("Watchmaker/Setup/3 Install OpenXR And Meta XR SDK")]
        public static void Stage3()
        {
            if (_addRequest != null && !_addRequest.IsCompleted) return;
            Debug.Log("[WatchmakerSetup] Installing com.unity.xr.management, com.unity.xr.openxr, com.meta.xr.sdk.all ...");
            _addRequest = Client.AddAndRemove(new[] { "com.unity.xr.management", "com.unity.xr.openxr", "com.meta.xr.sdk.all" }, null);
            EditorApplication.update += PollAdd;
        }

        static void PollAdd()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;
            EditorApplication.update -= PollAdd;
            var sb = new StringBuilder();
            if (_addRequest.Status == StatusCode.Success)
            {
                Check(sb, "Package install request", true);
                foreach (var p in _addRequest.Result.Where(p => p.name.StartsWith("com.meta.") || p.name.StartsWith("com.unity.xr.")))
                    sb.AppendLine("- INFO | " + p.name + " @ " + p.version);
            }
            else
            {
                Check(sb, "Package install request", false, _addRequest.Error != null ? _addRequest.Error.message : "unknown error");
            }
            _addRequest = null;
            Report("Stage 3: OpenXR and Meta XR SDK install", sb);
        }

        [MenuItem("Watchmaker/Setup/9 Report Installed XR Packages")]
        public static void ReportPackages()
        {
            _listRequest = Client.List(true, false);
            EditorApplication.update += PollList;
        }

        static void PollList()
        {
            if (_listRequest == null || !_listRequest.IsCompleted) return;
            EditorApplication.update -= PollList;
            var sb = new StringBuilder();
            if (_listRequest.Status == StatusCode.Success)
                foreach (var p in _listRequest.Result.Where(p => p.name.StartsWith("com.meta.") || p.name.StartsWith("com.unity.xr.") || p.name.Contains("ai.assistant")))
                    sb.AppendLine("- INFO | " + p.name + " @ " + p.version);
            else sb.AppendLine("- FAIL | " + (_listRequest.Error != null ? _listRequest.Error.message : "unknown"));
            _listRequest = null;
            Report("Installed XR packages", sb);
        }
    }
}
