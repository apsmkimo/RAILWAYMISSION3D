using System.Collections.Generic;
using UnityEngine;

namespace RailwayMission.Core
{
    /// <summary>
    /// 材質庫。從 Unity 內建的預設材質複製出各種顏色的材質，
    /// 因此不論專案使用 Built-in 或 URP 都能正確顯示，也不需要 Shader.Find（避免 Android 打包時被剝除）。
    /// 之後換成正式美術資源時，只要在這裡回傳美術做好的材質即可。
    /// </summary>
    public static class MaterialLibrary
    {
        static Material _base;
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        static Material Base
        {
            get
            {
                if (_base != null) return _base;
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _base = new Material(probe.GetComponent<Renderer>().sharedMaterial) { name = "RM_Base" };
                Object.Destroy(probe);
                return _base;
            }
        }

        public static void ClearCache()
        {
            Cache.Clear();
            _base = null;
        }

        /// <summary>取得指定顏色的材質。smoothness 0 = 霧面、1 = 光亮。</summary>
        public static Material Get(string key, Color color, float smoothness = 0.2f, float metallic = 0f, Color? emission = null)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Base) { name = "RM_" + key, enableInstancing = true };
            SetColor(m, color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", emission.Value);
            }
            Cache[key] = m;
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); // URP
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);         // Built-in
        }

        // ───── 常用材質（寫實色調） ─────
        public static Material Grass => Get("grass", new Color(0.30f, 0.42f, 0.20f), 0.05f);
        public static Material Rock => Get("rock", new Color(0.42f, 0.40f, 0.37f), 0.1f);
        public static Material Ballast => Get("ballast", new Color(0.45f, 0.43f, 0.40f), 0.05f);
        public static Material Sleeper => Get("sleeper", new Color(0.55f, 0.54f, 0.52f), 0.1f); // 混凝土枕木
        public static Material Rail => Get("rail", new Color(0.55f, 0.52f, 0.50f), 0.75f, 0.9f);
        public static Material Steel => Get("steel", new Color(0.30f, 0.32f, 0.35f), 0.4f, 0.6f);
        public static Material Concrete => Get("concrete", new Color(0.66f, 0.65f, 0.62f), 0.15f);
        public static Material DarkConcrete => Get("concrete_dark", new Color(0.22f, 0.22f, 0.22f), 0.1f);
        public static Material Water => Get("water", new Color(0.16f, 0.32f, 0.38f), 0.9f, 0.1f);
        public static Material TreeTrunk => Get("trunk", new Color(0.30f, 0.22f, 0.15f), 0.05f);
        public static Material TreeLeaf => Get("leaf", new Color(0.16f, 0.32f, 0.14f), 0.05f);
        public static Material TreeLeafLight => Get("leaf_light", new Color(0.26f, 0.42f, 0.18f), 0.05f);
        public static Material Asphalt => Get("asphalt", new Color(0.18f, 0.18f, 0.19f), 0.1f);
        public static Material Yellow => Get("yellow", new Color(0.95f, 0.78f, 0.10f), 0.3f);
        public static Material White => Get("white", new Color(0.92f, 0.92f, 0.90f), 0.3f);
        public static Material Red => Get("red", new Color(0.75f, 0.12f, 0.10f), 0.3f);
        public static Material Roof => Get("roof", new Color(0.35f, 0.38f, 0.42f), 0.3f, 0.3f);
        public static Material TunnelLamp => Get("tunnel_lamp", new Color(1f, 0.85f, 0.5f), 0.5f, 0f, new Color(1.6f, 1.2f, 0.6f));

        // 電聯車（白色車體、藍色腰帶，類似台灣通勤電聯車配色但為原創）
        public static Material TrainBody => Get("train_body", new Color(0.90f, 0.91f, 0.92f), 0.6f, 0.2f);
        public static Material TrainStripe => Get("train_stripe", new Color(0.06f, 0.30f, 0.62f), 0.6f, 0.1f);
        public static Material TrainWindow => Get("train_window", new Color(0.05f, 0.07f, 0.09f), 0.95f, 0.3f);
        public static Material TrainUnder => Get("train_under", new Color(0.12f, 0.12f, 0.13f), 0.2f, 0.4f);
        public static Material Headlight => Get("headlight", Color.white, 0.9f, 0f, new Color(2.5f, 2.4f, 2.0f));
        public static Material CabDash => Get("cab_dash", new Color(0.16f, 0.17f, 0.19f), 0.3f);
    }
}
