using UnityEngine;

namespace RailwayMission.Train
{
    /// <summary>
    /// 火車物理（與畫面更新頻率分離，以固定時間步長運算）。
    /// v_new = v_old + a·Δt，a = (牽引力 − 阻力 − 坡度阻力 − 煞車力) / 質量。
    /// 整列火車視為單一質點，車廂依序沿路徑跟隨。
    /// </summary>
    public class TrainPhysics
    {
        public const float FixedStep = 0.02f; // 50 Hz
        const float G = 9.81f;

        readonly TrainData _data;

        /// <summary>速度（m/s，永遠 ≥ 0，本版不模擬倒退）。</summary>
        public float Velocity { get; private set; }
        /// <summary>目前加速度（m/s²）。</summary>
        public float Acceleration { get; private set; }
        /// <summary>實際牽引輸出（0~1，含馬達爬升延遲）。</summary>
        public float TractionLevel { get; private set; }
        /// <summary>實際煞車缸壓力（0~1，含空氣煞車延遲）。</summary>
        public float BrakeLevel { get; private set; }
        public bool EmergencyActive { get; private set; }
        public float TractionForceKN { get; private set; }
        public float BrakeForceKN { get; private set; }

        public float SpeedKmh => Velocity * 3.6f;

        public TrainPhysics(TrainData data)
        {
            _data = data;
        }

        public void Reset(float velocity = 0f, float brakeLevel = 1f)
        {
            Velocity = velocity;
            Acceleration = 0f;
            TractionLevel = 0f;
            BrakeLevel = brakeLevel;
            EmergencyActive = false;
        }

        /// <param name="throttle">油門 0~1</param>
        /// <param name="brake">煞車 0~1</param>
        /// <param name="emergency">緊急煞車</param>
        /// <param name="grade">坡度 sin θ（上坡為正）</param>
        public void Step(float dt, float throttle, float brake, bool emergency, float grade)
        {
            EmergencyActive = emergency;

            // 煞車作用中時切斷牽引（安全聯鎖），緊急煞車時強制全煞
            float tractionTarget = (emergency || brake > 0.05f) ? 0f : Mathf.Clamp01(throttle);
            float brakeTarget = emergency ? 1f : Mathf.Clamp01(brake);

            TractionLevel = Mathf.MoveTowards(TractionLevel, tractionTarget,
                (tractionTarget > TractionLevel ? _data.tractionResponse : _data.tractionResponse * 2f) * dt);
            float brakeRate = brakeTarget > BrakeLevel ? _data.brakeApplyRate * (emergency ? 2.5f : 1f) : _data.brakeReleaseRate;
            BrakeLevel = Mathf.MoveTowards(BrakeLevel, brakeTarget, brakeRate * dt);

            float m = _data.TotalMassKg;
            float v = Velocity;

            // 牽引力：低速受最大牽引力限制，高速受功率限制 F = P / v
            float maxForce = _data.maxTractionKN * 1000f;
            float powerForce = v > 0.5f ? _data.maxPowerKW * 1000f / v : maxForce;
            float traction = Mathf.Min(maxForce, powerForce) * TractionLevel;
            // 接近最高速度時馬達控制逐漸降低輸出
            float vmax = _data.maxSpeedKmh / 3.6f;
            traction *= Mathf.Clamp01((vmax - v) / 2f);

            // 行駛阻力（Davis）
            float resistance = (_data.davisA + _data.davisB * v + _data.davisC * v * v) * 1000f;
            if (v < 0.01f) resistance = 0f;

            // 坡度
            float gradeForce = m * G * grade;

            // 煞車力
            float decel = EmergencyActive ? _data.emergencyBrakeDecel : _data.serviceBrakeDecel;
            float brakeForce = m * decel * BrakeLevel;

            TractionForceKN = traction / 1000f;
            BrakeForceKN = brakeForce / 1000f;

            float net = traction - resistance - gradeForce;
            if (v <= 0.001f)
            {
                // 靜止：只有當淨力超過煞車與坡度時才會開始移動
                if (net > brakeForce)
                {
                    float a = (net - brakeForce) / m;
                    Velocity = a * dt;
                    Acceleration = a;
                }
                else
                {
                    Velocity = 0f;
                    Acceleration = 0f;
                }
                return;
            }

            float accel = (net - brakeForce) / m;
            float newV = v + accel * dt;
            if (newV < 0f) newV = 0f; // 煞停（本版不模擬溜車倒退）
            Acceleration = (newV - v) / dt;
            Velocity = newV;
        }
    }
}
