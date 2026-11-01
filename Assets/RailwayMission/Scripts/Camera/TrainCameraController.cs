using RailwayMission.Track;
using RailwayMission.Train;
using UnityEngine;

namespace RailwayMission.Cameras
{
    public enum CameraMode { Cab, Nose, Chase }

    /// <summary>
    /// 三種視角：駕駛室（第一人稱）、車頭（車鼻前方）、追蹤（第三人稱）。
    /// 駕駛室視角會隨速度輕微晃動；追蹤視角可拖曳旋轉，進入隧道時自動降低高度避免穿模。
    /// </summary>
    public class TrainCameraController : MonoBehaviour
    {
        public CameraMode Mode { get; private set; } = CameraMode.Cab;

        Camera _cam;
        TrainController _train;
        TrackRoute _route;

        float _chaseYaw;      // 追蹤視角水平角度（度）
        float _chasePitch = 14f;
        float _chaseDistance = 34f;
        float _lookYaw;       // 駕駛室內轉頭
        float _swayTime;
        Vector3 _chaseVelocity;

        public void Init(Camera cam, TrainController train, TrackRoute route)
        {
            _cam = cam;
            _train = train;
            _route = route;
            _cam.nearClipPlane = 0.08f;
            _cam.farClipPlane = 1700f;
            _cam.fieldOfView = 58f;
            SnapToTarget();
        }

        public void SetMode(CameraMode mode)
        {
            Mode = mode;
            _lookYaw = 0f;
            SnapToTarget();
        }

        public void NextMode() => SetMode((CameraMode)(((int)Mode + 1) % 3));

        /// <summary>UI 拖曳畫面時呼叫（像素差量）。</summary>
        public void Drag(Vector2 delta)
        {
            if (Mode == CameraMode.Chase)
            {
                _chaseYaw += delta.x * 0.25f;
                _chasePitch = Mathf.Clamp(_chasePitch - delta.y * 0.15f, 2f, 60f);
            }
            else if (Mode == CameraMode.Cab)
            {
                _lookYaw = Mathf.Clamp(_lookYaw + delta.x * 0.2f, -70f, 70f);
            }
        }

        public void Zoom(float delta)
        {
            _chaseDistance = Mathf.Clamp(_chaseDistance - delta, 14f, 90f);
        }

        void SnapToTarget()
        {
            if (_train == null) return;
            UpdateCamera(true);
        }

        void LateUpdate()
        {
            if (_train == null) return;
            UpdateCamera(false);
        }

        void UpdateCamera(bool snap)
        {
            Transform car = _train.LeadCar;
            float v = _train.Physics.Velocity;
            switch (Mode)
            {
                case CameraMode.Cab:
                {
                    _swayTime += Time.deltaTime * (0.8f + v * 0.08f);
                    float amp = Mathf.Clamp01(v / 30f);
                    Vector3 sway = new Vector3(Mathf.Sin(_swayTime * 1.7f) * 0.012f, Mathf.Sin(_swayTime * 3.1f) * 0.008f, 0) * amp;
                    var anchor = _train.CabAnchor;
                    transform.position = anchor.position + anchor.rotation * sway;
                    // 煞車／加速時頭部微微前傾後仰
                    float pitch = -_train.Physics.Acceleration * 0.6f + 2.5f;
                    transform.rotation = anchor.rotation * Quaternion.Euler(pitch, _lookYaw, Mathf.Sin(_swayTime * 1.3f) * 0.25f * amp);
                    break;
                }
                case CameraMode.Nose:
                {
                    var anchor = _train.NoseAnchor;
                    transform.position = anchor.position;
                    transform.rotation = anchor.rotation * Quaternion.Euler(3f, 0, 0);
                    break;
                }
                case CameraMode.Chase:
                {
                    bool inTunnel = _route.IsInside(LandmarkType.Tunnel, _train.HeadS - 20f, 25f);
                    float dist = _chaseDistance;
                    float pitch = inTunnel ? 6f : _chasePitch;
                    // 以車頭後方約一節車廂處為目標
                    Vector3 target = car.position + Vector3.up * 2.2f - car.forward * 4f;
                    Quaternion orbit = Quaternion.LookRotation(car.forward, Vector3.up) * Quaternion.Euler(pitch, _chaseYaw, 0);
                    Vector3 desired = target - orbit * Vector3.forward * dist;
                    if (inTunnel)
                    {
                        // 隧道內：位於車頂與隧道拱頂之間，沿軌道中心線跟隨
                        Vector3 p = _route.Path.PositionAt(_train.HeadS - 28f);
                        desired = p + Vector3.up * 5.9f;
                    }
                    transform.position = snap ? desired : Vector3.SmoothDamp(transform.position, desired, ref _chaseVelocity, inTunnel ? 0.05f : 0.25f);
                    transform.rotation = Quaternion.LookRotation(target - transform.position, Vector3.up);
                    break;
                }
            }
        }
    }
}
