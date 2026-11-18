// Editor-only: tidies the submission folder by moving development drafts into a _dev_notes subfolder (nothing is deleted).
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerSubmissionTidy
    {
        const string Dir = @"E:\Unity\Unity Projects\Watchmaker_Submission";
        static readonly string[] Drafts = { "5_ASSET_PROMPTS_FOR_CHATGPT.md", "5b_TRIPO_TEXT_PROMPTS.md", "6_FULL_INTERACTION_PLAN.md", "size-report.txt" };

        [MenuItem("Watchmaker/Build/5 Tidy Submission Folder")]
        public static void Tidy()
        {
            string notes = Path.Combine(Dir, "_dev_notes");
            Directory.CreateDirectory(notes);
            int moved = 0;
            foreach (string f in Drafts)
            {
                string src = Path.Combine(Dir, f), dst = Path.Combine(notes, f);
                if (!File.Exists(src)) continue;
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(src, dst);
                moved++;
            }
            Debug.Log("[Tidy] moved " + moved + " draft files to " + notes);
        }
    }
}
