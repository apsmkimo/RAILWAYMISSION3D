#if RM_URP
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RailwayMission.EditorTools
{
    /// <summary>
    /// 自動建立並套用適合 Android 的 URP 設定（第一次開啟專案時執行一次，也可從選單重新執行）。
    /// </summary>
    [InitializeOnLoad]
    public static class RailwayMissionURPSetup
    {
        const string Folder = "Assets/RailwayMission/Settings";
        const string AssetPath = Folder + "/URP_Mobile.asset";

        static RailwayMissionURPSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (GraphicsSettings.defaultRenderPipeline == null) Setup();
            };
        }

        [MenuItem("Railway Mission/5. 套用 URP 行動版渲染設定", priority = 30)]
        public static void Setup()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (asset == null)
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
                asset = UniversalRenderPipelineAsset.Create();
                AssetDatabase.CreateAsset(asset, AssetPath);
                asset.LoadBuiltinRendererData();
            }
            asset.shadowDistance = 160f;
            asset.msaaSampleCount = 2;
            asset.supportsHDR = false;
            asset.renderScale = 1f;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            GraphicsSettings.defaultRenderPipeline = asset;
            if (QualitySettings.renderPipeline == null) QualitySettings.renderPipeline = asset;
            Debug.Log("[Railway Mission] 已套用 URP 行動版渲染設定：" + AssetPath);
        }
    }
}
#endif
