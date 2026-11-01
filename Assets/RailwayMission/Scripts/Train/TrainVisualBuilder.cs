using RailwayMission.Core;
using UnityEngine;

namespace RailwayMission.Train
{
    public enum CarRole { LeadCab, Middle, Pantograph, TailCab }

    /// <summary>
    /// 以程序化網格建出原創電聯車外觀（白色車體＋藍色腰帶）。
    /// 正式美術完成後，可改為載入 Blender 製作的 Prefab，TrainController 不需修改。
    /// 車廂座標：原點 = 車廂中心的軌面高度，+Z 為前進方向。
    /// </summary>
    public static class TrainVisualBuilder
    {
        // submesh 索引
        const float Gauge = 1.067f;
        const int Body = 0, Stripe = 1, Window = 2, Under = 3, Roof = 4, Light = 5, Tail = 6, Dash = 7;

        public static Material[] Materials => new[]
        {
            MaterialLibrary.TrainBody, MaterialLibrary.TrainStripe, MaterialLibrary.TrainWindow,
            MaterialLibrary.TrainUnder, MaterialLibrary.Roof, MaterialLibrary.Headlight,
            MaterialLibrary.Get("taillight", new Color(0.8f, 0.05f, 0.05f), 0.6f, 0f, new Color(1.5f, 0.05f, 0.05f)),
            MaterialLibrary.CabDash
        };

        public static GameObject BuildCar(TrainData d, CarRole role, Transform parent, string name)
        {
            var mb = new MeshBuilder(8);
            float L = d.carLength;
            float W = d.carWidth;
            float halfL = L * 0.5f;
            const float floorY = 1.05f;
            float roofY = d.carHeight - 0.3f;
            float bodyH = roofY - floorY;

            // 車體
            mb.AddBox(Body, new Vector3(0, floorY + bodyH * 0.5f, 0), Quaternion.identity, new Vector3(W, bodyH, L));
            // 車頂（略窄、圓弧感以兩層表現）
            mb.AddBox(Roof, new Vector3(0, roofY + 0.12f, 0), Quaternion.identity, new Vector3(W - 0.25f, 0.24f, L - 0.3f));
            mb.AddBox(Roof, new Vector3(0, roofY + 0.27f, 0), Quaternion.identity, new Vector3(W - 1.0f, 0.1f, L - 1.0f));
            // 冷氣機組
            for (int i = -1; i <= 1; i += 2)
                mb.AddBox(Roof, new Vector3(0, roofY + 0.45f, i * L * 0.25f), Quaternion.identity, new Vector3(1.8f, 0.35f, 3.2f));

            // 腰帶（兩側）
            float sideX = W * 0.5f + 0.01f;
            for (int side = -1; side <= 1; side += 2)
            {
                mb.AddBox(Stripe, new Vector3(side * sideX, 1.40f, 0), Quaternion.identity, new Vector3(0.02f, 0.22f, L - 0.05f));
                mb.AddBox(Stripe, new Vector3(side * sideX, 2.98f, 0), Quaternion.identity, new Vector3(0.02f, 0.06f, L - 0.05f));
            }

            // 側窗與車門（每側 3 門）
            float[] doors = { -L * 0.32f, 0f, L * 0.32f };
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (W * 0.5f + 0.015f);
                foreach (float dz in doors)
                {
                    mb.AddBox(Under, new Vector3(x, floorY + 1.0f, dz), Quaternion.identity, new Vector3(0.02f, 2.0f, 1.35f));
                    mb.AddBox(Window, new Vector3(side * (W * 0.5f + 0.025f), floorY + 1.45f, dz - 0.33f), Quaternion.identity, new Vector3(0.02f, 0.75f, 0.5f));
                    mb.AddBox(Window, new Vector3(side * (W * 0.5f + 0.025f), floorY + 1.45f, dz + 0.33f), Quaternion.identity, new Vector3(0.02f, 0.75f, 0.5f));
                }
                // 門與門之間的窗戶
                for (int k = 0; k < doors.Length + 1; k++)
                {
                    float z0 = k == 0 ? -halfL + 1.6f : doors[k - 1] + 0.9f;
                    float z1 = k == doors.Length ? halfL - 1.6f : doors[k] - 0.9f;
                    if (z1 - z0 < 0.8f) continue;
                    mb.AddBox(Window, new Vector3(x, 2.35f, (z0 + z1) * 0.5f), Quaternion.identity, new Vector3(0.02f, 0.85f, z1 - z0));
                }
            }

