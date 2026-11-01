using System.Collections.Generic;
using RailwayMission.Mission;
using RailwayMission.Track;
using RailwayMission.Train;
using UnityEngine;

namespace RailwayMission.Data
{
    /// <summary>
    /// 第一版內建內容：路線「青嶺線」（原創虛構、台灣山線風格）、EMU 電聯車、任務 01。
    /// 若在 GameManager 指定了 ScriptableObject 資產，會優先使用資產內容。
    /// </summary>
    public static class DefaultContent
    {
        /// <summary>
        /// 青嶺線：楓林站 → 平原直線 → 左彎爬坡 → 溪谷鐵橋 → 青嶺隧道 → 下坡彎道 → 青山站，約 3 公里。
        /// 控制點座標 (x, 軌面高度 y, z)，單位公尺。
        /// </summary>
        public static RouteDefinition CreateQinglingLine()
        {
            var r = new RouteDefinition
            {
                routeName = "青嶺線 Qingling Line",
                gauge = 1.067f,
                controlPoints = new List<Vector3>
                {
                    new Vector3(0, 0, -200),     // 0  起點（楓林站後方）
                    new Vector3(0, 0, 0),        // 1  楓林站停車位置
                    new Vector3(0, 0, 350),      // 2
                    new Vector3(-50, 2, 600),    // 3  左彎
                    new Vector3(-150, 5, 820),   // 4
                    new Vector3(-205, 6, 1000),  // 5  鐵橋起點
                    new Vector3(-225, 6, 1220),  // 6  鐵橋終點
                    new Vector3(-205, 10, 1420), // 7  爬坡
                    new Vector3(-150, 15, 1600), // 8  隧道入口
                    new Vector3(-60, 18, 1800),  // 9
                    new Vector3(40, 18, 1990),   // 10 隧道出口
                    new Vector3(120, 14, 2200),  // 11 下坡
                    new Vector3(162, 8, 2480),   // 12
                    new Vector3(170, 5, 2750),   // 13
                    new Vector3(170, 5, 2950),   // 14 青山站停車位置
                    new Vector3(170, 5, 3250),   // 15
                    new Vector3(170, 5, 3400)    // 16 終點
                },
                landmarks = new List<LandmarkDef>
                {
                    new LandmarkDef { id = "bridge", type = LandmarkType.Bridge, start = new RouteAnchor(5), end = new RouteAnchor(6) },
                    new LandmarkDef { id = "tunnel", type = LandmarkType.Tunnel, start = new RouteAnchor(8), end = new RouteAnchor(10) }
                },
                stations = new List<StationDef>
                {
                    new StationDef { id = "fenglin", nameZh = "楓林站", nameEn = "Fenglin", stopMarker = new RouteAnchor(1), platformLength = 130f, side = 1 },
                    new StationDef { id = "qingshan", nameZh = "青山站", nameEn = "Qingshan", stopMarker = new RouteAnchor(14), platformLength = 130f, side = 1 }
                },
                speedZones = new List<SpeedZoneDef>
                {
                    new SpeedZoneDef { start = new RouteAnchor(0), limitKmh = 60 },
                    new SpeedZoneDef { start = new RouteAnchor(2), limitKmh = 90 },
                    new SpeedZoneDef { start = new RouteAnchor(5, -150), limitKmh = 70 },  // 鐵橋
                    new SpeedZoneDef { start = new RouteAnchor(6, 60), limitKmh = 85 },
                    new SpeedZoneDef { start = new RouteAnchor(8, -80), limitKmh = 70 },   // 隧道
                    new SpeedZoneDef { start = new RouteAnchor(10, 80), limitKmh = 90 },
                    new SpeedZoneDef { start = new RouteAnchor(13, -300), limitKmh = 60 }, // 進站
                    new SpeedZoneDef { start = new RouteAnchor(14, -200), limitKmh = 45 }
                }
            };
            return r;
        }

        public static TrainData CreateEmu() => TrainData.CreateDefaultEmu();

        /// <summary>任務 01：首次列車駕駛。</summary>
        public static MissionData CreateMission01()
        {
            var m = ScriptableObject.CreateInstance<MissionData>();
            m.name = "Mission01_FirstDrive";
            m.missionId = "M01";
            m.missionNumber = 1;
            m.titleZh = "首次列車駕駛";
            m.titleEn = "First Drive";
            m.descriptionZh = "駕駛 EMU 電聯車從楓林站出發，沿青嶺線經過溪谷鐵橋與青嶺隧道，準時抵達青山站，並精準停在月台的「停」字停車位置標。";
            m.descriptionEn = "Drive the EMU from Fenglin along the Qingling Line, over the river bridge and through Qingling Tunnel, then arrive at Qingshan on time and stop exactly at the platform stop marker.";
            m.difficulty = 1;
            m.startStationId = "fenglin";
            m.destinationStationId = "qingshan";
            m.timeLimit = 240f;
            m.objectives = new List<ObjectiveDef>
            {
                new ObjectiveDef { type = ObjectiveType.StartMoving, labelZh = "啟動火車", labelEn = "Start the train" },
                new ObjectiveDef { type = ObjectiveType.ReachSpeed, value = 60f, labelZh = "將速度提升至 60 km/h", labelEn = "Accelerate to 60 km/h" },
                new ObjectiveDef { type = ObjectiveType.ObeySpeedLimits, labelZh = "維持安全速度（不超速）", labelEn = "Keep a safe speed (no overspeed)" },
                new ObjectiveDef { type = ObjectiveType.PassLandmark, landmarkId = "bridge", labelZh = "通過溪谷鐵橋", labelEn = "Cross the river bridge" },
                new ObjectiveDef { type = ObjectiveType.EnterLandmark, landmarkId = "tunnel", labelZh = "進入青嶺隧道", labelEn = "Enter Qingling Tunnel" },
                new ObjectiveDef { type = ObjectiveType.ArriveStation, labelZh = "抵達青山站", labelEn = "Arrive at Qingshan" },
                new ObjectiveDef { type = ObjectiveType.StopAtMarker, labelZh = "精準停靠月台", labelEn = "Stop precisely at the marker" }
            };
            return m;
        }
    }
}
