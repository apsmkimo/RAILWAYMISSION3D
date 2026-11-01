using System;
using System.Collections.Generic;
using UnityEngine;

namespace RailwayMission.Track
{
    public enum LandmarkType { Bridge, Tunnel }

    /// <summary>以「控制點索引 + 偏移距離」定義路線上的位置，讓資料不必寫死 s 值。</summary>
    [Serializable]
    public struct RouteAnchor
    {
        public int point;
        public float offset;

        public RouteAnchor(int point, float offset = 0f)
        {
            this.point = point;
            this.offset = offset;
        }

        public float Resolve(TrackPath path) => path.KnotDistance(point) + offset;
    }

    [Serializable]
    public class LandmarkDef
    {
        public string id;
        public LandmarkType type;
        public RouteAnchor start;
        public RouteAnchor end;
    }

    [Serializable]
    public class StationDef
    {
        public string id;
        public string nameZh;
        public string nameEn;
        /// <summary>停車位置標（列車車頭應停的位置）。</summary>
        public RouteAnchor stopMarker;
        public float platformLength = 130f;
        /// <summary>月台在軌道右側(+1)或左側(-1)。</summary>
        public int side = 1;
    }

    [Serializable]
    public class SpeedZoneDef
    {
        public RouteAnchor start;
        public float limitKmh;
    }

    /// <summary>路線資料定義（可放在 ScriptableObject 中編輯）。</summary>
    [Serializable]
    public class RouteDefinition
    {
        public string routeName = "Route";
        /// <summary>軌距（公尺）。台灣台鐵為 1.067 m 窄軌。</summary>
        public float gauge = 1.067f;
        public List<Vector3> controlPoints = new List<Vector3>();
        public List<LandmarkDef> landmarks = new List<LandmarkDef>();
        public List<StationDef> stations = new List<StationDef>();
        public List<SpeedZoneDef> speedZones = new List<SpeedZoneDef>();
    }

    public struct SpeedZone
    {
        public float startS;
        public float endS;
        public float limitKmh;
    }

    public class Landmark
    {
        public string id;
        public LandmarkType type;
        public float startS;
        public float endS;
        public bool Contains(float s) => s >= startS && s <= endS;
    }

    public class Station
    {
        public string id;
        public string nameZh;
        public string nameEn;
        public float stopS;
        public float platformLength;
        public int side;
        public float PlatformStartS => stopS - platformLength + 8f;
        public float PlatformEndS => stopS + 8f;
    }

    /// <summary>
    /// 執行時期的路線：已建好的 TrackPath，以及解析成 s 值的地標、車站與速限區間。
    /// </summary>
    public class TrackRoute
    {
        public RouteDefinition Definition { get; }
        public TrackPath Path { get; }
        public List<Landmark> Landmarks { get; } = new List<Landmark>();
        public List<Station> Stations { get; } = new List<Station>();
        public List<SpeedZone> SpeedZones { get; } = new List<SpeedZone>();

        public TrackRoute(RouteDefinition def)
        {
            Definition = def;
            Path = new TrackPath(def.controlPoints);

            foreach (var l in def.landmarks)
            {
                Landmarks.Add(new Landmark
                {
                    id = l.id,
                    type = l.type,
                    startS = l.start.Resolve(Path),
                    endS = l.end.Resolve(Path)
                });
            }

            foreach (var st in def.stations)
            {
                Stations.Add(new Station
                {
                    id = st.id,
                    nameZh = st.nameZh,
                    nameEn = st.nameEn,
                    stopS = st.stopMarker.Resolve(Path),
                    platformLength = st.platformLength,
                    side = st.side
                });
            }

            var zones = new List<SpeedZoneDef>(def.speedZones);
            zones.Sort((a, b) => a.start.Resolve(Path).CompareTo(b.start.Resolve(Path)));
            for (int i = 0; i < zones.Count; i++)
            {
                float start = i == 0 ? -1000f : zones[i].start.Resolve(Path);
                float end = i + 1 < zones.Count ? zones[i + 1].start.Resolve(Path) : Path.Length + 1000f;
                SpeedZones.Add(new SpeedZone { startS = start, endS = end, limitKmh = zones[i].limitKmh });
            }
        }

        public Landmark GetLandmark(string id) => Landmarks.Find(l => l.id == id);
        public Station GetStation(string id) => Stations.Find(s => s.id == id);

        public bool IsInside(LandmarkType type, float s, float margin = 0f)
        {
            foreach (var l in Landmarks)
                if (l.type == type && s >= l.startS - margin && s <= l.endS + margin) return true;
            return false;
        }

        /// <summary>目前速限（km/h）。</summary>
        public float SpeedLimitAt(float s)
        {
            foreach (var z in SpeedZones)
                if (s >= z.startS && s < z.endS) return z.limitKmh;
            return SpeedZones.Count > 0 ? SpeedZones[SpeedZones.Count - 1].limitKmh : 80f;
        }

        /// <summary>前方下一個速限變化（只要與目前不同）。找不到回傳 false。</summary>
        public bool NextSpeedChange(float s, float lookahead, out float distance, out float limitKmh)
        {
            float current = SpeedLimitAt(s);
            foreach (var z in SpeedZones)
            {
                if (z.startS > s && z.startS - s <= lookahead && !Mathf.Approximately(z.limitKmh, current))
                {
                    distance = z.startS - s;
                    limitKmh = z.limitKmh;
                    return true;
                }
            }
            distance = 0;
            limitKmh = current;
            return false;
        }

        /// <summary>
        /// 列車整列（車頭 s 至車尾 s）所經過區段中最嚴格的速限。
        /// 寫實規則：車尾離開限速區之前不可加速。
        /// </summary>
        public float EffectiveLimit(float headS, float trainLength)
        {
            float limit = SpeedLimitAt(headS);
            foreach (var z in SpeedZones)
            {
                if (z.endS > headS - trainLength && z.startS < headS) limit = Mathf.Min(limit, z.limitKmh);
            }
            return limit;
        }
    }
}
