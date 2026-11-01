using RailwayMission.Train;
using UnityEngine;

namespace RailwayMission.Audio
{
    /// <summary>
    /// 火車音效（全部以程式合成，不需音檔）：馬達聲（音高隨速度）、行駛滾動聲、鋼軌接縫「喀噹」聲、喇叭、煞車洩氣聲。
    /// 之後可直接把 AudioClip 換成實際錄音。
    /// </summary>
    public class TrainAudio : MonoBehaviour
    {
        const int Rate = 44100;

        TrainController _train;
        AudioSource _motor, _rolling, _oneShot, _horn, _brake;
        float _prevBrake;

        public void Init(TrainController train)
        {
            _train = train;
            _motor = CreateSource("Motor", CreateMotorClip(), true, 0f);
            _rolling = CreateSource("Rolling", CreateNoiseClip(2f, 0.6f, 0.02f), true, 0f);
            _brake = CreateSource("BrakeAir", CreateNoiseClip(1.2f, 0.35f, 0.5f), false, 0.35f);
            _horn = CreateSource("Horn", CreateHornClip(), false, 0.8f);
            _oneShot = CreateSource("OneShot", null, false, 1f);
            _motor.Play();
            _rolling.Play();

            _train.Horn += () => { if (!_horn.isPlaying) _horn.Play(); };
            _train.RailJoint += _ => PlayJoints();
        }

        AudioSource CreateSource(string name, AudioClip clip, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.volume = volume;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
        }

        void Update()
        {
            if (_train == null) return;
            float v = _train.Physics.Velocity;
            float speed01 = Mathf.Clamp01(v / 33f);
            float traction = _train.Physics.TractionLevel;

            // VVVF 馬達聲：音高隨速度上升，有牽引時較大聲
            _motor.pitch = 0.45f + speed01 * 1.6f;
            _motor.volume = Mathf.Lerp(_motor.volume, (v > 0.1f ? 0.08f : 0f) + traction * 0.35f, Time.deltaTime * 4f);

            _rolling.pitch = 0.6f + speed01 * 0.8f;
            _rolling.volume = Mathf.Lerp(_rolling.volume, speed01 * 0.45f, Time.deltaTime * 3f);

            // 煞車緩解時的洩氣聲
            float brake = _train.Physics.BrakeLevel;
            if (_prevBrake - brake > 0.25f && !_brake.isPlaying) { _brake.Play(); _prevBrake = brake; }
            if (brake > _prevBrake + 0.001f || brake < 0.05f) _prevBrake = brake;
        }

        void PlayJoints()
        {
            if (_train.Physics.Velocity < 1f) return;
            // 一個轉向架的兩組輪對依序通過接縫
            _oneShot.pitch = Random.Range(0.92f, 1.08f);
            _oneShot.PlayOneShot(JointClip, 0.55f);
            float gap = 2.1f / Mathf.Max(_train.Physics.Velocity, 1f);
            if (gap < 0.5f) StartCoroutine(Delayed(gap));
        }

        System.Collections.IEnumerator Delayed(float t)
        {
            yield return new WaitForSeconds(t);
            _oneShot.PlayOneShot(JointClip, 0.45f);
        }

        static AudioClip _jointClip;
        static AudioClip JointClip => _jointClip != null ? _jointClip : (_jointClip = CreateJointClip());

        // ───── 合成 ─────

        static AudioClip CreateMotorClip()
        {
            int n = Rate; // 1 秒循環
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = 0.5f * Mathf.Sin(2 * Mathf.PI * 220f * t)
                        + 0.25f * Mathf.Sin(2 * Mathf.PI * 440f * t)
                        + 0.15f * Mathf.Sin(2 * Mathf.PI * 660f * t)
                        + 0.1f * Mathf.Sin(2 * Mathf.PI * 110f * t);
                data[i] *= 0.6f;
            }
            var clip = AudioClip.Create("Motor", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>低通濾波的雜訊（smoothing 越小越低沉）。</summary>
        static AudioClip CreateNoiseClip(float seconds, float amp, float smoothing)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            var rng = new System.Random(7);
            float y = 0;
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1);
                y += (x - y) * smoothing;
                data[i] = y * amp * (smoothing < 0.1f ? 6f : 1f);
            }
            // 首尾淡入淡出，避免循環爆音
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float f = i / (float)fade;
                data[i] *= f;
                data[n - 1 - i] *= f;
            }
            var clip = AudioClip.Create("Noise", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateJointClip()
        {
            int n = Rate / 6;
            var data = new float[n];
            var rng = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 35f);
                data[i] = env * (0.6f * Mathf.Sin(2 * Mathf.PI * 95f * t) + 0.4f * (float)(rng.NextDouble() * 2 - 1));
            }
            var clip = AudioClip.Create("Joint", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateHornClip()
        {
            int n = (int)(Rate * 1.4f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t * 20f) * Mathf.Clamp01((1.4f - t) * 8f);
                // 雙音氣笛
                float s = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 311f * t)) * 0.3f + Mathf.Sin(2 * Mathf.PI * 370f * t) * 0.5f
                        + Mathf.Sin(2 * Mathf.PI * 622f * t) * 0.15f;
                data[i] = s * env * 0.5f;
            }
            var clip = AudioClip.Create("Horn", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
