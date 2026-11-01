using System.Collections.Generic;
using RailwayMission.Cameras;
using RailwayMission.Core;
using RailwayMission.Mission;
using RailwayMission.Save;
using RailwayMission.Track;
using RailwayMission.Train;
using UnityEngine;

namespace RailwayMission.UI
{
    /// <summary>
    /// 手機駕駛介面（橫向全螢幕）。以 IMGUI 繪製，不需任何 UI 預製物，觸控與滑鼠皆可操作。
    /// 左下：煞車把手；右下：油門把手；下方中央：速度表、速限、緊急煞車；
    /// 上方：任務目標、下一站距離、暫停／喇叭／視角按鈕；進站時顯示停車位置導引。
    /// 鍵盤（編輯器測試用）：W/S 油門、↓/↑ 煞車、空白鍵 緊急煞車、H 喇叭、C 視角、Esc 暫停。
    /// </summary>
    public class DrivingHUD : MonoBehaviour
    {
        const float RefHeight = 1080f;

        TrainController _train;
        MissionManager _mission;
        TrainCameraController _camera;
        TrackRoute _route;
        System.Action _restart;

        Font _font;
        float _scale = 1f;
        float _vw;          // 虛擬寬度
        Rect _safe;         // 安全區域（虛擬座標）

        bool _paused;
        int _activeLever;   // 0 無、1 油門、2 煞車、3 拖曳鏡頭
        readonly List<Rect> _blockers = new List<Rect>();

        string _notice;
        float _noticeTime;
        bool _newBest;

        static Texture2D _white;
        readonly Dictionary<int, GUIStyle> _styles = new Dictionary<int, GUIStyle>();

        // 色彩
        static readonly Color Panel = new Color(0.04f, 0.06f, 0.08f, 0.62f);
        static readonly Color PanelSolid = new Color(0.07f, 0.09f, 0.12f, 0.96f);
        static readonly Color Accent = new Color(0.16f, 0.64f, 0.29f);
        static readonly Color Warn = new Color(0.95f, 0.25f, 0.2f);
        static readonly Color Amber = new Color(1f, 0.72f, 0.1f);
        static readonly Color Gold = new Color(0.9f, 0.63f, 0.1f);
        static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);

        public void Init(TrainController train, MissionManager mission, TrainCameraController cam, TrackRoute route, Font font, System.Action restart)
        {
            _train = train;
            _mission = mission;
            _camera = cam;
            _route = route;
            _font = font;
            _restart = restart;
            _mission.Notice += ShowNotice;
            _mission.Finished += OnFinished;
        }

        void ShowNotice(string text)
        {
            _notice = text;
            _noticeTime = Time.unscaledTime;
        }

        void OnFinished(MissionResult r)
        {
            _newBest = false;
            if (r.success)
                _newBest = SaveManager.RecordMission(_mission.Data.missionId, r.stars, r.Total, r.elapsed, r.coins);
        }

        public void ResetUI()
        {
            _paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            _notice = null;
            _activeLever = 0;
        }

        // ══════════════════════════ 繪圖工具 ══════════════════════════

        static Texture2D White
        {
            get
            {
                if (_white != null) return _white;
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
                return _white;
            }
        }

        void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        GUIStyle Style(int size, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            int key = size * 100 + (int)anchor * 2 + (bold ? 1 : 0);
            if (_styles.TryGetValue(key, out var s)) return s;
            s = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = size,
                alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = true,
                richText = true,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            s.normal.textColor = Color.white;
            _styles[key] = s;
            return s;
        }

        void Text(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            var st = Style(size, anchor, bold);
            var old = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(r, text, st);
            GUI.contentColor = old;
        }

        bool Button(Rect r, string label, Color bg, int size = 34, bool enabled = true)
        {
            _blockers.Add(r);
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            Box(r, enabled ? (hover ? Color.Lerp(bg, Color.white, 0.15f) : bg) : new Color(0.3f, 0.3f, 0.3f, 0.6f));
            Text(r, label, size, enabled ? Color.white : Muted, TextAnchor.MiddleCenter, true);
            if (enabled && e.type == EventType.MouseDown && r.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        static string FormatTime(float t)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(t));
            return $"{s / 60:00}:{s % 60:00}";
        }

