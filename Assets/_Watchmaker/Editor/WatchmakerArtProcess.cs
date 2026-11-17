using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

namespace Watchmaker.EditorTools
{
    /// <summary>
    /// Turns the heavy Tripo FBX files into Quest-ready prefabs:
    /// decimated mesh, packed metallic-smoothness texture, ASTC import settings, URP Lit material, real-world scale.
    /// </summary>
    public static class WatchmakerArtProcess
    {
        struct Item
        {
            public string name, fbx, texDir;
            public int targetTris;
            public char axis;        // axis of the normalised model that gets a known real-world size
            public float meters;     // real-world size (game scale) of that axis
            public int texSize;
        }

        const string ModelsRoot = "Assets/_Watchmaker/Art/Models/";
        const string OutMesh = "Assets/_Watchmaker/Art/Models/Optimized";
        const string OutPrefab = "Assets/_Watchmaker/Art/Prefabs";
        const string OutMat = "Assets/_Watchmaker/Art/Materials";
        const string OutTex = "Assets/_Watchmaker/Art/Textures";

        static readonly Item[] Items =
        {
            new Item { name = "P1_Workbench",   fbx = ModelsRoot + "Environment/P1_Workbench/P1_Workbench.fbx",     texDir = ModelsRoot + "Environment/P1_Workbench/P1_Workbench.fbm",     targetTris = 15000, axis = 'y', meters = 0.95f,  texSize = 2048 },
            new Item { name = "P3_Tray",        fbx = ModelsRoot + "Environment/P3_Tray/P3_Tray.fbx",               texDir = ModelsRoot + "Environment/P3_Tray/P3_Tray.fbm",               targetTris = 3000,  axis = 'x', meters = 0.22f,  texSize = 1024 },
            new Item { name = "P4_Cushion",     fbx = ModelsRoot + "Environment/P4_Cushion/P4_Cushion.fbx",         texDir = ModelsRoot + "Environment/P4_Cushion/P4_Cushion.fbm",         targetTris = 2000,  axis = 'x', meters = 0.11f,  texSize = 1024 },
            new Item { name = "P5_Screwdriver", fbx = ModelsRoot + "Tools/P5_Screwdriver/P5_Screwdriver.fbx",       texDir = ModelsRoot + "Tools/P5_Screwdriver/P5_Screwdriver.fbm",       targetTris = 2000,  axis = 'x', meters = 0.18f,  texSize = 1024 },
            new Item { name = "P8_LoupeLever",  fbx = ModelsRoot + "Tools/P8_LoupeLever/P8_LoupeLever.fbx",         texDir = ModelsRoot + "Tools/P8_LoupeLever/P8_LoupeLever.fbm",         targetTris = 2500,  axis = 'x', meters = 0.14f,  texSize = 1024 },
        };

