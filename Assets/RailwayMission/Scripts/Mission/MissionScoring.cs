using UnityEngine;

namespace RailwayMission.Mission
{
    /// <summary>任務結算：時間、停靠、安全、操作四項評分，依總分給 1~3 星。</summary>
    public struct MissionResult
    {
        public bool success;
        public string failReason;
        public float elapsed;
        public float timeLimit;
        public float stopError;      // 正值 = 超過停車標、負值 = 停在標誌前
        public int violations;
        public int emergencyUses;
        public int harshEvents;

        public int timeScore;
        public int stopScore;
        public int safetyScore;
        public int smoothScore;
        public int Total => timeScore + stopScore + safetyScore + smoothScore;
        public int stars;
        public int coins;
    }

    public static class MissionScoring
    {
        public const int MaxTime = 300, MaxStop = 300, MaxSafety = 200, MaxSmooth = 200;

        public static void Evaluate(ref MissionResult r, MissionData data)
        {
            if (!r.success)
            {
                r.stars = 0;
                r.coins = 0;
                return;
            }

            // 準時
            if (r.elapsed <= r.timeLimit) r.timeScore = MaxTime;
            else if (r.elapsed <= r.timeLimit * 1.15f) r.timeScore = MaxTime / 2;
            else r.timeScore = 0;

            // 停靠
            float e = Mathf.Abs(r.stopError);
            if (e <= data.stopPerfectMeters) r.stopScore = MaxStop;
            else if (e <= data.stopGoodMeters) r.stopScore = 220;
            else if (e <= data.stopGoodMeters * 2f) r.stopScore = 120;
            else if (e <= 10f) r.stopScore = 50;
            else r.stopScore = 0;

            // 安全
            r.safetyScore = Mathf.Max(0, MaxSafety - 70 * r.violations - (r.emergencyUses > 0 ? 100 : 0));

            // 平穩操作
            r.smoothScore = Mathf.Max(0, MaxSmooth - 40 * r.harshEvents);

            int total = r.Total;
            r.stars = total >= 850 ? 3 : total >= 600 ? 2 : 1;
            r.coins = total;
        }
    }
}
