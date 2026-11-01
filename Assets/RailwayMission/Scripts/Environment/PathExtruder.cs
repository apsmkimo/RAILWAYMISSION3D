using RailwayMission.Core;
using RailwayMission.Track;
using UnityEngine;

namespace RailwayMission.Environment
{
    /// <summary>
    /// 沿著鐵軌路徑擠出斷面（道碴、鋼軌、月台、隧道、橋面…）。
    /// 斷面點以 (橫向 x, 垂直 y) 表示，相對於軌面中心；
    /// 從後方往前看（沿 +s）時，斷面需以「順時針」排列，可見面在行進方向的左手側。
    /// </summary>
    public static class PathExtruder
    {
        public static void Extrude(MeshBuilder mb, int submesh, TrackPath path, float s0, float s1, float step, Vector2[] profile, float uvScale = 4f)
        {
            if (s1 <= s0) return;
            int segs = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / step));
            float ds = (s1 - s0) / segs;
            for (int i = 0; i < segs; i++)
            {
                float sa = s0 + i * ds;
                float sb = sa + ds;
                Vector3 pa = path.PositionAt(sa), pb = path.PositionAt(sb);
                Vector3 ra = path.RightAt(sa), rb = path.RightAt(sb);
                for (int k = 0; k < profile.Length - 1; k++)
                {
                    Vector3 a = pa + ra * profile[k].x + Vector3.up * profile[k].y;
                    Vector3 b = pb + rb * profile[k].x + Vector3.up * profile[k].y;
                    Vector3 c = pb + rb * profile[k + 1].x + Vector3.up * profile[k + 1].y;
                    Vector3 d = pa + ra * profile[k + 1].x + Vector3.up * profile[k + 1].y;
                    mb.AddQuad(submesh, a, b, c, d);
                }
            }
        }

        /// <summary>沿路徑放置一個有方向的長方體（local：x 橫向、y 垂直、z 沿路徑）。</summary>
        public static void BoxAt(MeshBuilder mb, int submesh, TrackPath path, float s, Vector3 local, Vector3 size)
        {
            Vector3 f = path.TangentAt(s);
            Vector3 flat = new Vector3(f.x, 0, f.z).normalized;
            Quaternion rot = Quaternion.LookRotation(flat, Vector3.up);
            Vector3 center = path.PositionAt(s) + path.RightAt(s) * local.x + Vector3.up * local.y + flat * local.z;
            mb.AddBox(submesh, center, rot, size);
        }

        public static Vector3 PointAt(TrackPath path, float s, float lateral, float height)
        {
            return path.PositionAt(s) + path.RightAt(s) * lateral + Vector3.up * height;
        }

        public static Quaternion RotationAt(TrackPath path, float s)
        {
            Vector3 f = path.TangentAt(s);
            return Quaternion.LookRotation(new Vector3(f.x, 0, f.z).normalized, Vector3.up);
        }
    }
}
