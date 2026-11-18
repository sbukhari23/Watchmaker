// Editor-only: places the six Tripo assets (case, case back, hands, pusher, loupe) around the existing movement. Idempotent.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Watchmaker.EditorTools
{
    public static class WatchmakerV2Build
    {
        const string Inc = "Assets/_Watchmaker/Art/Models/Incoming";
        const string MatDir = "Assets/_Watchmaker/Art/Materials";
        static readonly Vector3 Center = new Vector3(0f, 1.0f, 0.42f);

        public static float CaseDiameter = 0.19f, BackDiameter = 0.172f;
        public static bool FlipCase = false, FlipBack = false;

        static GameObject LoadFbx(string folder)
        {
            string[] g = AssetDatabase.FindAssets("t:Model", new[] { Inc + "/" + folder });
            return g.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g[0]));
        }

        static void TexSettings(string path, bool normal, int max)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.maxTextureSize = max; imp.textureCompression = TextureImporterCompression.Compressed; imp.mipmapEnabled = true;
            var a = imp.GetPlatformTextureSettings("Android"); a.overridden = true; a.maxTextureSize = max; a.format = TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(a);
            imp.SaveAndReimport();
        }

        static Material MakeMat(string folder, float metallic, float smooth, int tex)
        {
            string[] g = AssetDatabase.FindAssets("t:Texture2D " + folder + "_basecolor", new[] { Inc + "/" + folder });
            string basePath = g.Length > 0 ? AssetDatabase.GUIDToAssetPath(g[0]) : null;
            string[] n = AssetDatabase.FindAssets("t:Texture2D " + folder + "_normal", new[] { Inc + "/" + folder });
            string normPath = n.Length > 0 ? AssetDatabase.GUIDToAssetPath(n[0]) : null;
            if (basePath != null) TexSettings(basePath, false, tex);
            if (normPath != null) TexSettings(normPath, true, tex);
            string path = MatDir + "/M_V2_" + folder + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            if (basePath != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            if (normPath != null) { mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normPath)); mat.EnableKeyword("_NORMALMAP"); }
            mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Instantiates the model under a wrapper, aligns one mesh-local axis to a world direction, scales so the chosen dimension equals targetSize and centres it on pos.
        static Transform Place(string name, string folder, Transform parent, Vector3 pos, int axisMode, Vector3 worldDir, float target, bool flip, Material mat)
        {
            GameObject fbx = LoadFbx(folder);
            if (fbx == null) { Debug.LogError("[V2] missing " + folder); return null; }
            var old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
            var wrap = new GameObject(name).transform; wrap.SetParent(parent, false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, wrap);
            inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
            var mf = inst.GetComponentInChildren<MeshFilter>();
            Vector3 s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            int axis; float dim;
            if (axisMode == 0) { axis = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2); dim = (s[(axis + 1) % 3] + s[(axis + 2) % 3]) * 0.5f; } // thinnest axis, diameter
            else { axis = s.x >= s.y && s.x >= s.z ? 0 : (s.y >= s.z ? 1 : 2); dim = s[axis]; }                                                   // longest axis
            Vector3 local = axis == 0 ? Vector3.right : (axis == 1 ? Vector3.up : Vector3.forward);
            Vector3 cur = mf.transform.TransformDirection(local);
            wrap.rotation = Quaternion.FromToRotation(cur, flip ? -worldDir : worldDir);
            wrap.localScale = Vector3.one * (target / dim);
            wrap.position = pos;
            var r = inst.GetComponentInChildren<Renderer>();
            inst.transform.position += pos - r.bounds.center;
            if (mat != null) foreach (var rr in inst.GetComponentsInChildren<Renderer>()) rr.sharedMaterial = mat;
            return wrap;
        }

        [MenuItem("Watchmaker/Setup/9 Build V2 Watch")]
        public static void Build()
        {
            GameObject root = GameObject.Find("WatchV2");
            if (root == null) root = new GameObject("WatchV2");
            var rt = root.transform;

            Material mCase = MakeMat("01_WATCH_CASE_BODY", 0.7f, 0.6f, 1024);
            Material mBack = MakeMat("02_CASE_BACK", 0.8f, 0.6f, 512);
            Material mHour = MakeMat("04a_HOUR_HAND", 0.6f, 0.6f, 512);
            Material mMin = MakeMat("04b_MINUTE_HAND", 0.6f, 0.6f, 512);
            Material mPush = MakeMat("06_SIDE_PUSHER_BUTTON", 0.8f, 0.6f, 512);
            Material mLoupe = MakeMat("11_JEWELERS_LOUPE", 0.4f, 0.55f, 1024);
            AssetDatabase.SaveAssets();

            float h = CaseDiameter * 0.285f;
            Place("Case", "01_WATCH_CASE_BODY", rt, Center + Vector3.up * (0.0015f + h * 0.5f), 0, Vector3.up, CaseDiameter, FlipCase, mCase);
            Place("CaseBack", "02_CASE_BACK", rt, Center + Vector3.up * (h + 0.012f), 0, Vector3.up, BackDiameter, FlipBack, mBack);
            Place("HourHand", "04a_HOUR_HAND", rt, Center + new Vector3(0f, h * 0.9f, 0.01f), 1, Vector3.forward, 0.05f, false, mHour);
            Place("MinuteHand", "04b_MINUTE_HAND", rt, Center + new Vector3(0.012f, h * 0.9f + 0.003f, 0.02f), 1, new Vector3(-0.5f, 0f, 0.85f), 0.075f, false, mMin);
            Place("SidePusher", "06_SIDE_PUSHER_BUTTON", rt, Center + new Vector3(CaseDiameter * 0.5f, h * 0.5f, -CaseDiameter * 0.26f), 1, new Vector3(0.87f, 0f, -0.5f), 0.035f, false, mPush);
            Place("Loupe", "11_JEWELERS_LOUPE", rt, new Vector3(0.20f, 1.03f, 0.30f), 1, Vector3.right, 0.13f, false, mLoupe);

            EditorSceneManager.MarkSceneDirty(rt.gameObject.scene);
            Debug.Log("[V2] placed assets");
        }
    }
}