        // ══════════════════════════ 主繪製 ══════════════════════════

        void OnGUI()
        {
            if (_train == null || _mission == null) return;
            _scale = Screen.height / RefHeight;
            _vw = Screen.width / _scale;
            var sa = Screen.safeArea;
            _safe = new Rect(sa.x / _scale, (Screen.height - sa.yMax) / _scale, sa.width / _scale, sa.height / _scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            _blockers.Clear();

            HandleKeyboard();

            switch (_mission.State)
            {
                case MissionState.Briefing:
                    DrawBriefing();
                    break;
                case MissionState.Running:
                    DrawDriving();
                    if (_paused) DrawPause();
                    break;
                case MissionState.Completed:
                case MissionState.Failed:
                    DrawDriving(false);
                    DrawResult();
                    break;
            }
        }

        void HandleKeyboard()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (_mission.State != MissionState.Running)
            {
                if (e.keyCode == KeyCode.Return && _mission.State == MissionState.Briefing) { _mission.Begin(); e.Use(); }
                return;
            }
            switch (e.keyCode)
            {
                case KeyCode.W: _train.throttleInput = Mathf.Clamp01(_train.throttleInput + 0.1f); _train.brakeInput = 0f; break;
                case KeyCode.S: _train.throttleInput = Mathf.Clamp01(_train.throttleInput - 0.1f); break;
                case KeyCode.DownArrow: _train.brakeInput = Mathf.Clamp01(_train.brakeInput + 0.1f); _train.throttleInput = 0f; break;
                case KeyCode.UpArrow: _train.brakeInput = Mathf.Clamp01(_train.brakeInput - 0.1f); break;
                case KeyCode.Space: _train.SetEmergency(!_train.EmergencyBrake); break;
                case KeyCode.H: _train.SoundHorn(); break;
                case KeyCode.C: _camera.NextMode(); break;
                case KeyCode.Escape: SetPaused(!_paused); break;
                default: return;
            }
            e.Use();
        }

        void SetPaused(bool p)
        {
            _paused = p;
            Time.timeScale = p ? 0f : 1f;
            AudioListener.pause = p;
        }

        // ══════════════════════════ 駕駛畫面 ══════════════════════════

        void DrawDriving(bool interactive = true)
        {
            float left = _safe.x + 28f;
            float right = _safe.xMax - 28f;
            float top = _safe.y + 24f;
            float bottom = _safe.yMax - 24f;

            DrawMissionPanel(new Rect(left, top, 560f, 0f));
            DrawStationPanel(new Rect(Mathf.Max((_vw - 560f) * 0.5f, left + 580f), top, 560f, 132f));

            // 右上按鈕
            if (interactive && !_paused)
            {
                float bw = 128f, bh = 96f, gap = 14f;
                float x = right - bw;
                if (Button(new Rect(x, top, bw, bh), Loc.T("暫停", "Pause"), Panel, 30)) SetPaused(true);
                x -= bw + gap;
                if (Button(new Rect(x, top, bw, bh), Loc.T("視角", "View"), Panel, 30)) _camera.NextMode();
                x -= bw + gap;
                if (Button(new Rect(x, top, bw, bh), Loc.T("喇叭", "Horn"), Panel, 30)) _train.SoundHorn();
                Text(new Rect(right - 3 * bw - 2 * gap, top + bh + 6f, 3 * bw + 2 * gap, 36f), CameraName(), 24, Muted, TextAnchor.MiddleRight);
            }

            // 把手
            float leverW = 150f, leverH = 470f;
            var brakeRect = new Rect(left, bottom - leverH, leverW, leverH);
            var throttleRect = new Rect(right - leverW, bottom - leverH, leverW, leverH);
            DrawLever(brakeRect, 2, _train.brakeInput, _train.Physics.BrakeLevel, Loc.T("煞車", "Brake"), new Color(0.92f, 0.35f, 0.18f), true, interactive && !_paused);
            DrawLever(throttleRect, 1, _train.throttleInput, _train.Physics.TractionLevel, Loc.T("油門", "Throttle"), Accent, false, interactive && !_paused);

            DrawSpeedPanel(new Rect((_vw - 640f) * 0.5f, bottom - 230f, 640f, 230f), interactive && !_paused);
            DrawStopGuide(new Rect((_vw - 640f) * 0.5f, bottom - 230f - 18f - 104f, 640f, 104f));
            DrawNotice(new Rect((_vw - 900f) * 0.5f, top + 150f, 900f, 70f));

            if (interactive && !_paused) HandleCameraDrag();
        }

