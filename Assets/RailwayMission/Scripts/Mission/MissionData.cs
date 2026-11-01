using System;
using System.Collections.Generic;
using UnityEngine;

namespace RailwayMission.Mission
{
    public enum ObjectiveType
    {
        StartMoving,        // 啟動列車
        ReachSpeed,         // 速度達到 value km/h
        ObeySpeedLimits,    // 全程不超速（持續性目標，任務結束時判定）
        EnterLandmark,      // 車頭進入地標（如隧道）
        PassLandmark,       // 車尾通過地標（如橋梁）
        ArriveStation,      // 車頭進入目的地月台
        StopAtMarker        // 停在停車位置標
    }

    [Serializable]
    public class ObjectiveDef
    {
        public ObjectiveType type;
        public string labelZh;
        public string labelEn;
        public float value;
        public string landmarkId;
    }

    /// <summary>
    /// 任務資料（ScriptableObject）。新增任務只需建立新的 MissionData，不必改程式。
    /// Project 視窗右鍵 Create → Railway → Mission。
    /// </summary>
    [CreateAssetMenu(menuName = "Railway/Mission", fileName = "MissionData")]
    public class MissionData : ScriptableObject
    {
        [Header("基本資料")]
        public string missionId = "M01";
        public int missionNumber = 1;
        public string titleZh = "首次列車駕駛";
        public string titleEn = "First Drive";
        [TextArea] public string descriptionZh;
        [TextArea] public string descriptionEn;
        [UnityEngine.Range(1, 3)] public int difficulty = 1;

        [Header("路線")]
        public string startStationId = "fenglin";
        public string destinationStationId = "qingshan";

        [Header("判定")]
        [Tooltip("準時抵達的時間限制（秒）")] public float timeLimit = 240f;
        [Tooltip("完美停車誤差（公尺）")] public float stopPerfectMeters = 1.0f;
        [Tooltip("良好停車誤差（公尺）")] public float stopGoodMeters = 3.0f;
        [Tooltip("可接受的停車範圍：停在標誌前多少公尺內")] public float stopShortAllowance = 25f;
        [Tooltip("超過停車位置多少公尺判定衝過月台（任務失敗）")] public float overrunFail = 15f;
        [Tooltip("容許超速（km/h）")] public float overspeedGrace = 3f;
        [Tooltip("嚴重超速（km/h）：超過速限此值即任務失敗")] public float severeOverspeed = 25f;

        [Header("目標")]
        public List<ObjectiveDef> objectives = new List<ObjectiveDef>();

        public string Title => RailwayMission.Core.Loc.T(titleZh, titleEn);
        public string Description => RailwayMission.Core.Loc.T(descriptionZh, descriptionEn);
    }
}