        [MenuItem("Watchmaker/Art/2 Build Optimized Props")]
        public static void BuildAll()
        {
            var report = new StringBuilder();
            report.AppendLine("\n# Art build (optimized props) " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            EnsureFolder("Assets/_Watchmaker/Art/Models", "Optimized");
            EnsureFolder("Assets/_Watchmaker/Art", "Prefabs");
            EnsureFolder("Assets/_Watchmaker/Art", "Materials");
            EnsureFolder("Assets/_Watchmaker/Art", "Textures");

            for (int i = 0; i < Items.Length; i++)
            {
                var it = Items[i];
                EditorUtility.DisplayProgressBar("Watchmaker art", "Processing " + it.name, i / (float)Items.Length);
                try { Process(it, report); }
                catch (System.Exception e) { report.AppendLine("- FAIL | " + it.name + " | " + e.Message); Debug.LogError(e); }
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Append(report.ToString());
            Debug.Log("[Watchmaker] Art build finished, see _planning/art-report.md");
        }

        static void Process(Item it, StringBuilder report)
        {
            // 1. Texture import settings
            SetupTexture(it.texDir + "/" + it.name + "_basecolor.jpg", true, false, it.texSize);
            SetupTexture(it.texDir + "/" + it.name + "_normal.jpg", false, true, it.texSize);

            // 2. Packed metallic (R) + smoothness (A) texture
            string msPath = PackMetallicSmoothness(it);

            // 3. Material
            var mat = CreateMaterial(it, msPath);

            // 4. Mesh from the FBX
            var fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(it.fbx);
            if (fbxRoot == null) throw new System.Exception("FBX not found: " + it.fbx);
            MeshFilter best = null; int bestTris = -1;
            foreach (var mf in fbxRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                int t = 0;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) t += (int)(mf.sharedMesh.GetIndexCount(s) / 3);
                if (t > bestTris) { bestTris = t; best = mf; }
            }
            if (best == null) throw new System.Exception("no mesh in " + it.fbx);

            var src = best.sharedMesh;

            // Repeated passes: one pass of the quadric simplifier stops early on dense scans.
            Mesh current = src;
            int currentTris = bestTris;
            for (int pass = 0; pass < 10 && currentTris > it.targetTris * 1.05f; pass++)
            {
                var simplifier = new MeshSimplifier();
                simplifier.Initialize(current);
                var opts = SimplificationOptions.Default;
                opts.PreserveBorderEdges = false;
                opts.PreserveUVSeamEdges = false;
                opts.PreserveUVFoldoverEdges = false;
                opts.PreserveSurfaceCurvature = false;
                opts.EnableSmartLink = true;
                opts.MaxIterationCount = 1000;
                opts.Agressiveness = 8.0;
                simplifier.SimplificationOptions = opts;
                float q = Mathf.Clamp01(it.targetTris / (float)currentTris);
                simplifier.SimplifyMesh(q);
                var next = simplifier.ToMesh();
                int nextTris = 0;
                for (int s2 = 0; s2 < next.subMeshCount; s2++) nextTris += (int)(next.GetIndexCount(s2) / 3);
                if (current != src) Object.DestroyImmediate(current);
                current = next;
                if (nextTris >= currentTris * 0.97f) { currentTris = nextTris; break; }
                currentTris = nextTris;
            }
            var simplifierResult = current;
            var dst = simplifierResult == src ? Object.Instantiate(src) : simplifierResult;
            dst.name = it.name + "_opt";
            dst.RecalculateBounds();
            dst.RecalculateTangents();
            dst.UploadMeshData(false);

            string meshPath = OutMesh + "/" + it.name + "_opt.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(dst, meshPath);

            int newTris = 0;
            for (int s = 0; s < dst.subMeshCount; s++) newTris += (int)(dst.GetIndexCount(s) / 3);

            // 5. Prefab: node transform of the FBX mesh (relative to FBX root) * real-world scale
            Matrix4x4 rel = fbxRoot.transform.worldToLocalMatrix * best.transform.localToWorldMatrix;
            var rends = fbxRoot.GetComponentsInChildren<Renderer>(true);
            Bounds b = default; bool first = true;
            foreach (var r in rends) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            float refSize = it.axis == 'y' ? b.size.y : (it.axis == 'z' ? b.size.z : b.size.x);
            float scale = it.meters / Mathf.Max(0.0001f, refSize);

            var root = new GameObject(it.name);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            var mr = model.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            model.transform.localPosition = rel.GetPosition() * scale;
            model.transform.localRotation = rel.rotation;
            model.transform.localScale = rel.lossyScale * scale;

            string prefabPath = OutPrefab + "/" + it.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            report.AppendLine("- PASS | " + it.name + " | tris " + bestTris + " -> " + newTris + " (target " + it.targetTris +
                              ") | scale " + scale.ToString("F4") + " | prefab " + prefabPath);
        }

        static void SetupTexture(string path, bool srgb, bool normal, int maxSize)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) throw new System.Exception("texture not found: " + path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.mipmapEnabled = true;
            imp.maxTextureSize = maxSize;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = maxSize,
                format = TextureImporterFormat.ASTC_6x6, textureCompression = TextureImporterCompression.Compressed,
            });
            imp.SaveAndReimport();
        }

        static string PackMetallicSmoothness(Item it)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string metFile = Path.Combine(root, it.texDir, it.name + "_metallic.jpg");
            string rouFile = Path.Combine(root, it.texDir, it.name + "_roughness.jpg");
            var met = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var rou = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            ImageConversion.LoadImage(met, File.ReadAllBytes(metFile));
            ImageConversion.LoadImage(rou, File.ReadAllBytes(rouFile));

            int n = it.texSize;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                float v = (y + 0.5f) / n;
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;
                    float m = met.GetPixelBilinear(u, v).r;
                    float r = rou.GetPixelBilinear(u, v).r;
                    px[y * n + x] = new Color32((byte)(m * 255f), 0, 0, (byte)((1f - r) * 255f));
                }
            }
            var packed = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            packed.SetPixels32(px);
            packed.Apply();
            string assetPath = OutTex + "/" + it.name + "_MS.png";
            File.WriteAllBytes(Path.Combine(root, assetPath), ImageConversion.EncodeToPNG(packed));
            Object.DestroyImmediate(met); Object.DestroyImmediate(rou); Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            imp.sRGBTexture = false;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.mipmapEnabled = true;
            imp.maxTextureSize = n;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = n,
                format = TextureImporterFormat.ASTC_6x6, textureCompression = TextureImporterCompression.Compressed,
            });
            imp.SaveAndReimport();
            return assetPath;
        }

        static Material CreateMaterial(Item it, string msPath)
        {
            string path = OutMat + "/M_" + it.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(it.texDir + "/" + it.name + "_basecolor.jpg");
            var nrmTex = AssetDatabase.LoadAssetAtPath<Texture2D>(it.texDir + "/" + it.name + "_normal.jpg");
            var msTex = AssetDatabase.LoadAssetAtPath<Texture2D>(msPath);
            mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", nrmTex);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", msTex);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_ReceiveShadows", 0f);
            mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        static void Append(string text)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(root, "_planning");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "art-report.md"), text);
        }
    }
}
