// Editor-only: writes the largest assets of the last build to the submission folder.
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerSizeReport
    {
        [MenuItem("Watchmaker/Build/4 Write Last Build Size Report")]
        public static void Write()
        {
            BuildReport r = BuildReport.GetLatestReport();
            if (r == null) { Debug.LogWarning("[SizeReport] no build report"); return; }
            var sb = new StringBuilder();
            sb.AppendLine("total MB: " + (r.summary.totalSize / 1048576f).ToString("F1"));
            var all = r.packedAssets.SelectMany(p => p.contents).OrderByDescending(c => c.packedSize).Take(40);
            foreach (var c in all) sb.AppendLine((c.packedSize / 1048576f).ToString("F1") + " MB  " + c.type.Name + "  " + c.sourceAssetPath);
            sb.AppendLine("--- by file ---");
            foreach (var f in r.GetFiles().OrderByDescending(f => (long)f.size).Take(15)) sb.AppendLine(((long)f.size / 1048576f).ToString("F1") + " MB  " + f.path);
            File.WriteAllText(@"E:\Unity\Unity Projects\Watchmaker_Submission\size-report.txt", sb.ToString());
            Debug.Log("[SizeReport] written");
        }
    }
}
