using System.Collections.Generic;
using UnityEngine;

namespace RailwayMission.Track
{
    /// <summary>
    /// 鐵軌路徑（Spline）。以向心 Catmull-Rom 曲線通過所有控制點，
    /// 再以固定間距重新取樣，建立「累積距離 s → 位置／方向」查表。
    /// 火車的位置完全由 s 決定：P = Spline(s)。
    /// </summary>
    public class TrackPath
    {
        public const float SampleSpacing = 1.0f;

        readonly List<Vector3> _points = new List<Vector3>();
        readonly List<Vector3> _tangents = new List<Vector3>();
        readonly float[] _knotDistances;

        public float Length { get; private set; }
        public int SampleCount => _points.Count;

        /// <summary>每個控制點在路徑上的累積距離（公尺）。</summary>
        public float KnotDistance(int index) => _knotDistances[Mathf.Clamp(index, 0, _knotDistances.Length - 1)];

        public TrackPath(IList<Vector3> controlPoints)
        {
            int n = controlPoints.Count;
            _knotDistances = new float[n];

            // 1) 先以細密參數取樣曲線
            var dense = new List<Vector3>();
            var denseKnot = new List<int>(); // dense index of each knot
            const int stepsPerSegment = 64;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[i + 1];
                Vector3 p3 = controlPoints[Mathf.Min(i + 2, n - 1)];
                if (i == 0) p0 = p1 - (p2 - p1);
                if (i + 2 > n - 1) p3 = p2 + (p2 - p1);
                denseKnot.Add(dense.Count);
                for (int s = 0; s < stepsPerSegment; s++)
                {
                    dense.Add(CentripetalCatmullRom(p0, p1, p2, p3, s / (float)stepsPerSegment));
                }
            }
            denseKnot.Add(dense.Count);
            dense.Add(controlPoints[n - 1]);

            // 2) 計算累積弧長
            var cum = new float[dense.Count];
            for (int i = 1; i < dense.Count; i++) cum[i] = cum[i - 1] + Vector3.Distance(dense[i - 1], dense[i]);
            Length = cum[cum.Length - 1];
            for (int k = 0; k < n; k++) _knotDistances[k] = cum[denseKnot[k]];

            // 3) 以固定間距重新取樣
            int count = Mathf.FloorToInt(Length / SampleSpacing) + 1;
            int j = 0;
            for (int i = 0; i < count; i++)
            {
                float s = i * SampleSpacing;
                while (j < cum.Length - 2 && cum[j + 1] < s) j++;
                float seg = cum[j + 1] - cum[j];
                float t = seg > 1e-5f ? (s - cum[j]) / seg : 0f;
                _points.Add(Vector3.Lerp(dense[j], dense[j + 1], t));
            }
            for (int i = 0; i < _points.Count; i++)
            {
                Vector3 a = _points[Mathf.Max(i - 1, 0)];
                Vector3 b = _points[Mathf.Min(i + 1, _points.Count - 1)];
                _tangents.Add((b - a).normalized);
            }
        }

        static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t0 = 0f;
            float t1 = t0 + Mathf.Pow(Vector3.Distance(p0, p1), 0.5f);
            float t2 = t1 + Mathf.Pow(Vector3.Distance(p1, p2), 0.5f);
            float t3 = t2 + Mathf.Pow(Vector3.Distance(p2, p3), 0.5f);
            if (t1 - t0 < 1e-4f) t1 = t0 + 1e-4f;
            if (t2 - t1 < 1e-4f) t2 = t1 + 1e-4f;
            if (t3 - t2 < 1e-4f) t3 = t2 + 1e-4f;
            float tt = Mathf.Lerp(t1, t2, t);
            Vector3 a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3;
            return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2;
        }

        /// <summary>取得距離 s 處的位置（軌面中心）。</summary>
        public Vector3 PositionAt(float s)
        {
            Locate(s, out int i, out float t);
            return Vector3.LerpUnclamped(_points[i], _points[i + 1], t) + ExtrapolationOffset(s);
        }

        /// <summary>取得距離 s 處的前進方向（單位向量）。</summary>
        public Vector3 TangentAt(float s)
        {
            Locate(s, out int i, out float t);
            return Vector3.Slerp(_tangents[i], _tangents[i + 1], Mathf.Clamp01(t)).normalized;
        }

        /// <summary>水平右方向（軌道橫向）。</summary>
        public Vector3 RightAt(float s)
        {
            Vector3 f = TangentAt(s);
            Vector3 r = Vector3.Cross(Vector3.up, f);
            if (r.sqrMagnitude < 1e-6f) r = Vector3.right;
            return r.normalized;
        }

        /// <summary>坡度（sin θ），正值為上坡。</summary>
        public float GradeAt(float s) => TangentAt(s).y;

        /// <summary>曲率半徑（公尺），直線回傳極大值。</summary>
        public float CurveRadiusAt(float s)
        {
            Vector3 a = TangentAt(s - 10f);
            Vector3 b = TangentAt(s + 10f);
            a.y = 0; b.y = 0;
            float angle = Vector3.Angle(a, b) * Mathf.Deg2Rad;
            if (angle < 1e-4f) return 100000f;
            return 20f / angle;
        }

        /// <summary>找出最接近某世界座標的路徑距離 s（粗略搜尋）。</summary>
        public float ClosestDistance(Vector3 world, float step = 2f)
        {
            float best = 0f;
            float bestD = float.MaxValue;
            for (float s = 0; s <= Length; s += step)
            {
                float d = (PositionAt(s) - world).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        void Locate(float s, out int i, out float t)
        {
            float f = s / SampleSpacing;
            i = Mathf.Clamp(Mathf.FloorToInt(f), 0, _points.Count - 2);
            t = Mathf.Clamp01(f - i);
        }

        /// <summary>超出路徑兩端時，沿端點方向直線延伸（避免列車卡住）。</summary>
        Vector3 ExtrapolationOffset(float s)
        {
            if (s < 0f) return _tangents[0] * s;
            float over = s - (_points.Count - 1) * SampleSpacing;
            if (over > 0f) return _tangents[_tangents.Count - 1] * over;
            return Vector3.zero;
        }
    }
}
