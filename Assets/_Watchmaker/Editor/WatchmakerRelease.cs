// Editor-only: release APK build for Quest (IL2CPP, ARM64, not a development build). Copies the APK to the submission folder.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerRelease
    {
        const string SubmissionDir = @"E:\Unity\Unity Projects\Watchmaker_Submission";

        [MenuItem("Watchmaker/Build/3 Build Release APK")]
        public static void BuildRelease()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            Directory.CreateDirectory(Path.Combine(root, "Builds"));
            string apk = Path.Combine(root, "Builds", "Watchmaker.apk");

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.atelier.watchmaker");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 2;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            string text;
            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary s = report.summary;
                text = "result: " + s.result + "\nerrors: " + s.totalErrors + "\nwarnings: " + s.totalWarnings +
                       "\nduration: " + s.totalTime + "\nsize MB: " + (s.totalSize / 1048576f).ToString("F1") + "\noutput: " + s.outputPath + "\n";
                if (s.result == BuildResult.Succeeded && Directory.Exists(SubmissionDir))
                {
                    string dst = Path.Combine(SubmissionDir, "Watchmaker.apk");
                    File.Copy(apk, dst, true);
                    text += "copied to: " + dst + "\n";
                }
                var errors = report.steps.SelectMany(st => st.messages)
                    .Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Distinct().Take(15).ToArray();
                if (errors.Length > 0) text += "\nfirst errors:\n- " + string.Join("\n- ", errors) + "\n";
            }
            catch (Exception e) { text = "result: Exception\n" + e + "\n"; }

            File.WriteAllText(Path.Combine(SubmissionDir, "build-report.txt"), DateTime.Now + "\n" + text);
            Debug.Log("[WatchmakerRelease]\n" + text);
        }
    }
}
