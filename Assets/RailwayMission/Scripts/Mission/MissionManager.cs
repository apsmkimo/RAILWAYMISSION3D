using System;
using RailwayMission.Core;
using RailwayMission.Track;
using RailwayMission.Train;
using UnityEngine;

namespace RailwayMission.Mission
{
    public enum MissionState { Briefing, Running, Completed, Failed }

    /// <summary>
    /// 任務流程：載入任務 → 初始化火車 → 計時 → 監測速度 → 監測停靠位置 → 判斷違規 → 判斷抵達 → 評分。
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public MissionData Data { get; private set; }
        public MissionState State { get; private set; } = MissionState.Briefing;
        public float Elapsed { get; private set; }
        public int Violations { get; private set; }
        public int EmergencyUses { get; private set; }
        public int HarshEvents { get; private set; }
        public Station Destination { get; private set; }
        public Station Origin { get; private set; }
        public MissionResult Result { get; private set; }

        /// <summary>目前是否處於超速狀態（給 UI 閃爍警示）。</summary>
        public bool Overspeeding { get; private set; }
        public float CurrentLimit { get; private set; }

        public event Action<string> Notice;           // 一般提示訊息
        public event Action<MissionResult> Finished;  // 任務結束

        bool[] _done;
        TrainController _train;
        TrackRoute _route;
        float _overspeedTimer;
        bool _overspeedCounted;
        float _harshCooldown;
        bool _lastEmergency;
        float _stoppedTimer;
        bool _shortStopNoticeShown;

        public int ObjectiveCount => Data.objectives.Count;
        public ObjectiveDef GetObjective(int i) => Data.objectives[i];
        public bool IsObjectiveDone(int i) => _done[i];

        /// <summary>目前要完成的目標索引（略過持續性目標）。全部完成回傳 -1。</summary>
        public int CurrentObjectiveIndex
        {
            get
            {
                for (int i = 0; i < _done.Length; i++)
                    if (!_done[i] && Data.objectives[i].type != ObjectiveType.ObeySpeedLimits) return i;
                return -1;
            }
        }

        /// <summary>到目的地停車位置的距離（公尺），負值代表已超過。</summary>
        public float DistanceToStop => Destination.stopS - _train.HeadS;

        public void Init(MissionData data, TrackRoute route, TrainController train)
        {
            Data = data;
            _route = route;
            _train = train;
            Origin = route.GetStation(data.startStationId);
            Destination = route.GetStation(data.destinationStationId);
            ResetMission();
        }

        public void ResetMission()
        {
            _done = new bool[Data.objectives.Count];
            Elapsed = 0f;
            Violations = 0;
            EmergencyUses = 0;
            HarshEvents = 0;
            _overspeedTimer = 0f;
            _overspeedCounted = false;
            _harshCooldown = 0f;
            _lastEmergency = false;
            _stoppedTimer = 0f;
            _shortStopNoticeShown = false;
            Overspeeding = false;
            State = MissionState.Briefing;
            _train.ResetTrain(Origin.stopS);
            _train.InputLocked = true;
        }

        public void Begin()
        {
            if (State != MissionState.Briefing) return;
            State = MissionState.Running;
            _train.InputLocked = false;
            Notice?.Invoke(Loc.T("出發信號已開放，放開煞車並加速。", "Signal clear. Release the brake and accelerate."));
        }

        void Update()
        {
            if (State != MissionState.Running || Time.deltaTime <= 0f) return;
            float dt = Time.deltaTime;
            Elapsed += dt;

            float speed = _train.SpeedKmh;
            float head = _train.HeadS;
            CurrentLimit = _route.EffectiveLimit(head, _train.Data.TrainLength);

            CheckSpeed(speed, dt);
            if (State != MissionState.Running) return;
            CheckComfort(dt);
            CheckObjectives(speed, head);
            if (State == MissionState.Running) CheckArrival(head, dt);
        }

        void CheckSpeed(float speed, float dt)
        {
            Overspeeding = speed > CurrentLimit + Data.overspeedGrace;
            if (speed > CurrentLimit + Data.severeOverspeed)
            {
                Fail(Loc.T("嚴重超速，列車已被 ATP 強制停車。", "Severe overspeed. ATP forced an emergency stop."));
                return;
            }
            if (Overspeeding)
            {
                _overspeedTimer += dt;
                if (_overspeedTimer > 2f && !_overspeedCounted)
                {
                    _overspeedCounted = true;
                    Violations++;
                    Notice?.Invoke(Loc.T($"超速！目前速限 {CurrentLimit:0} km/h", $"Overspeed! Limit is {CurrentLimit:0} km/h"));
                }
            }
            else if (speed <= CurrentLimit)
            {
                _overspeedTimer = 0f;
                _overspeedCounted = false;
            }
        }

