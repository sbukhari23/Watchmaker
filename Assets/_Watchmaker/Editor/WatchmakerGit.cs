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

        static string Git(string args, out int code, int timeoutMs = 120000, System.Collections.Generic.Dictionary<string, string> env = null)
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
            if (env != null) foreach (var kv in env) psi.EnvironmentVariables[kv.Key] = kv.Value;
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

        /// <summary>Rewrites the messages of all commits after the root commit on the current branch, removing
        /// co-author and session trailer lines. Trees, authors and dates are preserved. Then force-pushes with lease.</summary>
        [MenuItem("Watchmaker/Git/3 Remove Co-Author Trailers And Force Push")]
        public static void StripTrailers()
        {
            var sb = new StringBuilder();
            int c;
            string branch = Git("rev-parse --abbrev-ref HEAD", out c).Trim();
            string oldHead = Git("rev-parse HEAD", out c).Trim();
            string[] commits = Git("rev-list --reverse HEAD", out c).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            string merges = Git("rev-list --merges HEAD", out c).Trim();
            if (commits.Length < 2 || merges.Length > 0) { Write("Rewrite aborted", "Needs a linear history with at least 2 commits."); return; }

            string parent = commits[0];
            int changed = 0;
            for (int i = 1; i < commits.Length; i++)
            {
                string sha = commits[i];
                string body = Git("show -s --format=%B " + sha, out c);
                var kept = body.Replace("\r", "").Split('\n')
                    .Where(l => !l.StartsWith("Co-Authored-By:", StringComparison.OrdinalIgnoreCase) && !l.StartsWith("Claude-Session:", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                while (kept.Count > 0 && kept[kept.Count - 1].Trim().Length == 0) kept.RemoveAt(kept.Count - 1);
                string cleaned = string.Join("\n", kept) + "\n";
                if (cleaned.TrimEnd() != body.Replace("\r", "").TrimEnd()) changed++;

                string[] meta = Git("show -s --format=%an%n%ae%n%aI%n%cn%n%ce%n%cI " + sha, out c).Replace("\r", "").Split('\n');
                var env = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "GIT_AUTHOR_NAME", meta[0] }, { "GIT_AUTHOR_EMAIL", meta[1] }, { "GIT_AUTHOR_DATE", meta[2] },
                    { "GIT_COMMITTER_NAME", meta[3] }, { "GIT_COMMITTER_EMAIL", meta[4] }, { "GIT_COMMITTER_DATE", meta[5] }
                };
                string msgFile = Path.Combine(PlanningDir, "rewrite-msg.txt");
                File.WriteAllText(msgFile, cleaned);
                string created = Git("commit-tree " + sha + "^{tree} -p " + parent + " -F \"" + msgFile + "\"", out c, 120000, env).Trim();
                if (c != 0 || created.Length != 40) { Write("Rewrite aborted", "commit-tree failed for " + sha + ":\n" + created); return; }
                sb.AppendLine(sha.Substring(0, 7) + " -> " + created.Substring(0, 7) + " (" + meta[0] + ")");
                parent = created;
            }

            string diff = Git("diff --stat " + oldHead + " " + parent, out c).Trim();
            if (diff.Length > 0) { Write("Rewrite aborted", "Tree mismatch after rewrite:\n" + diff); return; }

            string upd = Git("update-ref refs/heads/" + branch + " " + parent + " " + oldHead, out c);
            sb.AppendLine("\n## update-ref (exit " + c + ")\n" + upd);
            if (c == 0)
            {
                string push = Git("push --force-with-lease=" + branch + ":" + oldHead + " origin " + branch, out c, 300000);
                sb.AppendLine("## push (exit " + c + ")\n```\n" + push + "```");
            }
            sb.AppendLine("## messages changed: " + changed);
            sb.AppendLine("## log\n```\n" + Git("log --format=%h%x20%an%x20%s -5", out c) + "```");
            sb.AppendLine("## trailers remaining\n```\n" + Git("log --format=%B -5", out c).Split('\n').Count(l => l.StartsWith("Co-Authored-By") || l.StartsWith("Claude-Session")) + "\n```");
            sb.AppendLine("## status\n```\n" + Git("status -sb", out c) + "```");
            Write("Rewrite report", sb.ToString());
        }
    }
}
