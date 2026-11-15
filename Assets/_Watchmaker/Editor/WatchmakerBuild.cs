// Editor-only: one-click development builds. Result is written to _planning/build-report.md.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerBuild
    {
        [MenuItem("Watchmaker/Build/1 Build Development APK")]
        public static void BuildApk() => Build(false);

        [MenuItem("Watchmaker/Build/2 Build And Run On Headset")]
        public static void BuildAndRun() => Build(true);

        static void Build(bool run)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(root, "Builds");
            Directory.CreateDirectory(outDir);
            string apk = Path.Combine(outDir, "Watchmaker-dev.apk");

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.Development | (run ? BuildOptions.AutoRunPlayer : BuildOptions.None)
            };

            string text;
            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary s = report.summary;
                text = "result: " + s.result + "\nerrors: " + s.totalErrors + "\nwarnings: " + s.totalWarnings +
                       "\nduration: " + s.totalTime + "\nsize MB: " + (s.totalSize / 1048576f).ToString("F1") +
                       "\noutput: " + s.outputPath + "\nrun requested: " + run + "\n";
                var errors = report.steps.SelectMany(st => st.messages)
                    .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                    .Select(m => m.content).Distinct().Take(15).ToArray();
                if (errors.Length > 0) text += "\nfirst errors:\n- " + string.Join("\n- ", errors) + "\n";
            }
            catch (Exception e) { text = "result: Exception\n" + e + "\n"; }

            string dir = Path.Combine(root, "_planning");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "build-report.md"),
                "# Build report (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n\n```\n" + text + "```\n");
            Debug.Log("[WatchmakerBuild]\n" + text);
        }
    }
}
