using System.IO;
using System.Linq;
using RailwayMission.Core;
using RailwayMission.Data;
using RailwayMission.Track;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RailwayMission.EditorTools
{
    /// <summary>
    /// 編輯器工具：選單「Railway Mission」。
    /// - 套用 Android 專案設定（橫向、IL2CPP、ARM64、套件名稱）
    /// - 建立 Mission01 場景並加入 Build Settings
    /// - 建立可編輯的預設資料資產（路線／火車／任務）
    /// - 一鍵建置 Android APK（也供 GitHub Actions 使用）
    /// </summary>
    [InitializeOnLoad]
    public static class RailwayMissionEditor
    {
        public const string ScenePath = "Assets/RailwayMission/Scenes/Mission01.unity";
        const string DataFolder = "Assets/RailwayMission/Data";
        const string SetupKey = "RailwayMission.ProjectConfigured.v1";

        static RailwayMissionEditor()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool(SetupKey + "." + Application.dataPath.GetHashCode(), false))
                {
                    ApplyProjectSettings();
                    EditorPrefs.SetBool(SetupKey + "." + Application.dataPath.GetHashCode(), true);
                }
            };
        }

        [MenuItem("Railway Mission/1. 套用 Android 專案設定", priority = 1)]
        public static void ApplyProjectSettings()
        {
            PlayerSettings.companyName = "apsmkimo";
            PlayerSettings.productName = "Railway Mission 3D";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.apsmkimo.railwaymission3d");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            Debug.Log("[Railway Mission] 已套用 Android 專案設定：橫向、IL2CPP、ARM64、com.apsmkimo.railwaymission3d");
        }

        [MenuItem("Railway Mission/2. 建立 Mission01 場景", priority = 2)]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateSceneInternal();
            EditorSceneManager.OpenScene(ScenePath);
        }

        static void CreateSceneInternal()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var gm = new GameObject("RailwayMission").AddComponent<GameManager>();
            // 若已建立資料資產則自動指定
            gm.routeAsset = AssetDatabase.LoadAssetAtPath<RouteAsset>($"{DataFolder}/QinglingLine.asset");
            gm.trainData = AssetDatabase.LoadAssetAtPath<Train.TrainData>($"{DataFolder}/EMU_Commuter.asset");
            gm.missionData = AssetDatabase.LoadAssetAtPath<Mission.MissionData>($"{DataFolder}/Mission01_FirstDrive.asset");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Railway Mission] 已建立場景並加入 Build Settings：" + ScenePath);
        }

        [MenuItem("Railway Mission/3. 建立可編輯的資料資產", priority = 3)]
        public static void CreateDataAssets()
        {
            Directory.CreateDirectory(DataFolder);
            AssetDatabase.Refresh();
            var route = ScriptableObject.CreateInstance<RouteAsset>();
            route.route = DefaultContent.CreateQinglingLine();
            AssetDatabase.CreateAsset(route, $"{DataFolder}/QinglingLine.asset");
            AssetDatabase.CreateAsset(DefaultContent.CreateEmu(), $"{DataFolder}/EMU_Commuter.asset");
            AssetDatabase.CreateAsset(DefaultContent.CreateMission01(), $"{DataFolder}/Mission01_FirstDrive.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[Railway Mission] 已建立資料資產：" + DataFolder + "（重新執行步驟 2 會自動套用到場景）");
        }

        [MenuItem("Railway Mission/4. 建置 Android APK", priority = 20)]
        public static void BuildAndroidMenu() => BuildAndroid();

        /// <summary>命令列／CI 建置入口：-executeMethod RailwayMission.EditorTools.RailwayMissionEditor.BuildAndroid</summary>
        public static void BuildAndroid()
        {
            ApplyProjectSettings();
            if (!File.Exists(ScenePath)) CreateSceneInternal();
            string output = "Builds/Android/RailwayMission3D.apk";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Railway Mission] 建置結果：{report.summary.result}，大小 {report.summary.totalSize / (1024 * 1024)} MB → {output}");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