            // 車底設備與裙板
            mb.AddBox(Under, new Vector3(0, 0.75f, 0), Quaternion.identity, new Vector3(W - 0.3f, 0.6f, L - 6f));
            // 轉向架
            for (int b = -1; b <= 1; b += 2)
            {
                float bz = b * d.bogieSpacing * 0.5f;
                mb.AddBox(Under, new Vector3(0, 0.55f, bz), Quaternion.identity, new Vector3(Gauge + 0.5f, 0.4f, 2.6f));
                for (int w = -1; w <= 1; w += 2)
                for (int side = -1; side <= 1; side += 2)
                    mb.AddBox(Under, new Vector3(side * (Gauge * 0.5f), 0.43f, bz + w * 1.05f), Quaternion.identity, new Vector3(0.12f, 0.86f, 0.86f));
            }
            // 連結器／貫通道
            mb.AddBox(Under, new Vector3(0, 1.0f, halfL + 0.2f), Quaternion.identity, new Vector3(0.5f, 0.3f, 0.4f));
            mb.AddBox(Under, new Vector3(0, 1.0f, -halfL - 0.2f), Quaternion.identity, new Vector3(0.5f, 0.3f, 0.4f));
            if (role == CarRole.Middle || role == CarRole.Pantograph)
            {
                mb.AddBox(Under, new Vector3(0, 2.1f, halfL + 0.2f), Quaternion.identity, new Vector3(1.2f, 2.1f, 0.4f));
                mb.AddBox(Under, new Vector3(0, 2.1f, -halfL - 0.2f), Quaternion.identity, new Vector3(1.2f, 2.1f, 0.4f));
            }

            // 駕駛室端
            if (role == CarRole.LeadCab) AddCabEnd(mb, d, +1);
            if (role == CarRole.TailCab) AddCabEnd(mb, d, -1);
            if (role == CarRole.LeadCab) AddCabInterior(mb, d);

            // 集電弓
            if (role == CarRole.Pantograph)
            {
                float py = roofY + 0.35f;
                mb.AddBox(Under, new Vector3(0, py, 2f), Quaternion.identity, new Vector3(1.4f, 0.2f, 1.4f));
                mb.AddBeam(Under, new Vector3(-0.5f, py + 0.1f, 2.5f), new Vector3(0, py + 1.1f, 1.6f), 0.06f, 0.06f, Vector3.up);
                mb.AddBeam(Under, new Vector3(0.5f, py + 0.1f, 2.5f), new Vector3(0, py + 1.1f, 1.6f), 0.06f, 0.06f, Vector3.up);
                mb.AddBeam(Under, new Vector3(0, py + 1.1f, 1.6f), new Vector3(0, PantographHeadY(d), 2.2f), 0.06f, 0.06f, Vector3.up);
                mb.AddBox(Under, new Vector3(0, PantographHeadY(d), 2.2f), Quaternion.identity, new Vector3(1.6f, 0.05f, 0.25f));
            }

            var go = mb.ToGameObject(name, parent, Materials);
            return go;
        }

        /// <summary>集電弓頂端高度（接觸線高度），與 WorldBuilder 的電車線一致。</summary>
        public static float PantographHeadY(TrainData d) => 5.4f;

