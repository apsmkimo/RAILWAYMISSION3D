using System.Collections.Generic;
using RailwayMission.Core;
using RailwayMission.Track;
using UnityEngine;

namespace RailwayMission.Environment
{
    /// <summary>
    /// 依照路線資料程序化產生整個場景：鐵軌、地形、橋梁、隧道、車站、電車線、樹林、號誌牌、河流與光照。
    /// 第一版全部以程式產生，因此不需要任何外部美術資源就能直接執行；
    /// 之後可逐步用 Blender 模型替換各個部分。
    /// </summary>
    public class WorldBuilder
    {
        // ───── 可調參數 ─────
        const float ChunkLength = 200f;     // 鐵軌網格分段長度（利於視錐剔除）
        const float SleeperSpacing = 0.7f;
        const float TerrainCell = 12.5f;
        const float TerrainMargin = 700f;
        const int TerrainChunkCells = 32;
        const float ValleyDepth = 22f;
        const float MountainHeight = 70f;
        const float CatenaryHeight = 5.4f;
        const float TunnelHoleHalfWidth = 16f;

        readonly TrackRoute _route;
        readonly TrackPath _path;
        readonly Transform _root;
        readonly float _gauge;
        Font _font;

        // 地形計算用的粗取樣
        Vector3[] _coarse;
        const float CoarseStep = 10f;

        // 河谷
        bool _hasRiver;
        Vector2 _riverCenter, _riverAcross, _riverAlong;
        float _riverHalfWidth, _waterLevel;

        // 山（隧道）
        readonly List<(Vector2 a, Vector2 b, float baseY)> _mountains = new List<(Vector2, Vector2, float)>();

        public WorldBuilder(TrackRoute route, Transform root)
        {
            _route = route;
            _path = route.Path;
            _root = root;
            _gauge = route.Definition.gauge;
        }

        public Font Font => _font;

        public void Build(Font font)
        {
            _font = font;
            PrepareTerrainFeatures();
            BuildTrack();
            BuildBridges();
            BuildTunnels();
            BuildStations();
            BuildCatenary();
            BuildSpeedSigns();
            BuildTerrain();
            BuildWater();
            BuildTrees();
        }

        Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            return go.transform;
        }

        // ═════════════════════════════ 鐵軌 ═════════════════════════════

