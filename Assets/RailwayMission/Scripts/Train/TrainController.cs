using System.Collections.Generic;
using RailwayMission.Track;
using UnityEngine;

namespace RailwayMission.Train
{
    /// <summary>
    /// 火車行駛控制：接收油門／煞車輸入、以固定步長推進物理、沿 Spline 擺放每節車廂。
    /// </summary>
    public class TrainController : MonoBehaviour
    {
        public TrainData Data { get; private set; }
        public TrainPhysics Physics { get; private set; }
        public TrackRoute Route { get; private set; }

        /// <summary>車頭（前端）在路徑上的距離 s（公尺）。</summary>
        public float HeadS { get; private set; }
        public float TailS => HeadS - Data.TrainLength;
        public float SpeedKmh => Physics.SpeedKmh;
        public bool IsStopped => Physics.Velocity < 0.05f;

        // ───── 駕駛輸入（UI 寫入） ─────
        [Range(0, 1)] public float throttleInput;
        [Range(0, 1)] public float brakeInput = 1f;
        public bool EmergencyBrake { get; private set; }
        public bool InputLocked { get; set; } = true;

        /// <summary>車頭駕駛室攝影機錨點。</summary>
        public Transform CabAnchor { get; private set; }
        /// <summary>車頭外部（車鼻）攝影機錨點。</summary>
        public Transform NoseAnchor { get; private set; }
        public Transform LeadCar => _cars.Count > 0 ? _cars[0] : transform;

        public event System.Action Horn;
        public event System.Action<int> RailJoint; // 參數：車廂索引

        readonly List<Transform> _cars = new List<Transform>();
        float _accumulator;
        float _lastJointS;
        Light _headlight;

        public void Build(TrackRoute route, TrainData data, float startHeadS)
        {
            Route = route;
            Data = data;
            Physics = new TrainPhysics(data);

            for (int i = 0; i < data.carCount; i++)
            {
                CarRole role = i == 0 ? CarRole.LeadCab
                    : i == data.carCount - 1 ? CarRole.TailCab
                    : i == 1 ? CarRole.Pantograph : CarRole.Middle;
                var car = TrainVisualBuilder.BuildCar(data, role, transform, $"Car_{i + 1}");
                _cars.Add(car.transform);
            }

            float front = data.carLength * 0.5f;
            CabAnchor = new GameObject("CabAnchor").transform;
            CabAnchor.SetParent(_cars[0], false);
            CabAnchor.localPosition = new Vector3(-0.45f, 2.55f, front - 1.15f);

            NoseAnchor = new GameObject("NoseAnchor").transform;
            NoseAnchor.SetParent(_cars[0], false);
            NoseAnchor.localPosition = new Vector3(0f, 1.6f, front + 1.2f);

            // 車頭燈（隧道內很重要）
            var lightGo = new GameObject("Headlight");
            lightGo.transform.SetParent(_cars[0], false);
            lightGo.transform.localPosition = new Vector3(0, 1.8f, front + 0.3f);
            lightGo.transform.localRotation = Quaternion.Euler(4f, 0, 0);
            _headlight = lightGo.AddComponent<Light>();
            _headlight.type = LightType.Spot;
            _headlight.range = 140f;
            _headlight.spotAngle = 38f;
            _headlight.intensity = 3.5f;
            _headlight.color = new Color(1f, 0.96f, 0.86f);
            _headlight.shadows = LightShadows.None;

            ResetTrain(startHeadS);
        }

        public void ResetTrain(float headS)
        {
            HeadS = headS;
            _lastJointS = headS;
            _accumulator = 0f;
            throttleInput = 0f;
            brakeInput = 1f;
            EmergencyBrake = false;
            Physics.Reset(0f, 1f);
            PlaceCars();
        }

        public void SetEmergency(bool on)
        {
            if (on) { EmergencyBrake = true; throttleInput = 0f; }
            else if (IsStopped) EmergencyBrake = false; // 寫實：緊急煞車需停車後才能解除
        }

        public void SoundHorn() => Horn?.Invoke();

        void Update()
        {
            if (Physics == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _accumulator += Mathf.Min(dt, 0.25f);
            float throttle = InputLocked ? 0f : throttleInput;
            float brake = InputLocked ? 1f : brakeInput;
            while (_accumulator >= TrainPhysics.FixedStep)
            {
                float grade = Route.Path.GradeAt(HeadS - Data.TrainLength * 0.5f);
                Physics.Step(TrainPhysics.FixedStep, throttle, brake, EmergencyBrake, grade);
                HeadS += Physics.Velocity * TrainPhysics.FixedStep;
                _accumulator -= TrainPhysics.FixedStep;
            }

            // 鋼軌接縫聲（每 25 公尺一段軌條）
            if (HeadS - _lastJointS >= 25f)
            {
                _lastJointS += 25f * Mathf.Floor((HeadS - _lastJointS) / 25f);
                RailJoint?.Invoke(0);
            }

            PlaceCars();
        }

        void PlaceCars()
        {
            var path = Route.Path;
            float pitch = Data.carLength + Data.carGap;
            float overhang = (Data.carLength - Data.bogieSpacing) * 0.5f;
            for (int i = 0; i < _cars.Count; i++)
            {
                float carFront = HeadS - i * pitch;
                float sF = carFront - overhang;
                float sR = sF - Data.bogieSpacing;
                Vector3 pF = path.PositionAt(sF);
                Vector3 pR = path.PositionAt(sR);
                Vector3 fwd = pF - pR;
                if (fwd.sqrMagnitude < 1e-6f) fwd = path.TangentAt(sF);
                _cars[i].SetPositionAndRotation((pF + pR) * 0.5f, Quaternion.LookRotation(fwd.normalized, Vector3.up));
            }
        }
    }
}
