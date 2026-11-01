using RailwayMission.Audio;
using RailwayMission.Cameras;
using RailwayMission.Data;
using RailwayMission.Environment;
using RailwayMission.Mission;
using RailwayMission.Track;
using RailwayMission.Train;
using RailwayMission.UI;
using UnityEngine;

namespace RailwayMission.Core
{
    /// <summary>
    /// 遊戲總控制：建立路線與場景、火車、任務、攝影機、音效與介面，並處理重新開始。
    /// 在 Inspector 指定 ScriptableObject 資產即可替換路線／火車／任務；留空則使用內建的第一版內容。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("資料（留空使用內建內容）")]
        public RouteAsset routeAsset;
        public TrainData trainData;
        public MissionData missionData;

        [Header("畫面")]
        [Tooltip("目標 FPS（Android 建議 30~60）")] public int targetFrameRate = 60;
        public float shadowDistance = 160f;

        public TrackRoute Route { get; private set; }
        public TrainController Train { get; private set; }
        public MissionManager Mission { get; private set; }
        public TrainCameraController CameraRig { get; private set; }
        public DrivingHUD Hud { get; private set; }

        Font _font;

        void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            QualitySettings.shadowDistance = shadowDistance;
        }

        void Start()
        {
            _font = CreateFont();

            var routeDef = routeAsset != null ? routeAsset.route : DefaultContent.CreateQinglingLine();
            var train = trainData != null ? trainData : DefaultContent.CreateEmu();
            var mission = missionData != null ? missionData : DefaultContent.CreateMission01();

            // 路線與場景
            Route = new TrackRoute(routeDef);
            var world = new GameObject("World").transform;
            new WorldBuilder(Route, world).Build(_font);
            SetupLighting();

            // 火車
            var origin = Route.GetStation(mission.startStationId);
            Train = new GameObject("Train").AddComponent<TrainController>();
            Train.Build(Route, train, origin != null ? origin.stopS : 50f + train.TrainLength);

            // 任務
            Mission = gameObject.AddComponent<MissionManager>();
            Mission.Init(mission, Route, Train);

            // 攝影機
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            if (cam.GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
            CameraRig = cam.gameObject.AddComponent<TrainCameraController>();
            CameraRig.Init(cam, Train, Route);
            if (RenderSettings.skybox == null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.62f, 0.75f, 0.88f);
            }

            // 音效
            new GameObject("TrainAudio").AddComponent<TrainAudio>().Init(Train);

            // 介面
            Hud = gameObject.AddComponent<DrivingHUD>();
            Hud.Init(Train, Mission, CameraRig, Route, _font, Restart);
        }

        public void Restart()
        {
            Hud.ResetUI();
            Mission.ResetMission();
            CameraRig.SetMode(CameraRig.Mode);
        }

        static Font CreateFont()
        {
            // 優先使用系統內建的繁體中文字型；Unity 動態字型也會自動向系統字型借用缺少的字元
            string[] names =
            {
                "Noto Sans CJK TC", "Noto Sans TC", "NotoSansCJK-Regular", "Microsoft JhengHei", "Microsoft JhengHei UI",
                "PingFang TC", "Heiti TC", "Noto Sans CJK SC", "Droid Sans Fallback", "Roboto", "Arial"
            };
            var font = Font.CreateDynamicFontFromOSFont(names, 32);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }

        void SetupLighting()
        {
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.18f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 1500f;
            RenderSettings.fogColor = new Color(0.66f, 0.74f, 0.82f);
        }
    }

    /// <summary>
    /// 自動啟動：在任何沒有 GameManager 的場景按下 Play 即可直接開始 Demo。
    /// </summary>
    public static class GameBootstrap
    {
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Enabled) return;
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("RailwayMission").AddComponent<GameManager>();
        }
    }
}