        string CameraName()
        {
            switch (_camera.Mode)
            {
                case CameraMode.Cab: return Loc.T("駕駛室視角", "Cab view");
                case CameraMode.Nose: return Loc.T("車頭視角", "Front view");
                default: return Loc.T("追蹤視角（可拖曳旋轉）", "Chase view (drag to orbit)");
            }
        }

        void DrawMissionPanel(Rect r)
        {
            var m = _mission;
            int count = m.ObjectiveCount;
            float h = 128f + count * 38f;
            r.height = h;
            Box(r, Panel);
            Text(new Rect(r.x + 20, r.y + 12, r.width - 40, 36), Loc.T($"任務 {m.Data.missionNumber:00}｜", $"Mission {m.Data.missionNumber:00} | ") + m.Data.Title, 28, Color.white, TextAnchor.MiddleLeft, true);

            bool late = m.Elapsed > m.Data.timeLimit;
            Text(new Rect(r.x + 20, r.y + 50, r.width - 40, 30),
                Loc.T("時間 ", "Time ") + FormatTime(m.Elapsed) + "  /  " + Loc.T("預定 ", "Target ") + FormatTime(m.Data.timeLimit),
                24, late ? Warn : Muted);

            float y = r.y + 92f;
            int current = m.CurrentObjectiveIndex;
            for (int i = 0; i < count; i++)
            {
                var o = m.GetObjective(i);
                bool done = m.IsObjectiveDone(i);
                bool ongoing = o.type == ObjectiveType.ObeySpeedLimits;
                string mark;
                Color c;
                if (ongoing)
                {
                    bool ok = m.Violations == 0;
                    mark = ok ? "◆" : "✗";
                    c = ok ? new Color(0.6f, 0.85f, 1f) : Warn;
                }
                else if (done) { mark = "✓"; c = new Color(0.55f, 0.95f, 0.6f); }
                else if (i == current) { mark = "▶"; c = Color.white; }
                else { mark = "○"; c = Muted; }
                Text(new Rect(r.x + 20, y, 34, 34), mark, 24, c, TextAnchor.MiddleCenter, true);
                Text(new Rect(r.x + 60, y, r.width - 80, 34), Loc.T(o.labelZh, o.labelEn), 25, c, TextAnchor.MiddleLeft, i == current);
                y += 38f;
            }
        }

        void DrawStationPanel(Rect r)
        {
            var dest = _mission.Destination;
            var origin = _mission.Origin;
            Box(r, Panel);
            float dist = Mathf.Max(0f, dest.stopS - _train.HeadS);
            string name = Loc.T(dest.nameZh, dest.nameEn);
            Text(new Rect(r.x + 22, r.y + 10, r.width - 44, 34), Loc.T("下一站：", "Next: ") + name, 26, Muted);
            string d = dist >= 1000f ? $"{dist / 1000f:0.00} km" : $"{dist:0} m";
            Text(new Rect(r.x + 22, r.y + 44, r.width - 44, 46), Loc.T("距離 ", "Distance ") + d, 36, Color.white, TextAnchor.MiddleLeft, true);
            float total = Mathf.Max(1f, dest.stopS - origin.stopS);
            float p = Mathf.Clamp01((_train.HeadS - origin.stopS) / total);
            var bar = new Rect(r.x + 22, r.y + 104, r.width - 44, 10);
            Box(bar, new Color(1, 1, 1, 0.18f));
            Box(new Rect(bar.x, bar.y, bar.width * p, bar.height), Accent);
            // 地標標記
            foreach (var l in _route.Landmarks)
            {
                float lp = Mathf.Clamp01((l.startS - origin.stopS) / total);
                float lw = Mathf.Clamp01((l.endS - origin.stopS) / total) - lp;
                Box(new Rect(bar.x + bar.width * lp, bar.y - 4, Mathf.Max(3f, bar.width * lw), 3), l.type == LandmarkType.Tunnel ? Muted : new Color(0.5f, 0.8f, 1f, 0.8f));
            }
        }