        static void AddCabEnd(MeshBuilder mb, TrainData d, int dir)
        {
            float halfL = d.carLength * 0.5f;
            float z = dir * (halfL + 0.02f);
            float W = d.carWidth;
            // 前擋風玻璃
            AddFacingQuad(mb, Window, new Vector3(0, 2.6f, z + dir * 0.01f), W - 0.5f, 0.95f, dir);
            // 前方腰帶
            mb.AddBox(Stripe, new Vector3(0, 1.40f, z), Quaternion.identity, new Vector3(W, 0.22f, 0.04f));
            // 車頭燈／尾燈
            int lightSubmesh = dir > 0 ? Light : Tail;
            for (int side = -1; side <= 1; side += 2)
                mb.AddBox(lightSubmesh, new Vector3(side * 0.95f, 1.75f, z + dir * 0.01f), Quaternion.identity, new Vector3(0.35f, 0.16f, 0.04f));
            // 排障器
            mb.AddBox(Under, new Vector3(0, 0.35f, dir * (halfL - 0.2f)), Quaternion.identity, new Vector3(W - 0.6f, 0.4f, 0.15f));
            // 車頭上方目的地顯示器
            AddFacingQuad(mb, Window, new Vector3(0, 3.22f, z + dir * 0.01f), 1.2f, 0.22f, dir);
        }

        /// <summary>駕駛室內裝：儀表台、窗框、天花板，讓第一人稱視角有駕駛室的包覆感。</summary>
        static void AddCabInterior(MeshBuilder mb, TrainData d)
        {
            float front = d.carLength * 0.5f;
            float W = d.carWidth;
            // 儀表台
            mb.AddBox(Dash, new Vector3(0, 1.95f, front - 0.45f), Quaternion.Euler(-12f, 0, 0), new Vector3(W - 0.3f, 0.35f, 0.7f));
            // 儀表台上的計器
            for (int i = -1; i <= 1; i++)
                mb.AddBox(Under, new Vector3(-0.5f + i * 0.32f, 2.18f, front - 0.62f), Quaternion.Euler(-35f, 0, 0), new Vector3(0.26f, 0.22f, 0.03f));
            // 窗框立柱
            for (int side = -1; side <= 1; side += 2)
                mb.AddBox(Dash, new Vector3(side * (W * 0.5f - 0.18f), 2.6f, front - 0.12f), Quaternion.identity, new Vector3(0.2f, 1.3f, 0.12f));
            // 擋風玻璃上緣與天花板
            mb.AddBox(Dash, new Vector3(0, 3.16f, front - 0.15f), Quaternion.identity, new Vector3(W - 0.1f, 0.18f, 0.2f));
            mb.AddBox(Dash, new Vector3(0, 3.28f, front - 1.6f), Quaternion.identity, new Vector3(W - 0.1f, 0.06f, 3.0f));
            // 側牆下半部
            for (int side = -1; side <= 1; side += 2)
                mb.AddBox(Dash, new Vector3(side * (W * 0.5f - 0.06f), 1.6f, front - 1.6f), Quaternion.identity, new Vector3(0.06f, 1.1f, 3.0f));
            // 中央窗柱細條
            mb.AddBox(Dash, new Vector3(0, 2.6f, front - 0.08f), Quaternion.identity, new Vector3(0.06f, 0.95f, 0.05f));
        }

        /// <summary>只朝外的單面四邊形（從駕駛室內看出去時不會擋住視線）。</summary>
        static void AddFacingQuad(MeshBuilder mb, int submesh, Vector3 c, float w, float h, int dir)
        {
            float x = w * 0.5f, y = h * 0.5f;
            if (dir > 0)
                mb.AddQuad(submesh, c + new Vector3(x, -y, 0), c + new Vector3(x, y, 0), c + new Vector3(-x, y, 0), c + new Vector3(-x, -y, 0));
            else
                mb.AddQuad(submesh, c + new Vector3(-x, -y, 0), c + new Vector3(-x, y, 0), c + new Vector3(x, y, 0), c + new Vector3(x, -y, 0));
        }
    }
}