        void BuildTrack()
        {
            var parent = Group("Track");
            float railOffset = _gauge * 0.5f + 0.035f;
            var ballastProfile = new[] { new Vector2(-2.4f, -0.75f), new Vector2(-1.55f, -0.30f), new Vector2(1.55f, -0.30f), new Vector2(2.4f, -0.75f) };
            Vector2[] RailProfile(float c) => new[]
            {
                new Vector2(c - 0.035f, -0.16f), new Vector2(c - 0.035f, 0f), new Vector2(c + 0.035f, 0f), new Vector2(c + 0.035f, -0.16f)
            };

            float start = 0f, end = _path.Length;
            for (float c0 = start; c0 < end; c0 += ChunkLength)
            {
                float c1 = Mathf.Min(c0 + ChunkLength, end);
                var mb = new MeshBuilder(3); // 0 道碴、1 枕木、2 鋼軌

                // 道碴（橋上不鋪）
                foreach (var (a, b) in SubtractRanges(c0, c1, LandmarkType.Bridge))
                    PathExtruder.Extrude(mb, 0, _path, a, b, 4f, ballastProfile);

                // 枕木
                float first = Mathf.Ceil(c0 / SleeperSpacing) * SleeperSpacing;
                for (float s = first; s < c1; s += SleeperSpacing)
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(0, -0.235f, 0), new Vector3(2.0f, 0.13f, 0.22f));

                // 鋼軌
                PathExtruder.Extrude(mb, 2, _path, c0, c1, 2f, RailProfile(-railOffset));
                PathExtruder.Extrude(mb, 2, _path, c0, c1, 2f, RailProfile(railOffset));

                mb.ToGameObject($"Track_{(int)c0:0000}", parent,
                    new[] { MaterialLibrary.Ballast, MaterialLibrary.Sleeper, MaterialLibrary.Rail }, false);
            }
        }

        /// <summary>從 [s0,s1] 中扣除某類地標的範圍，回傳剩下的區段。</summary>
        IEnumerable<(float, float)> SubtractRanges(float s0, float s1, LandmarkType type)
        {
            var cuts = new List<(float, float)>();
            foreach (var l in _route.Landmarks)
                if (l.type == type) cuts.Add((l.startS, l.endS));
            cuts.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            float cur = s0;
            foreach (var (a, b) in cuts)
            {
                if (b <= cur || a >= s1) continue;
                if (a > cur) yield return (cur, Mathf.Min(a, s1));
                cur = Mathf.Max(cur, b);
            }
            if (cur < s1) yield return (cur, s1);
        }

        // ═════════════════════════════ 橋梁 ═════════════════════════════

        void BuildBridges()
        {
            var parent = Group("Bridges");
            foreach (var l in _route.Landmarks)
            {
                if (l.type != LandmarkType.Bridge) continue;
                var mb = new MeshBuilder(2); // 0 鋼構、1 混凝土

                // 鋼板梁橋面
                var deck = new[]
                {
                    new Vector2(-2.3f, -2.0f), new Vector2(-2.3f, -0.30f), new Vector2(2.3f, -0.30f),
                    new Vector2(2.3f, -2.0f), new Vector2(-2.3f, -2.0f)
                };
                PathExtruder.Extrude(mb, 0, _path, l.startS - 2f, l.endS + 2f, 4f, deck);

                // 護欄
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 2.2f;
                    var rail = new[] { new Vector2(x - 0.05f, 0.9f), new Vector2(x - 0.05f, 1.0f), new Vector2(x + 0.05f, 1.0f), new Vector2(x + 0.05f, 0.9f) };
                    PathExtruder.Extrude(mb, 0, _path, l.startS, l.endS, 4f, rail);
                    for (float s = l.startS; s <= l.endS; s += 3f)
                        PathExtruder.BoxAt(mb, 0, _path, s, new Vector3(x, 0.35f, 0), new Vector3(0.08f, 1.3f, 0.08f));
                }

                // 橋墩
                float length = l.endS - l.startS;
                int spans = Mathf.Max(2, Mathf.RoundToInt(length / 32f));
                for (int i = 1; i < spans; i++)
                {
                    float s = l.startS + length * i / spans;
                    Vector3 top = PathExtruder.PointAt(_path, s, 0, -2.0f);
                    float ground = TerrainHeight(top.x, top.z) - 3f;
                    float h = top.y - ground;
                    if (h < 1f) continue;
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(0, -2.0f - h * 0.5f, 0), new Vector3(3.6f, h, 2.4f));
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(0, -2.3f, 0), new Vector3(5.0f, 0.6f, 3.0f));
                }
                // 橋台
                foreach (float s in new[] { l.startS, l.endS })
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(0, -5f, 0), new Vector3(7f, 6f, 4f));

                mb.ToGameObject("Bridge_" + l.id, parent, new[] { MaterialLibrary.Steel, MaterialLibrary.Concrete });
            }
        }

        // ═════════════════════════════ 隧道 ═════════════════════════════

        void BuildTunnels()
        {
            var parent = Group("Tunnels");
            // 由內側看到的斷面（逆時針 = 可見面朝向中心）
            var tube = new[]
            {
                new Vector2(4.2f, -0.8f), new Vector2(4.5f, 2.6f), new Vector2(3.7f, 5.3f), new Vector2(1.7f, 6.8f),
                new Vector2(-1.7f, 6.8f), new Vector2(-3.7f, 5.3f), new Vector2(-4.5f, 2.6f), new Vector2(-4.2f, -0.8f),
                new Vector2(4.2f, -0.8f)
            };
            var walkway = new[] { new Vector2(-4.3f, -0.1f), new Vector2(-4.3f, 0.2f), new Vector2(-3.0f, 0.2f), new Vector2(-3.0f, -0.8f) };

            foreach (var l in _route.Landmarks)
            {
                if (l.type != LandmarkType.Tunnel) continue;
                var mb = new MeshBuilder(3); // 0 襯砌、1 洞門、2 燈
                var cover = new MeshBuilder(1);
                PathExtruder.Extrude(mb, 0, _path, l.startS, l.endS, 4f, tube);
                PathExtruder.Extrude(mb, 0, _path, l.startS, l.endS, 4f, walkway);

                // 隧道燈（每 40 公尺）
                for (float s = l.startS + 20f; s < l.endS; s += 40f)
                    PathExtruder.BoxAt(mb, 2, _path, s, new Vector3(4.15f, 3.6f, 0), new Vector3(0.12f, 0.25f, 0.9f));

                // 覆土帶：遮住地形在隧道上方挖空的區域
                const float coverHalf = TunnelHoleHalfWidth + 14f;
                float coverStep = 5f;
                for (float s = l.startS; s < l.endS; s += coverStep)
                {
                    float sb = Mathf.Min(s + coverStep, l.endS);
                    Vector3 la = CoverPoint(s, -coverHalf), ra = CoverPoint(s, coverHalf);
                    Vector3 lb = CoverPoint(sb, -coverHalf), rb = CoverPoint(sb, coverHalf);
                    cover.AddQuad(0, la, lb, rb, ra);
                }

                // 洞門（入口與出口），中間留出 9 m × 7.3 m 開口；高度延伸到覆土帶
                foreach (var (s, dir) in new[] { (l.startS, -1f), (l.endS, 1f) })
                {
                    float trackY = _path.PositionAt(s).y;
                    float top = Mathf.Max(16f, Mathf.Max(CoverPoint(s, -coverHalf).y, CoverPoint(s, coverHalf).y) - trackY + 1.5f);
                    float z = dir * 1.0f;
                    float wallH = top + 1.0f;
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(-(4.5f + 15f), wallH * 0.5f - 1.0f, z), new Vector3(30f, wallH, 2f));
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(4.5f + 15f, wallH * 0.5f - 1.0f, z), new Vector3(30f, wallH, 2f));
                    float upperH = top - 7.3f;
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(0, 7.3f + upperH * 0.5f, z), new Vector3(9.1f, upperH, 2f));
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(0, top + 0.6f, z * 1.4f), new Vector3(62f, 1.2f, 2.8f)); // 帽石
                    // 洞口前的地坪（遮住地形挖空的邊緣）
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(-18.4f, -1.75f, dir * 9f), new Vector3(32f, 2f, 16f));
                    PathExtruder.BoxAt(mb, 1, _path, s, new Vector3(18.4f, -1.75f, dir * 9f), new Vector3(32f, 2f, 16f));
                    // 隧道名稱牌（面向進洞列車）
                    var signPos = PathExtruder.PointAt(_path, s + dir * 2.05f, 0, 9.0f);
                    var rot = PathExtruder.RotationAt(_path, s) * (dir > 0 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity);
                    CreateText("TunnelName", parent, l.id == "tunnel" ? "青嶺隧道" : l.id, signPos, rot, 1.4f, new Color(0.15f, 0.15f, 0.15f));
                }

                mb.ToGameObject("Tunnel_" + l.id, parent,
                    new[] { MaterialLibrary.DarkConcrete, MaterialLibrary.Concrete, MaterialLibrary.TunnelLamp });
                cover.ToGameObject("TunnelCover_" + l.id, parent, new[] { MaterialLibrary.Grass }, false);
            }
        }

        /// <summary>隧道覆土帶的頂點：取挖空範圍外側的地形高度。</summary>
        Vector3 CoverPoint(float s, float lateral)
        {
            Vector3 p = PathExtruder.PointAt(_path, s, lateral, 0f);
            Vector3 outer = PathExtruder.PointAt(_path, s, lateral + Mathf.Sign(lateral) * 6f, 0f);
            p.y = Mathf.Max(TerrainHeight(outer.x, outer.z), _path.PositionAt(s).y + 8.5f) - 0.3f;
            return p;
        }

        // ═════════════════════════════ 車站 ═════════════════════════════

        void BuildStations()
        {
            var parent = Group("Stations");
            foreach (var st in _route.Stations)
            {
                int side = st.side >= 0 ? 1 : -1;
                var mb = new MeshBuilder(5); // 0 月台、1 黃線、2 屋頂、3 鋼構、4 建築
                float s0 = st.PlatformStartS, s1 = st.PlatformEndS;
                const float edge = 1.6f, width = 7f, top = 1.0f;

                Vector2[] platform = side > 0
                    ? new[] { new Vector2(edge, -0.75f), new Vector2(edge, top), new Vector2(edge + width, top), new Vector2(edge + width, -0.75f) }
                    : new[] { new Vector2(-edge - width, -0.75f), new Vector2(-edge - width, top), new Vector2(-edge, top), new Vector2(-edge, -0.75f) };
                PathExtruder.Extrude(mb, 0, _path, s0, s1, 4f, platform);
                // 月台端面
                PathExtruder.BoxAt(mb, 0, _path, s0, new Vector3(side * (edge + width * 0.5f), (top - 0.75f) * 0.5f, 0.05f), new Vector3(width, top + 0.75f, 0.1f));
                PathExtruder.BoxAt(mb, 0, _path, s1, new Vector3(side * (edge + width * 0.5f), (top - 0.75f) * 0.5f, -0.05f), new Vector3(width, top + 0.75f, 0.1f));

                // 黃色警戒線
                Vector2[] yellow = side > 0
                    ? new[] { new Vector2(edge + 0.35f, top + 0.01f), new Vector2(edge + 0.6f, top + 0.01f) }
                    : new[] { new Vector2(-edge - 0.6f, top + 0.01f), new Vector2(-edge - 0.35f, top + 0.01f) };
                PathExtruder.Extrude(mb, 1, _path, s0, s1, 4f, yellow);

                // 雨棚（月台中段 70 公尺）
                float mid = st.stopS - st.platformLength * 0.45f;
                float roofX = side * (edge + width * 0.55f);
                for (float s = mid - 35f; s <= mid + 35f; s += 10f)
                {
                    PathExtruder.BoxAt(mb, 3, _path, s, new Vector3(roofX, top + 1.6f, 0), new Vector3(0.2f, 3.2f, 0.2f));
                    PathExtruder.BoxAt(mb, 2, _path, s, new Vector3(roofX, top + 3.3f, 0), new Vector3(width - 0.6f, 0.15f, 10.2f));
                    if (s < mid + 30f)
                        PathExtruder.BoxAt(mb, 3, _path, s + 5f, new Vector3(roofX + side * 1.2f, top + 0.25f, 0), new Vector3(0.5f, 0.45f, 2.0f)); // 座椅
                }

                // 站房
                PathExtruder.BoxAt(mb, 4, _path, mid, new Vector3(side * (edge + width + 7f), 3.5f, 0), new Vector3(12f, 8.5f, 26f));
                PathExtruder.BoxAt(mb, 2, _path, mid, new Vector3(side * (edge + width + 7f), 8.0f, 0), new Vector3(13.5f, 0.6f, 27.5f));

                // 停車位置標
                PathExtruder.BoxAt(mb, 3, _path, st.stopS, new Vector3(side * (edge + 0.9f), top + 1.1f, 0), new Vector3(0.1f, 2.2f, 0.1f));

                mb.ToGameObject("Station_" + st.id, parent, new[]
                {
                    MaterialLibrary.Concrete, MaterialLibrary.Yellow, MaterialLibrary.Roof, MaterialLibrary.Steel,
                    MaterialLibrary.Get("station_building", new Color(0.82f, 0.78f, 0.70f), 0.15f)
                });

                // 停車位置標牌面（朝向進站列車）
                var markerPos = PathExtruder.PointAt(_path, st.stopS + 0.08f, side * (edge + 0.9f), top + 2.0f);
                var markerRot = PathExtruder.RotationAt(_path, st.stopS) * Quaternion.Euler(0, 180, 0);
                CreateBoard("StopMarker", parent, markerPos, markerRot, new Vector2(0.6f, 0.6f), MaterialLibrary.Yellow);
                var facing = PathExtruder.RotationAt(_path, st.stopS);
                CreateText("StopMarkerText", parent, "停", markerPos + facing * Vector3.back * 0.035f, facing, 0.45f, Color.black);

                // 站名牌（兩面）
                for (int k = -1; k <= 1; k += 2)
                {
                    float s = mid + k * 28f;
                    var pos = PathExtruder.PointAt(_path, s, side * (edge + 2.4f), top + 2.6f);
                    var rot = PathExtruder.RotationAt(_path, s) * Quaternion.Euler(0, side * 90f, 0);
                    CreateBoard("StationSign", parent, pos, rot, new Vector2(3.4f, 1.3f), MaterialLibrary.White);
                    CreateText("StationName", parent, st.nameZh + "\n" + st.nameEn, pos + rot * Vector3.back * 0.03f, rot, 0.5f, new Color(0.05f, 0.2f, 0.45f));
                }
            }
        }

        // ═════════════════════════════ 電車線 ═════════════════════════════

        void BuildCatenary()
        {
            var parent = Group("Catenary");
            for (float c0 = 0f; c0 < _path.Length; c0 += ChunkLength * 2f)
            {
                float c1 = Mathf.Min(c0 + ChunkLength * 2f, _path.Length);
                var mb = new MeshBuilder(2); // 0 桿、1 線
                var contact = new[] { new Vector2(-0.012f, CatenaryHeight + 0.012f), new Vector2(0.012f, CatenaryHeight + 0.012f), new Vector2(0.012f, CatenaryHeight - 0.012f), new Vector2(-0.012f, CatenaryHeight - 0.012f), new Vector2(-0.012f, CatenaryHeight + 0.012f) };
                PathExtruder.Extrude(mb, 1, _path, c0, c1, 5f, contact);

                float first = Mathf.Ceil(c0 / 50f) * 50f;
                for (float s = first; s < c1; s += 50f)
                {
                    bool inTunnel = _route.IsInside(LandmarkType.Tunnel, s, 2f);
                    bool onBridge = _route.IsInside(LandmarkType.Bridge, s, 0f);
                    bool nearStation = false;
                    foreach (var st in _route.Stations)
                        if (s > st.PlatformStartS - 5f && s < st.PlatformEndS + 5f && st.side < 0) nearStation = true;
                    if (inTunnel) continue;
                    float x = onBridge ? -2.6f : (nearStation ? 3.4f : -3.4f);
                    float baseY = onBridge ? -0.3f : -0.7f;
                    float sign = Mathf.Sign(x);
                    Vector3 pBase = PathExtruder.PointAt(_path, s, x, baseY);
                    mb.AddCylinder(0, pBase, 0.16f, 7.4f - baseY, 6, false);
                    // 懸臂
                    Vector3 armA = PathExtruder.PointAt(_path, s, x, 6.6f);
                    Vector3 armB = PathExtruder.PointAt(_path, s, -sign * 0.3f, 6.6f);
                    mb.AddBeam(0, armA, armB, 0.08f, 0.08f, Vector3.up);
                    Vector3 brA = PathExtruder.PointAt(_path, s, x, 5.6f);
                    mb.AddBeam(0, brA, armB + Vector3.down * 1.1f, 0.06f, 0.06f, Vector3.up);
                    mb.AddBeam(0, armB, armB + Vector3.down * 1.2f, 0.04f, 0.04f, Vector3.up);
                }
                mb.ToGameObject($"Catenary_{(int)c0:0000}", parent, new[] { MaterialLibrary.Steel, MaterialLibrary.TrainUnder }, false);
            }
        }

        // ═════════════════════════════ 速限號誌牌 ═════════════════════════════

        void BuildSpeedSigns()
        {
            var parent = Group("SpeedSigns");
            for (int i = 1; i < _route.SpeedZones.Count; i++)
            {
                var z = _route.SpeedZones[i];
                float s = z.startS;
                if (s < 0 || s > _path.Length) continue;
                float x = _route.IsInside(LandmarkType.Bridge, s) ? 2.4f : (_route.IsInside(LandmarkType.Tunnel, s) ? 3.9f : 3.0f);
                var basePos = PathExtruder.PointAt(_path, s, x, -0.7f);
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(post.GetComponent<Collider>());
                post.name = "SignPost";
                post.transform.SetParent(parent, false);
                post.transform.position = basePos + Vector3.up * 1.6f;
                post.transform.localScale = new Vector3(0.1f, 1.6f, 0.1f);
                post.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Steel;

                var rot = PathExtruder.RotationAt(_path, s); // forward = 列車行進方向（看板面向來車）
                var boardPos = basePos + Vector3.up * 3.0f;
                CreateBoard("SpeedBoard", parent, boardPos, rot, new Vector2(0.9f, 0.9f), MaterialLibrary.White);
                CreateBoard("SpeedBoardRim", parent, boardPos + rot * Vector3.forward * 0.02f, rot, new Vector2(1.0f, 1.0f), MaterialLibrary.Red);
                CreateText("SpeedText", parent, Mathf.RoundToInt(z.limitKmh).ToString(), boardPos + rot * Vector3.back * 0.035f, rot, 0.5f, Color.black);
            }
        }

        // ═════════════════════════════ 地形 ═════════════════════════════

        void PrepareTerrainFeatures()
        {
            int n = Mathf.CeilToInt(_path.Length / CoarseStep) + 1;
            _coarse = new Vector3[n];
            for (int i = 0; i < n; i++) _coarse[i] = _path.PositionAt(Mathf.Min(i * CoarseStep, _path.Length));

            foreach (var l in _route.Landmarks)
            {
                if (l.type == LandmarkType.Bridge && !_hasRiver)
                {
                    float mid = (l.startS + l.endS) * 0.5f;
                    Vector3 p = _path.PositionAt(mid);
                    Vector3 t = _path.TangentAt(mid);
                    _riverCenter = new Vector2(p.x, p.z);
                    _riverAcross = new Vector2(t.x, t.z).normalized;              // 沿鐵路方向 = 橫越河谷
                    _riverAlong = new Vector2(_riverAcross.y, -_riverAcross.x);   // 河流方向
                    _riverHalfWidth = (l.endS - l.startS) * 0.5f - 8f;
                    _waterLevel = p.y - 0.7f - ValleyDepth + 4f;
                    _hasRiver = true;
                }
                if (l.type == LandmarkType.Tunnel)
                {
                    Vector3 a = _path.PositionAt(l.startS), b = _path.PositionAt(l.endS);
                    _mountains.Add((new Vector2(a.x, a.z), new Vector2(b.x, b.z), Mathf.Min(a.y, b.y)));
                }
            }
        }

        /// <summary>找出最近的路徑段，回傳水平距離、s 值與該處軌面高度。</summary>
        void NearestTrack(float x, float z, out float dist, out float s, out float trackY)
        {
            float best = float.MaxValue;
            int bestI = 0;
            float bestT = 0f;
            var p = new Vector2(x, z);
            // 先以每 5 個取樣點粗找，再於附近細找（大幅減少地形生成時間）
            int roughI = 0;
            float roughBest = float.MaxValue;
            for (int i = 0; i < _coarse.Length; i += 5)
            {
                float d = (new Vector2(_coarse[i].x, _coarse[i].z) - p).sqrMagnitude;
                if (d < roughBest) { roughBest = d; roughI = i; }
            }
            int from = Mathf.Max(0, roughI - 12), to = Mathf.Min(_coarse.Length - 1, roughI + 12);
            for (int i = from; i < to; i++)
            {
                var a = new Vector2(_coarse[i].x, _coarse[i].z);
                var b = new Vector2(_coarse[i + 1].x, _coarse[i + 1].z);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best) { best = d; bestI = i; bestT = t; }
            }
            dist = Mathf.Sqrt(best);
            s = (bestI + bestT) * CoarseStep;
            trackY = Mathf.Lerp(_coarse[bestI].y, _coarse[bestI + 1].y, bestT);
        }

        static float Fbm(float x, float z)
        {
            float v = 0f, amp = 0.5f, f = 1f;
            for (int i = 0; i < 4; i++)
            {
                v += amp * Mathf.PerlinNoise(x * f + 31.7f, z * f + 11.3f);
                f *= 2.03f;
                amp *= 0.5f;
            }
            return v / 0.9375f;
        }

        /// <summary>地形高度函數。</summary>
        public float TerrainHeight(float x, float z)
        {
            NearestTrack(x, z, out float d, out float s, out float trackY);
            float bed = trackY - 0.75f;

            // 河谷係數（河谷內壓低丘陵）
            float valley = 0f;
            if (_hasRiver)
            {
                Vector2 rel = new Vector2(x, z) - _riverCenter;
                float across = Mathf.Abs(Vector2.Dot(rel, _riverAcross));
                float alongRiver = Mathf.Abs(Vector2.Dot(rel, _riverAlong));
                valley = (1f - Smooth(_riverHalfWidth * 0.5f, _riverHalfWidth, across)) * (1f - Smooth(1100f, 1500f, alongRiver));
            }

            // 遠離鐵路的丘陵
            float hillW = Smooth(10f, 140f, d) * (1f - valley);
            float hills = (Fbm(x * 0.0045f, z * 0.0045f) * 48f - 10f) * hillW
                          + (Fbm(x * 0.02f, z * 0.02f) - 0.5f) * 3f * Smooth(6f, 40f, d);
            float h = bed + hills;

            // 山（隧道所在）
            foreach (var m in _mountains)
            {
                Vector2 ab = m.b - m.a;
                float len = ab.magnitude;
                Vector2 dir = ab / Mathf.Max(len, 1e-3f);
                Vector2 rel = new Vector2(x, z) - m.a;
                float u = Vector2.Dot(rel, dir);
                float perp = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
                float along;
                if (u < 0f) along = 0.35f * Mathf.Exp(-(u * u) / (70f * 70f));
                else if (u > len) along = 0.35f * Mathf.Exp(-((u - len) * (u - len)) / (70f * 70f));
                else along = 0.35f + 0.65f * Mathf.Sin(Mathf.PI * u / len);
                float bump = MountainHeight * along * Mathf.Exp(-(perp * perp) / (240f * 240f));
                h += bump;
            }

            // 河谷
            bool inBridge = _route.IsInside(LandmarkType.Bridge, s, 6f);
            h -= ValleyDepth * valley;

            // 鐵路路基整平（隧道內與橋下不整平）
            bool inTunnel = _route.IsInside(LandmarkType.Tunnel, s, 0f);
            if (!inTunnel && !inBridge)
            {
                float inner = 4.5f;
                foreach (var st in _route.Stations)
                    if (s > st.PlatformStartS - 30f && s < st.PlatformEndS + 30f) inner = 26f;
                float w = 1f - Smooth(inner, inner + 24f, d);
                h = Mathf.Lerp(h, bed, w);
            }
            return h;
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        void BuildTerrain()
        {
            var parent = Group("Terrain");
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var p in _coarse) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            float x0 = Mathf.Floor((min.x - TerrainMargin) / TerrainCell) * TerrainCell;
            float z0 = Mathf.Floor((min.z - TerrainMargin) / TerrainCell) * TerrainCell;
            int nx = Mathf.CeilToInt((max.x - min.x + TerrainMargin * 2f) / TerrainCell) + 1;
            int nz = Mathf.CeilToInt((max.z - min.z + TerrainMargin * 2f) / TerrainCell) + 1;

            var heights = new float[nx, nz];
            var holes = new bool[nx, nz]; // 隧道上方挖空（由覆土帶與洞門遮蓋）
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                float x = x0 + i * TerrainCell, z = z0 + j * TerrainCell;
                heights[i, j] = TerrainHeight(x, z);
                NearestTrack(x, z, out float d, out float s, out _);
                holes[i, j] = d < TunnelHoleHalfWidth && _route.IsInside(LandmarkType.Tunnel, s, 6f);
            }

            Vector3 Normal(int i, int j)
            {
                float hl = heights[Mathf.Max(i - 1, 0), j], hr = heights[Mathf.Min(i + 1, nx - 1), j];
                float hd = heights[i, Mathf.Max(j - 1, 0)], hu = heights[i, Mathf.Min(j + 1, nz - 1)];
                return new Vector3(hl - hr, 2f * TerrainCell, hd - hu).normalized;
            }

            for (int ci = 0; ci < nx - 1; ci += TerrainChunkCells)
            for (int cj = 0; cj < nz - 1; cj += TerrainChunkCells)
            {
                int ei = Mathf.Min(ci + TerrainChunkCells, nx - 1);
                int ej = Mathf.Min(cj + TerrainChunkCells, nz - 1);
                var mb = new MeshBuilder(2); // 0 草地、1 岩壁
                int w = ej - cj + 1;
                var idx = new int[(ei - ci + 1) * w];
                for (int i = ci; i <= ei; i++)
                for (int j = cj; j <= ej; j++)
                {
                    var pos = new Vector3(x0 + i * TerrainCell, heights[i, j], z0 + j * TerrainCell);
                    idx[(i - ci) * w + (j - cj)] = mb.AddVertex(pos, Normal(i, j), new Vector2(pos.x, pos.z) * 0.05f);
                }
                for (int i = ci; i < ei; i++)
                for (int j = cj; j < ej; j++)
                {
                    int holeCount = (holes[i, j] ? 1 : 0) + (holes[i + 1, j] ? 1 : 0) + (holes[i, j + 1] ? 1 : 0) + (holes[i + 1, j + 1] ? 1 : 0);
                    if (holeCount >= 2) continue;
                    int a = idx[(i - ci) * w + (j - cj)];
                    int b = idx[(i - ci) * w + (j + 1 - cj)];
                    int c = idx[(i + 1 - ci) * w + (j + 1 - cj)];
                    int d = idx[(i + 1 - ci) * w + (j - cj)];
                    // 陡坡使用岩石材質
                    float steep = Mathf.Max(Mathf.Abs(heights[i + 1, j] - heights[i, j]), Mathf.Abs(heights[i, j + 1] - heights[i, j]),
                                            Mathf.Abs(heights[i + 1, j + 1] - heights[i, j])) / TerrainCell;
                    int sub = steep > 0.9f ? 1 : 0;
                    mb.AddTriangle(sub, a, b, c);
                    mb.AddTriangle(sub, a, c, d);
                }
                var go = mb.ToGameObject($"Terrain_{ci}_{cj}", parent, new[] { MaterialLibrary.Grass, MaterialLibrary.Rock }, false);
                go.GetComponent<MeshRenderer>().receiveShadows = true;
            }
        }

        void BuildWater()
        {
            if (!_hasRiver) return;
            var mb = new MeshBuilder(1);
            Vector3 c = new Vector3(_riverCenter.x, _waterLevel, _riverCenter.y);
            Vector3 across = new Vector3(_riverAcross.x, 0, _riverAcross.y) * (_riverHalfWidth * 0.85f);
            Vector3 along = new Vector3(_riverAlong.x, 0, _riverAlong.y) * 1400f;
            // 確保朝上
            Vector3 a = c - across - along, b = c - across + along, cc = c + across + along, d = c + across - along;
            if (Vector3.Cross(b - a, d - a).y < 0) { var tmp = b; b = d; d = tmp; }
            mb.AddQuad(0, a, b, cc, d);
            mb.ToGameObject("River", Group("Water"), new[] { MaterialLibrary.Water }, false);
        }

        // ═════════════════════════════ 樹林 ═════════════════════════════

        void BuildTrees()
        {
            var parent = Group("Trees");
            var rng = new System.Random(20261101);
            const float groupLength = 300f;
            int groups = Mathf.CeilToInt(_path.Length / groupLength);
            var builders = new MeshBuilder[groups];
            for (int g = 0; g < groups; g++) builders[g] = new MeshBuilder(3);

            int count = 1700;
            for (int i = 0; i < count; i++)
            {
                float s = (float)rng.NextDouble() * _path.Length;
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float r = (float)rng.NextDouble();
                float lateral = side * Mathf.Lerp(16f, 280f, r * r);
                Vector3 p = PathExtruder.PointAt(_path, s, lateral, 0);

                NearestTrack(p.x, p.z, out float dTrack, out float sNear, out _);
                if (dTrack < 14f) continue;
                if (_route.IsInside(LandmarkType.Tunnel, sNear, 15f) && dTrack < 30f) continue;
                bool nearStation = false;
                foreach (var st in _route.Stations)
                    if (sNear > st.PlatformStartS - 40f && sNear < st.PlatformEndS + 40f && dTrack < 40f) nearStation = true;
                if (nearStation) continue;

                float y = TerrainHeight(p.x, p.z);
                if (_hasRiver && y < _waterLevel + 1.0f) continue;
                p.y = y - 0.2f;

                int g = Mathf.Clamp((int)(s / groupLength), 0, groups - 1);
                var mb = builders[g];
                float scale = Mathf.Lerp(0.7f, 1.4f, (float)rng.NextDouble());
                bool conifer = y > 12f || rng.NextDouble() < 0.45;
                if (conifer)
                {
                    mb.AddCylinder(0, p, 0.25f * scale, 3f * scale, 5, false);
                    mb.AddCone(1, p + Vector3.up * 2.0f * scale, 2.6f * scale, 5.5f * scale, 6);
                    mb.AddCone(1, p + Vector3.up * 4.6f * scale, 1.9f * scale, 4.5f * scale, 6);
                }
                else
                {
                    mb.AddCylinder(0, p, 0.3f * scale, 3.5f * scale, 5, false);
                    mb.AddFrustum(2, p + Vector3.up * 2.6f * scale, 2.8f * scale, 1.6f * scale, 3.4f * scale, 7, true);
                    mb.AddCone(2, p + Vector3.up * 6.0f * scale, 1.6f * scale, 1.6f * scale, 7);
                }
            }

            for (int g = 0; g < groups; g++)
            {
                if (builders[g].VertexCount == 0) continue;
                builders[g].ToGameObject($"Trees_{g}", parent,
                    new[] { MaterialLibrary.TreeTrunk, MaterialLibrary.TreeLeaf, MaterialLibrary.TreeLeafLight });
            }
        }

        // ═════════════════════════════ 小工具 ═════════════════════════════

        void CreateBoard(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = new Vector3(size.x, size.y, 0.04f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>建立 3D 文字。rot 的 forward 應與觀看者的視線方向相同（TextMesh 由 -Z 側閱讀）。</summary>
        public void CreateText(string name, Transform parent, string text, Vector3 pos, Quaternion rot, float height, Color color)
        {
            if (_font == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = height * 10f / tm.fontSize; // 世界高度 ≈ fontSize × characterSize × 0.1
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _font.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