        void DrawLever(Rect r, int id, float value, float actual, string label, Color color, bool fillFromTop, bool interactive)
        {
            _blockers.Add(r);
            Box(r, Panel);
            Text(new Rect(r.x, r.y + 8, r.width, 34), label, 28, Color.white, TextAnchor.MiddleCenter, true);
            Text(new Rect(r.x, r.y + 42, r.width, 30), $"{Mathf.RoundToInt(value * 100)}%", 26, color, TextAnchor.MiddleCenter, true);

            var track = new Rect(r.x + r.width * 0.5f - 22f, r.y + 86f, 44f, r.height - 110f);
            Box(track, new Color(1, 1, 1, 0.12f));
            // 實際作用量（煞車缸壓力／牽引輸出）
            float fh = track.height * actual;
            var fill = fillFromTop ? new Rect(track.x, track.y, track.width, fh) : new Rect(track.x, track.yMax - fh, track.width, fh);
            Box(fill, new Color(color.r, color.g, color.b, 0.55f));
            // 刻度
            for (int i = 0; i <= 4; i++)
            {
                float ty = track.y + track.height * i / 4f;
                Box(new Rect(track.x - 16f, ty - 1f, 12f, 2f), Muted);
                Box(new Rect(track.xMax + 4f, ty - 1f, 12f, 2f), Muted);
            }
            // 把手位置
            float hy = fillFromTop ? track.y + track.height * value : track.yMax - track.height * value;
            var handle = new Rect(r.x + 10f, hy - 26f, r.width - 20f, 52f);
            Box(handle, color);
            Box(new Rect(handle.x + 12f, handle.center.y - 2f, handle.width - 24f, 4f), new Color(1, 1, 1, 0.8f));

            if (!interactive) return;
            var e = Event.current;
            var hit = new Rect(r.x - 20f, r.y, r.width + 40f, r.height);
            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
            {
                _activeLever = id;
                e.Use();
            }
            if (_activeLever == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag || e.type == EventType.Used))
            {
                float t = Mathf.Clamp01((e.mousePosition.y - track.y) / track.height);
                float v = fillFromTop ? t : 1f - t;
                // 吸附到 0% 與 100%
                if (v < 0.03f) v = 0f;
                if (v > 0.97f) v = 1f;
                if (id == 1)
                {
                    _train.throttleInput = v;
                    if (v > 0f) _train.brakeInput = 0f; // 單一主控器：推油門時煞車歸零
                }
                else
                {
                    _train.brakeInput = v;
                    if (v > 0f) _train.throttleInput = 0f;
                }
                if (e.type == EventType.MouseDrag) e.Use();
            }
            if (e.rawType == EventType.MouseUp && _activeLever == id) _activeLever = 0;
        }

        void DrawSpeedPanel(Rect r, bool interactive)
        {
            _blockers.Add(r);
            bool over = _mission.Overspeeding;
            bool blink = over && Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.3f;
            Box(r, blink ? new Color(0.45f, 0.05f, 0.05f, 0.8f) : Panel);

            // 速度
            float speed = _train.SpeedKmh;
            Text(new Rect(r.x + 24, r.y + 14, 300, 120), $"{speed:0}", 104, over ? Warn : Color.white, TextAnchor.MiddleLeft, true);
            Text(new Rect(r.x + 26, r.y + 130, 300, 34), "km/h", 28, Muted);

            // 速限標誌
            float limit = _mission.State == MissionState.Running ? _mission.CurrentLimit : _route.SpeedLimitAt(_train.HeadS);
            var sign = new Rect(r.x + 300, r.y + 22, 120, 120);
            Box(sign, Warn);
            Box(new Rect(sign.x + 10, sign.y + 10, sign.width - 20, sign.height - 20), Color.white);
            Text(sign, $"{limit:0}", 50, Color.black, TextAnchor.MiddleCenter, true);
            Text(new Rect(sign.x - 10, sign.yMax + 4, sign.width + 20, 30), Loc.T("速限", "Limit"), 22, Muted, TextAnchor.MiddleCenter);

            // 前方速限
            if (_route.NextSpeedChange(_train.HeadS, 900f, out float dist, out float next))
            {
                Text(new Rect(r.x + 440, r.y + 24, 190, 32), Loc.T("前方速限", "Next limit"), 22, Muted);
                Text(new Rect(r.x + 440, r.y + 56, 190, 44), $"{next:0} km/h", 32, next < limit ? Amber : Color.white, TextAnchor.MiddleLeft, true);
                Text(new Rect(r.x + 440, r.y + 100, 190, 32), $"{dist:0} m", 26, Color.white);
            }

            // 加速度／狀態
            float a = _train.Physics.Acceleration;
            string state = _train.EmergencyBrake ? Loc.T("緊急煞車", "EMERGENCY")
                : _train.Physics.BrakeLevel > 0.05f ? Loc.T("煞車中", "Braking")
                : _train.Physics.TractionLevel > 0.02f ? Loc.T("牽引中", "Powering")
                : Loc.T("惰行", "Coasting");
            Text(new Rect(r.x + 440, r.y + 134, 190, 30), state, 22, _train.EmergencyBrake ? Warn : Muted);
            Text(new Rect(r.x + 160, r.y + 130, 140, 34), $"{a:+0.00;-0.00} m/s²", 22, Muted, TextAnchor.MiddleRight);

            // 緊急煞車按鈕
            var eb = new Rect(r.x + 24, r.yMax - 58, r.width - 48, 46);
            if (interactive)
            {
                string ebLabel = _train.EmergencyBrake
                    ? (_train.IsStopped ? Loc.T("解除緊急煞車", "Release emergency brake") : Loc.T("緊急煞車作用中…", "Emergency braking…"))
                    : Loc.T("緊急煞車 EB", "EMERGENCY BRAKE");
                if (Button(eb, ebLabel, _train.EmergencyBrake ? new Color(0.5f, 0.1f, 0.1f, 0.95f) : new Color(0.75f, 0.1f, 0.08f, 0.95f), 26))
                    _train.SetEmergency(!_train.EmergencyBrake);
            }
        }

        void DrawStopGuide(Rect r)
        {
            if (_mission.State != MissionState.Running) return;
            float d = _mission.DistanceToStop;
            if (d > 450f || d < -20f) return;
            Box(r, Panel);
            Color c = Mathf.Abs(d) <= _mission.Data.stopPerfectMeters ? new Color(0.4f, 1f, 0.5f)
                    : Mathf.Abs(d) <= _mission.Data.stopGoodMeters ? Amber
                    : d < 0 ? Warn : Color.white;
            string txt = d >= 0 ? Loc.T($"停車位置 前方 {d:0.0} m", $"Stop marker in {d:0.0} m") : Loc.T($"已超過停車位置 {-d:0.0} m", $"Overran marker by {-d:0.0} m");
            Text(new Rect(r.x + 20, r.y + 8, r.width - 40, 44), txt, 32, c, TextAnchor.MiddleCenter, true);

            // 標尺：±30 m
            var bar = new Rect(r.x + 30, r.y + 64, r.width - 60, 16);
            Box(bar, new Color(1, 1, 1, 0.15f));
            float zero = bar.x + bar.width * 0.75f; // 停車標位於 3/4 處
            float perPx = bar.width * 0.75f / 40f;   // 左側 40 m
            float good = _mission.Data.stopGoodMeters * perPx;
            Box(new Rect(zero - good, bar.y, good * 2f, bar.height), new Color(1f, 0.75f, 0.1f, 0.4f));
            Box(new Rect(zero - 2f, bar.y - 8f, 4f, bar.height + 16f), Color.white);
            float pos = Mathf.Clamp(zero - d * perPx, bar.x, bar.xMax);
            Box(new Rect(pos - 6f, bar.y - 4f, 12f, bar.height + 8f), c);
        }

        void DrawNotice(Rect r)
        {
            if (string.IsNullOrEmpty(_notice)) return;
            float age = Time.unscaledTime - _noticeTime;
            if (age > 3.5f) return;
            float alpha = Mathf.Clamp01(3.5f - age);
            Box(r, new Color(0, 0, 0, 0.55f * alpha));
            Text(r, _notice, 30, new Color(1, 1, 1, alpha), TextAnchor.MiddleCenter, true);
        }

        void HandleCameraDrag()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && _activeLever == 0)
            {
                foreach (var b in _blockers) if (b.Contains(e.mousePosition)) return;
                _activeLever = 3;
            }
            else if (e.type == EventType.MouseDrag && _activeLever == 3)
            {
                _camera.Drag(e.delta);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                _camera.Zoom(-e.delta.y * 2f);
                e.Use();
            }
            if (e.rawType == EventType.MouseUp && _activeLever == 3) _activeLever = 0;
        }

        // ══════════════════════════ 任務簡報 ══════════════════════════

        void DrawBriefing()
        {
            Box(new Rect(0, 0, _vw, RefHeight), new Color(0, 0, 0, 0.45f));
            var data = _mission.Data;
            var card = new Rect((_vw - 1100f) * 0.5f, 90f, 1100f, 900f);
            Box(card, PanelSolid);
            Box(new Rect(card.x, card.y, card.width, 8f), Accent);

            float x = card.x + 50f, w = card.width - 100f, y = card.y + 40f;
            Text(new Rect(x, y, w, 40), "RAILWAY MISSION 3D", 26, Accent, TextAnchor.MiddleLeft, true);
            y += 46f;
            Text(new Rect(x, y, w, 64), Loc.T($"任務 {data.missionNumber:00}：", $"Mission {data.missionNumber:00}: ") + data.Title, 52, Color.white, TextAnchor.MiddleLeft, true);
            y += 74f;
            Text(new Rect(x, y, w, 30), new string('★', data.difficulty) + new string('☆', 3 - data.difficulty) + "   "
                 + Loc.T("路線：", "Route: ") + _route.Definition.routeName + "   " + Loc.T("車種：", "Train: ") + Loc.T(_train.Data.displayNameZh, _train.Data.displayNameEn), 24, Muted);
            y += 46f;
            Text(new Rect(x, y, w, 110), data.Description, 28, Color.white, TextAnchor.UpperLeft);
            y += 120f;

            Text(new Rect(x, y, w, 34), Loc.T("任務目標", "Objectives"), 28, Accent, TextAnchor.MiddleLeft, true);
            y += 40f;
            for (int i = 0; i < data.objectives.Count; i++)
            {
                var o = data.objectives[i];
                Text(new Rect(x + 10, y, w / 2f, 32), $"{i + 1}. " + Loc.T(o.labelZh, o.labelEn), 25, Color.white);
                y += 34f;
            }

            // 操作說明（右欄）
            var help = new Rect(card.x + card.width * 0.55f, card.y + 380f, card.width * 0.45f - 50f, 300f);
            Text(new Rect(help.x, help.y, help.width, 34), Loc.T("操作方式", "Controls"), 28, Accent, TextAnchor.MiddleLeft, true);
            Text(new Rect(help.x, help.y + 42, help.width, 260),
                Loc.T("• 右下油門：往上推加速\n• 左下煞車：往下拉減速\n• 進站前提早煞車，停在「停」字標誌\n• 拖曳畫面可轉動視角\n• 鍵盤：W/S 油門、↓/↑ 煞車、空白鍵緊急煞車",
                      "• Right lever: push up to accelerate\n• Left lever: pull down to brake\n• Brake early and stop at the 停 marker\n• Drag the screen to look around\n• Keys: W/S throttle, ↓/↑ brake, Space EB"),
                23, Muted, TextAnchor.UpperLeft);

            // 最佳紀錄
            var rec = SaveManager.GetRecord(data.missionId);
            string best = rec != null
                ? Loc.T($"最佳紀錄：{new string('★', rec.bestStars)}{new string('☆', 3 - rec.bestStars)}  {rec.bestScore} 分", $"Best: {new string('★', rec.bestStars)}{new string('☆', 3 - rec.bestStars)}  {rec.bestScore} pts")
                : Loc.T("尚未挑戰", "Not played yet");
            Text(new Rect(x, card.yMax - 150, w, 34), best + "      " + Loc.T("金幣：", "Coins: ") + SaveManager.Data.coins, 25, Gold);

            if (Button(new Rect(card.xMax - 50f - 420f, card.yMax - 106f, 420f, 76f), Loc.T("開始任務", "Start Mission"), Accent, 36))
                _mission.Begin();
            if (Button(new Rect(x, card.yMax - 106f, 220f, 76f), Loc.T("English", "中文"), new Color(1, 1, 1, 0.12f), 28))
            {
                Loc.Toggle();
                _styles.Clear();
            }
        }

        // ══════════════════════════ 暫停 ══════════════════════════

        void DrawPause()
        {
            Box(new Rect(0, 0, _vw, RefHeight), new Color(0, 0, 0, 0.55f));
            var card = new Rect((_vw - 640f) * 0.5f, 250f, 640f, 560f);
            Box(card, PanelSolid);
            Text(new Rect(card.x, card.y + 40, card.width, 60), Loc.T("暫停", "Paused"), 50, Color.white, TextAnchor.MiddleCenter, true);
            float bx = card.x + 80f, bw = card.width - 160f;
            if (Button(new Rect(bx, card.y + 150f, bw, 86f), Loc.T("繼續駕駛", "Resume"), Accent, 34)) SetPaused(false);
            if (Button(new Rect(bx, card.y + 256f, bw, 86f), Loc.T("重新開始任務", "Restart Mission"), new Color(1, 1, 1, 0.14f), 34))
            {
                SetPaused(false);
                _restart?.Invoke();
            }
            if (Button(new Rect(bx, card.y + 362f, bw, 86f), Loc.T("切換視角：", "View: ") + CameraName(), new Color(1, 1, 1, 0.14f), 28))
                _camera.NextMode();
            Text(new Rect(card.x, card.yMax - 70, card.width, 40), Loc.T("語言", "Language") + "：" + (Loc.Current == Language.ZhTW ? "繁體中文" : "English"), 24, Muted, TextAnchor.MiddleCenter);
        }

        // ══════════════════════════ 結算 ══════════════════════════

        void DrawResult()
        {
            var r = _mission.Result;
            Box(new Rect(0, 0, _vw, RefHeight), new Color(0, 0, 0, 0.5f));
            var card = new Rect((_vw - 820f) * 0.5f, 90f, 820f, 900f);
            Box(card, PanelSolid);
            Box(new Rect(card.x, card.y, card.width, 8f), r.success ? Accent : Warn);
            float x = card.x + 60f, w = card.width - 120f, y = card.y + 40f;

            Text(new Rect(card.x, y, card.width, 64), r.success ? Loc.T("任務完成！", "Mission Complete!") : Loc.T("任務失敗", "Mission Failed"), 56, Color.white, TextAnchor.MiddleCenter, true);
            y += 70f;
            Text(new Rect(card.x, y, card.width, 34), Loc.T($"任務 {_mission.Data.missionNumber:00}｜", $"Mission {_mission.Data.missionNumber:00} | ") + _mission.Data.Title, 26, Muted, TextAnchor.MiddleCenter);
            y += 50f;

            if (!r.success)
            {
                Text(new Rect(x, y + 40, w, 160), r.failReason, 34, Warn, TextAnchor.MiddleCenter, true);
                Text(new Rect(x, y + 220, w, 120), Loc.T("提示：注意前方速限，進站前約 300 公尺開始逐步煞車。", "Tip: watch the next speed limit and start braking about 300 m before the station."), 26, Muted, TextAnchor.MiddleCenter);
            }
            else
            {
                string stars = new string('★', r.stars) + new string('☆', 3 - r.stars);
                Text(new Rect(card.x, y, card.width, 100), stars, 90, Gold, TextAnchor.MiddleCenter, true);
                y += 116f;
                Box(new Rect(x, y, w, 2), new Color(1, 1, 1, 0.15f));
                y += 16f;
                Row(ref y, x, w, Loc.T("準時抵達", "On time"), $"{FormatTime(r.elapsed)} / {FormatTime(r.timeLimit)}", r.timeScore, MissionScoring.MaxTime);
                string stopTxt = r.stopError >= 0 ? Loc.T($"超過 {r.stopError:0.0} m", $"{r.stopError:0.0} m past") : Loc.T($"差 {-r.stopError:0.0} m", $"{-r.stopError:0.0} m short");
                Row(ref y, x, w, Loc.T("精準停靠", "Precise stop"), stopTxt, r.stopScore, MissionScoring.MaxStop);
                Row(ref y, x, w, Loc.T("安全駕駛", "Safe driving"), Loc.T($"超速 {r.violations} 次", $"{r.violations} overspeed") + (r.emergencyUses > 0 ? Loc.T("、緊急煞車", ", EB used") : ""), r.safetyScore, MissionScoring.MaxSafety);
                Row(ref y, x, w, Loc.T("平穩操作", "Smooth ride"), Loc.T($"急加減速 {r.harshEvents} 次", $"{r.harshEvents} harsh"), r.smoothScore, MissionScoring.MaxSmooth);
                Box(new Rect(x, y + 4, w, 2), new Color(1, 1, 1, 0.15f));
                y += 20f;
                Text(new Rect(x, y, w * 0.5f, 56), Loc.T("獲得金幣", "Coins earned"), 32, Color.white, TextAnchor.MiddleLeft, true);
                Text(new Rect(x + w * 0.5f, y, w * 0.5f, 56), $"{r.coins}", 48, Gold, TextAnchor.MiddleRight, true);
                y += 60f;
                Text(new Rect(x, y, w, 32), (_newBest ? Loc.T("新紀錄！  ", "New best!  ") : "") + Loc.T("金幣總額：", "Total coins: ") + SaveManager.Data.coins, 24, _newBest ? Gold : Muted, TextAnchor.MiddleRight);
            }

            float by = card.yMax - 116f;
            if (Button(new Rect(x, by, w * 0.48f, 80f), Loc.T("重新開始", "Retry"), r.success ? new Color(1, 1, 1, 0.14f) : Accent, 34))
                _restart?.Invoke();
            Button(new Rect(x + w * 0.52f, by, w * 0.48f, 80f), Loc.T("任務 02（開發中）", "Mission 02 (soon)"), Accent, 28, false);
        }

        void Row(ref float y, float x, float w, string label, string detail, int score, int max)
        {
            Text(new Rect(x, y, w * 0.4f, 44), label, 30, Color.white, TextAnchor.MiddleLeft, true);
            Text(new Rect(x + w * 0.38f, y, w * 0.4f, 44), detail, 24, Muted, TextAnchor.MiddleLeft);
            Text(new Rect(x + w * 0.75f, y, w * 0.25f, 44), $"+{score}", 32, score >= max ? new Color(0.45f, 0.95f, 0.55f) : Color.white, TextAnchor.MiddleRight, true);
            y += 56f;
        }
    }
}
