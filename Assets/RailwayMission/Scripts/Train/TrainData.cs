using UnityEngine;

namespace RailwayMission.Train
{
    /// <summary>
    /// 火車規格資料（ScriptableObject）。新增車種只要建立新的 TrainData 資產即可，
    /// 在 Project 視窗按右鍵 Create → Railway → Train Data。
    /// </summary>
    [CreateAssetMenu(menuName = "Railway/Train Data", fileName = "TrainData")]
    public class TrainData : ScriptableObject
    {
        [Header("基本資料")]
        public string trainId = "emu_commuter";
        public string displayNameZh = "EMU 通勤電聯車";
        public string displayNameEn = "EMU Commuter";

        [Header("編組")]
        [Min(1)] public int carCount = 4;
        [Tooltip("單節車廂長度（公尺）")] public float carLength = 20f;
        [Tooltip("車廂間距（公尺）")] public float carGap = 0.8f;
        [Tooltip("車體寬度（公尺）")] public float carWidth = 2.9f;
        [Tooltip("車體高度：軌面至車頂（公尺）")] public float carHeight = 3.65f;
        [Tooltip("轉向架中心距（公尺）")] public float bogieSpacing = 13.8f;

        [Header("重量")]
        [Tooltip("每節車廂空車重（公噸）")] public float tonnesPerCar = 38f;
        [Tooltip("乘客載重（公噸）")] public float passengerTonnes = 12f;

        [Header("牽引")]
        [Tooltip("最大牽引力（kN）")] public float maxTractionKN = 165f;
        [Tooltip("最大功率（kW）")] public float maxPowerKW = 2200f;
        [Tooltip("最高速度（km/h）")] public float maxSpeedKmh = 120f;
        [Tooltip("牽引力上升速率（每秒 0~1）：模擬馬達電流爬升")] public float tractionResponse = 0.6f;

        [Header("煞車")]
        [Tooltip("常用煞車最大減速度（m/s²）")] public float serviceBrakeDecel = 1.0f;
        [Tooltip("緊急煞車減速度（m/s²）")] public float emergencyBrakeDecel = 1.3f;
        [Tooltip("煞車缸壓力上升速率（每秒 0~1）")] public float brakeApplyRate = 0.9f;
        [Tooltip("煞車緩解速率（每秒 0~1）")] public float brakeReleaseRate = 0.6f;

        [Header("行駛阻力 Davis 公式 R = A + B·v + C·v²（kN，v 為 m/s）")]
        public float davisA = 2.0f;
        public float davisB = 0.035f;
        public float davisC = 0.0065f;

        public float TotalMassKg => (tonnesPerCar * carCount + passengerTonnes) * 1000f;
        public float TrainLength => carCount * carLength + (carCount - 1) * carGap;

        public static TrainData CreateDefaultEmu()
        {
            var d = CreateInstance<TrainData>();
            d.name = "EMU_Commuter";
            return d;
        }
    }
}
