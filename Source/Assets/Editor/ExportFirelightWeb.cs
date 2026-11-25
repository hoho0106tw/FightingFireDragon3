using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class ExportFirelightWeb
{
    private const string Scene = "Assets/Scenes/Firelight Forest.unity";
    private const string PagesFolder = "Firelight Forest GitHub Pages";

    // 共用現有批次匯出入口，讓本機啟動器和 GitHub Pages 使用同一個 WebGL 設定。
    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        int outputIndex = Array.IndexOf(args, "-webOutput");
        if (outputIndex < 0 || outputIndex + 1 >= args.Length)
            throw new ArgumentException("Pass -webOutput followed by the player output directory.");
        BuildPackage(Path.GetFullPath(args[outputIndex + 1]));
    }

    [MenuItem("Firelight/Build GitHub Pages")]
    public static void BuildGitHubPages()
    {
        ExportFromEditor(PagesFolder);
    }

    [MenuItem("Firelight/Build FightingFireDragon3")]
    public static void BuildFightingFireDragon3()
    {
        ExportFromEditor("FightingFireDragon3-WebGL");
    }

    private static void ExportFromEditor(string folder)
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        string output = Path.Combine(project, "Release", folder);
        string zip = BuildPackage(output);
        EditorUtility.RevealInFinder(zip);
    }

    private static string BuildPackage(string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        Build(output);
        // ZIP 內的 index.html 直接位於根目錄，解壓後即可上傳整組靜態檔案。
        string zip = output + ".zip";
        string temporary = zip + ".tmp";
        File.Delete(temporary);
        ZipFile.CreateFromDirectory(output, temporary, System.IO.Compression.CompressionLevel.Optimal, false);
        if (File.Exists(zip))
            File.Replace(temporary, zip, null);
        else
            File.Move(temporary, zip);
        Debug.Log($"GitHub Pages package: {zip}");
        return zip;
    }

    private static void Build(string output)
    {
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length != 1 || scenes[0] != Scene)
            throw new InvalidOperationException($"Enable only {Scene} in Build Profiles.");

        // GitHub Pages 使用靜態檔案。輸出原始 WASM，無須在伺服器自訂壓縮標頭。
        WebGLCompressionFormat previousCompression = PlayerSettings.WebGL.compressionFormat;
        bool previousHashedNames = PlayerSettings.WebGL.nameFilesAsHashes;
        string previousName = PlayerSettings.productName;
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
        if (pipeline == null || renderer == null)
            throw new InvalidOperationException("The WebGL Universal Render Pipeline assets are missing.");

        bool previousHDR = pipeline.supportsHDR;
        float previousScale = pipeline.renderScale;
        IntermediateTextureMode previousTextureMode = renderer.intermediateTextureMode;
        BuildReport result;
        try
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.productName = "Firelight Forest";
            // Use an SDR intermediate target for the WebGL forward renderer.
            pipeline.supportsHDR = false;
            pipeline.renderScale = 1f;
            renderer.intermediateTextureMode = IntermediateTextureMode.Always;
            SaveRenderingAssets(pipeline, renderer);
            result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
        }
        finally
        {
            renderer.intermediateTextureMode = previousTextureMode;
            pipeline.renderScale = previousScale;
            pipeline.supportsHDR = previousHDR;
            PlayerSettings.productName = previousName;
            PlayerSettings.WebGL.compressionFormat = previousCompression;
            PlayerSettings.WebGL.nameFilesAsHashes = previousHashedNames;
            SaveRenderingAssets(pipeline, renderer);
        }
        if (result.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"WebGL build ended with {result.summary.result}.");

        string index = Path.Combine(output, "index.html");
        string html = File.ReadAllText(index);
        const string mobileCheck = "/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)";
        const string tabletCheck = "(navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)";
        if (!html.Contains(tabletCheck))
            html = html.Replace(mobileCheck, mobileCheck + " || " + tabletCheck);
        const string canvasStyle = "<style>#unity-canvas { touch-action: none; }</style>";
        if (!html.Contains(canvasStyle))
            html = html.Replace("</head>", canvasStyle + "\n</head>");
        const string canvasHandler = "<script>document.querySelector(\"#unity-canvas\")?.addEventListener(\"contextmenu\", event => event.preventDefault());</script>";
        if (!html.Contains(canvasHandler))
            html = html.Replace("</body>", canvasHandler + "\n</body>");
        AddLegacyBuildAliases(output, html);
        File.WriteAllText(index, html);
        File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
    }

    private static void AddLegacyBuildAliases(string output, string html)
    {
        string[] assets = Regex.Matches(html, @"\bbuildUrl\s*\+\s*""/([^/""\r\n]+)""")
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToArray();
        string[] extensions = { ".loader.js", ".data", ".framework.js", ".wasm" };
        string[] names = extensions
            .Select(extension => assets.Single(asset => asset.EndsWith(extension, StringComparison.Ordinal)))
            .ToArray();
        string build = Path.Combine(output, "Build");
        for (int i = 0; i < extensions.Length; i++)
        {
            string legacyName = PagesFolder + extensions[i];
            if (names[i] != legacyName)
                File.Copy(Path.Combine(build, names[i]), Path.Combine(build, legacyName), true);
        }

        // A cached entry page can still call the loader at its original URL.
        if (names[0] == PagesFolder + extensions[0])
            return;

        string legacyLoader = Path.Combine(build, PagesFolder + extensions[0]);
        File.AppendAllText(legacyLoader,
            "\n;(() => {\n" +
            "  const loadUnity = createUnityInstance;\n" +
            "  createUnityInstance = (canvas, config, onProgress) => loadUnity(canvas, { ...config,\n" +
            $"    dataUrl: \"Build/{names[1]}\",\n" +
            $"    frameworkUrl: \"Build/{names[2]}\",\n" +
            $"    codeUrl: \"Build/{names[3]}\"\n" +
            "  }, onProgress);\n" +
            "})();\n");
    }

    private static void SaveRenderingAssets(UniversalRenderPipelineAsset pipeline, UniversalRendererData renderer)
    {
        EditorUtility.SetDirty(pipeline);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssetIfDirty(pipeline);
        AssetDatabase.SaveAssetIfDirty(renderer);
    }
}