        void CheckComfort(float dt)
        {
            if (_train.EmergencyBrake && !_lastEmergency)
            {
                EmergencyUses++;
                Notice?.Invoke(Loc.T("緊急煞車！停車後才能解除。", "Emergency brake! Release it after stopping."));
            }
            _lastEmergency = _train.EmergencyBrake;

            _harshCooldown -= dt;
            // 扣除坡度影響後，由駕駛操作造成的加減速度；超過 0.92 m/s² 視為操作過猛（乘客不適）
            float grade = _route.Path.GradeAt(_train.HeadS - _train.Data.TrainLength * 0.5f);
            float a = Mathf.Abs(_train.Physics.Acceleration + 9.81f * grade);
            if (!_train.EmergencyBrake && a > 0.92f && _harshCooldown <= 0f && _train.SpeedKmh > 15f)
            {
                HarshEvents++;
                _harshCooldown = 4f;
                Notice?.Invoke(Loc.T("操作太猛，乘客站不穩了！", "Too harsh! Passengers lost their balance."));
            }
        }

        void CheckObjectives(float speed, float head)
        {
            for (int i = 0; i < _done.Length; i++)
            {
                if (_done[i]) continue;
                var o = Data.objectives[i];
                bool complete = false;
                switch (o.type)
                {
                    case ObjectiveType.StartMoving:
                        complete = speed > 2f;
                        break;
                    case ObjectiveType.ReachSpeed:
                        complete = speed >= o.value - 0.5f;
                        break;
                    case ObjectiveType.EnterLandmark:
                    {
                        var l = _route.GetLandmark(o.landmarkId);
                        complete = l != null && head >= l.startS;
                        break;
                    }
                    case ObjectiveType.PassLandmark:
                    {
                        var l = _route.GetLandmark(o.landmarkId);
                        complete = l != null && _train.TailS >= l.endS;
                        break;
                    }
                    case ObjectiveType.ArriveStation:
                        complete = head >= Destination.PlatformStartS;
                        break;
                    case ObjectiveType.StopAtMarker:
                    case ObjectiveType.ObeySpeedLimits:
                        break; // 由 CheckArrival / 結算判定
                }
                if (complete)
                {
                    _done[i] = true;
                    Notice?.Invoke("✓ " + Loc.T(o.labelZh, o.labelEn));
                }
            }
        }

        void CheckArrival(float head, float dt)
        {
            float error = head - Destination.stopS; // >0 超過
            if (error > Data.overrunFail)
            {
                Fail(Loc.T("列車衝過停車位置，未能停靠月台。", "The train overran the platform."));
                return;
            }

            bool inWindow = error >= -Data.stopShortAllowance;
            if (_train.IsStopped && inWindow && !_train.EmergencyBrake)
            {
                _stoppedTimer += dt;
                if (_stoppedTimer >= 1.5f) Complete(error);
            }
            else
            {
                _stoppedTimer = 0f;
                if (_train.IsStopped && head > Destination.PlatformStartS && !inWindow && !_shortStopNoticeShown)
                {
                    _shortStopNoticeShown = true;
                    Notice?.Invoke(Loc.T("還沒到停車位置，請慢慢向前移動。", "Not at the stop marker yet. Move forward slowly."));
                }
            }
        }

        void Complete(float stopError)
        {
            for (int i = 0; i < _done.Length; i++)
            {
                var t = Data.objectives[i].type;
                if (t == ObjectiveType.StopAtMarker) _done[i] = true;
                if (t == ObjectiveType.ObeySpeedLimits) _done[i] = Violations == 0;
            }
            var r = new MissionResult
            {
                success = true,
                elapsed = Elapsed,
                timeLimit = Data.timeLimit,
                stopError = stopError,
                violations = Violations,
                emergencyUses = EmergencyUses,
                harshEvents = HarshEvents
            };
            MissionScoring.Evaluate(ref r, Data);
            Finish(MissionState.Completed, r);
        }

        void Fail(string reason)
        {
            _train.SetEmergency(true);
            var r = new MissionResult
            {
                success = false,
                failReason = reason,
                elapsed = Elapsed,
                timeLimit = Data.timeLimit,
                violations = Violations,
                emergencyUses = EmergencyUses,
                harshEvents = HarshEvents
            };
            MissionScoring.Evaluate(ref r, Data);
            Finish(MissionState.Failed, r);
        }

        void Finish(MissionState state, MissionResult r)
        {
            State = state;
            Result = r;
            _train.InputLocked = true;
            Finished?.Invoke(r);
        }
    }
}
