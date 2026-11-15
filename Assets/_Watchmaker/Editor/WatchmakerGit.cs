// Editor-only: runs git from Unity. Stage → review report → commit and push using _planning/commit-msg.txt.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerGit
    {
        static readonly string[] Forbidden = { "CLAUDE.md", "_planning/", "Library/", "Builds/", "Temp/", "Logs/", "UserSettings/", ".keystore", ".apk" };

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        static string PlanningDir => Path.Combine(ProjectRoot, "_planning");

        static string Git(string args, out int code, int timeoutMs = 120000)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = ProjectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            var sb = new StringBuilder();
            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } code = -999; return sb + "\nTIMEOUT"; }
                p.WaitForExit();
                code = p.ExitCode;
            }
            return sb.ToString();
        }

        static void Write(string title, string body)
        {
            Directory.CreateDirectory(PlanningDir);
            File.WriteAllText(Path.Combine(PlanningDir, "git-report.md"),
                "# " + title + " (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n\n" + body);
            UnityEngine.Debug.Log("[WatchmakerGit] " + title);
        }

        static string[] StagedForbidden()
        {
            int c;
            string names = Git("diff --cached --name-only", out c);
            return names.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)
                .Where(l => Forbidden.Any(f => l == f || l.StartsWith(f) || l.EndsWith(f))).ToArray();
        }

        [MenuItem("Watchmaker/Git/1 Stage All And Report")]
        public static void StageAndReport()
        {
            AssetDatabase.SaveAssets();
            int c;
            var sb = new StringBuilder();
            sb.AppendLine("## branch\n```\n" + Git("rev-parse --abbrev-ref HEAD", out c) + "```");
            string add = Git("add -A", out c);
            sb.AppendLine("## git add -A (exit " + c + ")\n```\n" + add + "```");
            string stat = Git("diff --cached --stat", out c);
            var lines = stat.Split('\n');
            sb.AppendLine("## staged summary\n```\n" + lines.LastOrDefault(l => l.Trim().Length > 0) + "\n```");
            sb.AppendLine("## staged top-level paths\n```");
            string names = Git("diff --cached --name-status", out c);
            foreach (var g in names.Split('\n').Where(l => l.Trim().Length > 0)
                         .Select(l => { var parts = l.Split('\t'); var path = parts[parts.Length - 1]; var seg = path.Split('/'); return parts[0][0] + " " + string.Join("/", seg.Take(Math.Min(seg.Length, 3))); })
                         .GroupBy(x => x).OrderBy(g => g.Key))
                sb.AppendLine(g.Count() + "\t" + g.Key);
            sb.AppendLine("```");
            var bad = StagedForbidden();
            sb.AppendLine("## forbidden paths staged: " + bad.Length + "\n```\n" + string.Join("\n", bad.Take(50)) + "\n```");
            sb.AppendLine("## lfs tracked (count)\n```\n" + Git("lfs ls-files", out c).Split('\n').Count(l => l.Trim().Length > 0) + "\n```");
            Write("Stage report", sb.ToString());
        }

        [MenuItem("Watchmaker/Git/2 Commit And Push (uses _planning commit-msg.txt)")]
        public static void CommitAndPush()
        {
            var sb = new StringBuilder();
            string msg = Path.Combine(PlanningDir, "commit-msg.txt");
            if (!File.Exists(msg)) { Write("Commit aborted", "commit-msg.txt missing"); return; }
            var bad = StagedForbidden();
            if (bad.Length > 0) { Write("Commit aborted", "Forbidden paths staged:\n" + string.Join("\n", bad)); return; }
            int c;
            string commit = Git("commit -F \"" + msg + "\"", out c);
            var cl = commit.Split('\n');
            sb.AppendLine("## commit (exit " + c + ")\n```\n" + string.Join("\n", cl.Take(4)) + "\n```");
            if (c == 0)
            {
                string push = Git("push", out c, 300000);
                sb.AppendLine("## push (exit " + c + ")\n```\n" + push + "```");
            }
            sb.AppendLine("## log\n```\n" + Git("log --oneline -3", out c) + "```");
            sb.AppendLine("## status\n```\n" + Git("status -sb", out c) + "```");
            Write("Commit report", sb.ToString());
        }
    }
}
