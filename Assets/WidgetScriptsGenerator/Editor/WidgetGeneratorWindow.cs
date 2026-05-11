using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WidgetScriptsGenerator.Editor
{
    public class WidgetGeneratorWindow : EditorWindow
    {
        const string PLACEHOLDER_PACKAGE = "com.hc.widgetsample";
        const string PLUGINS_DEST = "Assets/Plugins/Android";
        const string ANDROIDLIB_FOLDER = "GameWidget.androidlib";

        static string SamplesRoot
        {
            get
            {
                // Locate this script file and derive the Editor folder path from it.
                var scriptGuid = AssetDatabase.FindAssets($"t:MonoScript {nameof(WidgetGeneratorWindow)}");
                foreach (var guid in scriptGuid)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.EndsWith($"{nameof(WidgetGeneratorWindow)}.cs"))
                    {
                        string dirAssetPath = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                        return Path.GetFullPath(dirAssetPath).Replace('\\', '/');
                    }
                }
                // Fallback 1: Package path
                string packagePath = "Packages/com.hc.widgetscriptsgenerator/Editor";
                string packageFull = Path.GetFullPath(packagePath).Replace('\\', '/');
                if (Directory.Exists(packageFull))
                    return packageFull;

                // Fallback 2: Assets path
                return Path.GetFullPath("Assets/WidgetScriptsGenerator/Editor").Replace('\\', '/');
            }
        }

        static string ScriptsSample => SamplesRoot + "/Scripts.sample";
        static string PluginsSample => SamplesRoot + "/Plugins.sample";

        string _packageName;
        string _scriptsFolder = "Assets/Scripts";
        Vector2 _scroll;

        // Status
        bool _checkedStatus;
        FileStatus _widgetData;
        FileStatus _widgetDataWriter;
        FileStatus _widgetUtility;
        FileStatus _addWidgetButton;
        FileStatus _androidLib;
        FileStatus _mainGradle;
        FileStatus _settingsGradle;

        [MenuItem("Tools/Widget Scripts Generator")]
        static void ShowWindow()
        {
            var window = GetWindow<WidgetGeneratorWindow>("Widget Generator");
            window.minSize = new Vector2(480, 520);
        }

        void OnEnable()
        {
            _packageName = PlayerSettings.applicationIdentifier;
            RefreshStatus();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Header
            EditorGUILayout.LabelField("Android Widget Scripts Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Generates all files needed for an Android home-screen widget.\n" +
                "The package name is used to identify your widget provider across Java, C#, and manifest files.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            // Package Name
            EditorGUILayout.LabelField("Package Name", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _packageName = EditorGUILayout.TextField("Application Identifier", _packageName);
            if (EditorGUI.EndChangeCheck())
                _checkedStatus = false;

            if (GUILayout.Button("Reset to Player Settings"))
            {
                _packageName = PlayerSettings.applicationIdentifier;
                _checkedStatus = false;
            }

            EditorGUILayout.Space(8);

            // Scripts output folder
            EditorGUILayout.LabelField("Output Folders", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _scriptsFolder = EditorGUILayout.TextField("C# Scripts", _scriptsFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string picked = EditorUtility.OpenFolderPanel("Select Scripts Folder", "Assets", "");
                if (!string.IsNullOrEmpty(picked))
                {
                    if (picked.Contains("Assets"))
                        _scriptsFolder = "Assets" + picked.Substring(picked.IndexOf("Assets") + 6);
                    else
                        Debug.LogWarning("Please select a folder inside the Assets directory.");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Android Plugins", PLUGINS_DEST);
            EditorGUILayout.Space(12);

            // Status
            EditorGUILayout.LabelField("File Status", EditorStyles.boldLabel);
            if (!_checkedStatus)
                RefreshStatus();

            DrawStatus("WidgetData.cs", _widgetData);
            DrawStatus("WidgetDataWriter.cs", _widgetDataWriter);
            DrawStatus("WidgetUtility.cs", _widgetUtility);
            DrawStatus("AddWidgetButton.cs", _addWidgetButton);
            EditorGUILayout.Space(4);
            DrawStatus(ANDROIDLIB_FOLDER + "/", _androidLib);
            DrawStatus("mainTemplate.gradle", _mainGradle);
            DrawStatus("settingsTemplate.gradle", _settingsGradle);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Refresh Status"))
                RefreshStatus();

            EditorGUILayout.Space(12);

            // Validation
            if (string.IsNullOrWhiteSpace(_packageName) || !_packageName.Contains("."))
            {
                EditorGUILayout.HelpBox(
                    "Please enter a valid package name (e.g., com.MyCompany.MyGame).",
                    MessageType.Warning);
                GUI.enabled = false;
            }

            // Generate
            EditorGUILayout.LabelField("Generate", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate All Missing Files", GUILayout.Height(32)))
            {
                GenerateAll(overwriteExisting: false);
            }

            EditorGUILayout.Space(4);

            GUI.color = new Color(1f, 0.85f, 0.85f);
            if (GUILayout.Button("Force Regenerate All (Overwrites Existing)"))
            {
                if (EditorUtility.DisplayDialog("Overwrite All?",
                    "This will overwrite all widget files including any customizations you've made. Are you sure?",
                    "Overwrite", "Cancel"))
                {
                    GenerateAll(overwriteExisting: true);
                }
            }
            GUI.color = Color.white;

            GUI.enabled = true;

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        // ─── Status ──────────────────────────────────────────────

        enum FileStatus { Missing, Exists, NeedsUpdate }

        void RefreshStatus()
        {
            _widgetData = CheckCSharpFile("WidgetData.cs", null);
            _widgetDataWriter = CheckCSharpFile("WidgetDataWriter.cs", "WidgetUtility.RequestWidgetUpdate");
            _widgetUtility = CheckCSharpFile("WidgetUtility.cs", "RequestPinWidget");
            _addWidgetButton = CheckCSharpFile("AddWidgetButton.cs", "WidgetUtility.RequestPinWidget");
            _androidLib = CheckAndroidLib();
            _mainGradle = CheckFileExists(Path.Combine(PLUGINS_DEST, "mainTemplate.gradle"));
            _settingsGradle = CheckFileExists(Path.Combine(PLUGINS_DEST, "settingsTemplate.gradle"));
            _checkedStatus = true;
        }

        FileStatus CheckFileExists(string path)
        {
            return File.Exists(path) ? FileStatus.Exists : FileStatus.Missing;
        }

        FileStatus CheckCSharpFile(string fileName, string requiredSnippet)
        {
            string path = Path.Combine(_scriptsFolder, fileName);
            if (!File.Exists(path))
            {
                // Search the whole project
                string[] guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName) + " t:MonoScript");
                if (guids.Length == 0)
                    guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName) + " t:TextAsset");

                bool found = false;
                foreach (var guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.EndsWith(fileName))
                    {
                        path = assetPath;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return FileStatus.Missing;
            }

            if (requiredSnippet == null)
                return FileStatus.Exists;

            string content = File.ReadAllText(path);
            return content.Contains(requiredSnippet) ? FileStatus.Exists : FileStatus.NeedsUpdate;
        }

        FileStatus CheckAndroidLib()
        {
            string libPath = Path.Combine(PLUGINS_DEST, ANDROIDLIB_FOLDER);
            if (!Directory.Exists(libPath))
                return FileStatus.Missing;

            // Check critical files
            string[] required = {
                "AndroidManifest.xml", "build.gradle", "project.properties", "proguard-rules.pro",
                "res/layout/widget_layout.xml", "res/xml/widget_info.xml"
            };

            foreach (var f in required)
            {
                if (!File.Exists(Path.Combine(libPath, f)))
                    return FileStatus.NeedsUpdate;
            }

            // Check if the Java provider exists (in any package path)
            bool javaFound = Directory.GetFiles(libPath, "GameWidgetProvider.java", SearchOption.AllDirectories).Length > 0;
            if (!javaFound)
                return FileStatus.NeedsUpdate;

            // Check if package name matches
            string manifest = File.ReadAllText(Path.Combine(libPath, "AndroidManifest.xml"));
            if (!manifest.Contains(_packageName))
                return FileStatus.NeedsUpdate;

            return FileStatus.Exists;
        }

        void DrawStatus(string label, FileStatus status)
        {
            EditorGUILayout.BeginHorizontal();
            string icon;
            switch (status)
            {
                case FileStatus.Exists:     icon = "✅"; break; // green check
                case FileStatus.NeedsUpdate: icon = "⚠️"; break; // warning
                default:                     icon = "❌"; break; // red X
            }
            EditorGUILayout.LabelField($"  {icon}  {label}", GUILayout.Width(300));
            EditorGUILayout.LabelField(status.ToString(), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ─── Generation ──────────────────────────────────────────

        void GenerateAll(bool overwriteExisting)
        {
            int generated = 0;
            int skipped = 0;

            // Ensure output directories exist
            Directory.CreateDirectory(_scriptsFolder);
            Directory.CreateDirectory(PLUGINS_DEST);

            // C# scripts
            generated += GenerateCSharpFile("WidgetData.cs", overwriteExisting, ref skipped);
            generated += GenerateCSharpFile("WidgetDataWriter.cs", overwriteExisting, ref skipped);
            generated += GenerateCSharpFile("WidgetUtility.cs", overwriteExisting, ref skipped);
            generated += GenerateCSharpFile("AddWidgetButton.cs", overwriteExisting, ref skipped);

            // Android Library (.androidlib)
            generated += GenerateAndroidLib(overwriteExisting, ref skipped);

            // Gradle templates (only if missing — never overwrite since user may have customized)
            generated += GenerateGradleTemplate("mainTemplate.gradle", overwriteExisting, ref skipped);
            generated += GenerateGradleTemplate("settingsTemplate.gradle", overwriteExisting, ref skipped);

            AssetDatabase.Refresh();
            RefreshStatus();

            string msg = $"Generated {generated} file(s).";
            if (skipped > 0) msg += $" Skipped {skipped} existing file(s).";
            EditorUtility.DisplayDialog("Widget Generator", msg, "OK");
            Debug.Log($"[WidgetGenerator] {msg}");
        }

        int GenerateCSharpFile(string fileName, bool overwrite, ref int skipped)
        {
            string destPath = Path.Combine(_scriptsFolder, fileName);

            if (File.Exists(destPath) && !overwrite)
            {
                skipped++;
                return 0;
            }

            string samplePath = Path.Combine(ScriptsSample, fileName + ".sample");
            if (!File.Exists(samplePath))
            {
                Debug.LogWarning($"[WidgetGenerator] Sample file not found: {samplePath}");
                return 0;
            }

            string content = File.ReadAllText(samplePath);
            content = ReplacePackageName(content);
            File.WriteAllText(destPath, content);
            Debug.Log($"[WidgetGenerator] Created {destPath}");
            return 1;
        }

        int GenerateAndroidLib(bool overwrite, ref int skipped)
        {
            string srcRoot = Path.Combine(PluginsSample, "Android", ANDROIDLIB_FOLDER);
            string destRoot = Path.Combine(PLUGINS_DEST, ANDROIDLIB_FOLDER);

            if (!Directory.Exists(srcRoot))
            {
                Debug.LogWarning($"[WidgetGenerator] Sample androidlib not found: {srcRoot}");
                return 0;
            }

            int count = 0;

            // Copy all files from the sample, replacing package names in text files
            // Special handling for the Java source: directory path must match package name
            string[] allFiles = Directory.GetFiles(srcRoot, "*", SearchOption.AllDirectories);

            foreach (string srcFile in allFiles)
            {
                if (srcFile.EndsWith(".meta")) continue;

                // Compute relative path from sample root
                string relativePath = srcFile.Substring(srcRoot.Length + 1).Replace('\\', '/');

                // If this is the Java file, remap directory to match package name
                if (relativePath.Contains("GameWidgetProvider.java"))
                {
                    string packagePath = _packageName.Replace('.', '/');
                    relativePath = $"src/main/java/{packagePath}/GameWidgetProvider.java";
                }

                string destFile = Path.Combine(destRoot, relativePath);

                if (File.Exists(destFile) && !overwrite)
                {
                    skipped++;
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                // Binary files (images, fonts) — copy directly
                string ext = Path.GetExtension(srcFile).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".ttf" || ext == ".otf")
                {
                    File.Copy(srcFile, destFile, overwrite);
                }
                else
                {
                    // Text files — replace package name
                    string content = File.ReadAllText(srcFile);
                    content = ReplacePackageName(content);
                    File.WriteAllText(destFile, content);
                }

                Debug.Log($"[WidgetGenerator] Created {destFile}");
                count++;
            }

            return count;
        }

        int GenerateGradleTemplate(string fileName, bool overwrite, ref int skipped)
        {
            string destPath = Path.Combine(PLUGINS_DEST, fileName);

            // Gradle templates are special — if they already exist, never overwrite
            // because the user may have added other dependencies or plugins
            if (File.Exists(destPath))
            {
                skipped++;
                return 0;
            }

            string srcPath = Path.Combine(PluginsSample, "Android", fileName);
            if (!File.Exists(srcPath))
            {
                Debug.LogWarning($"[WidgetGenerator] Sample not found: {srcPath}");
                return 0;
            }

            File.Copy(srcPath, destPath, false);
            Debug.Log($"[WidgetGenerator] Created {destPath}");
            return 1;
        }

        string ReplacePackageName(string content)
        {
            return content.Replace(PLACEHOLDER_PACKAGE, _packageName);
        }
    }
}
