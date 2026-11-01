using System;
using System.Collections.Generic;
using UnityEngine;

namespace RailwayMission.Save
{
    [Serializable]
    public class MissionRecord
    {
        public string missionId;
        public int bestStars;
        public int bestScore;
        public float bestTime;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int coins;
        public List<MissionRecord> missions = new List<MissionRecord>();
        public List<string> unlockedTrains = new List<string> { "emu_commuter" };
    }

    /// <summary>本機存檔（PlayerPrefs + JSON）。</summary>
    public static class SaveManager
    {
        const string Key = "rm_save_v1";
        static SaveData _data;

        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static void Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            _data = string.IsNullOrEmpty(json) ? new SaveData() : (JsonUtility.FromJson<SaveData>(json) ?? new SaveData());
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        public static MissionRecord GetRecord(string missionId)
        {
            return Data.missions.Find(m => m.missionId == missionId);
        }

        /// <summary>記錄任務成績，回傳是否刷新最佳紀錄。</summary>
        public static bool RecordMission(string missionId, int stars, int score, float time, int coins)
        {
            Data.coins += coins;
            var rec = GetRecord(missionId);
            bool best = false;
            if (rec == null)
            {
                rec = new MissionRecord { missionId = missionId, bestTime = float.MaxValue };
                Data.missions.Add(rec);
            }
            if (score > rec.bestScore) { rec.bestScore = score; best = true; }
            rec.bestStars = Mathf.Max(rec.bestStars, stars);
            if (time < rec.bestTime) rec.bestTime = time;
            Save();
            return best;
        }
    }
}
