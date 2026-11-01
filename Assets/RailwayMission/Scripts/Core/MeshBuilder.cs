using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RailwayMission.Core
{
    /// <summary>
    /// 程序化網格建構工具。支援多個 submesh（每個 submesh 對應一種材質），
    /// 用來把大量小物件（枕木、樹木、電線桿…）合併成少量 draw call，節省 Android 效能。
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<List<int>> _submeshes = new List<List<int>>();

        public int VertexCount => _vertices.Count;

        public MeshBuilder(int submeshCount = 1)
        {
            for (int i = 0; i < submeshCount; i++) _submeshes.Add(new List<int>());
        }

        public int SubmeshCount => _submeshes.Count;

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            foreach (var s in _submeshes) s.Clear();
        }

        public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _uvs.Add(uv);
            return _vertices.Count - 1;
        }

        public void AddTriangle(int submesh, int a, int b, int c)
        {
            var list = _submeshes[submesh];
            list.Add(a);
            list.Add(b);
            list.Add(c);
        }

        /// <summary>加入一個四邊形（順時針 a→b→c→d，從正面看）。</summary>
        public void AddQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(b - a, d - a);
            if (n.sqrMagnitude < 1e-10f) n = Vector3.up;
            n.Normalize();
            int ia = AddVertex(a, n, new Vector2(0, 0));
            int ib = AddVertex(b, n, new Vector2(0, 1));
            int ic = AddVertex(c, n, new Vector2(1, 1));
            int id = AddVertex(d, n, new Vector2(1, 0));
            AddTriangle(submesh, ia, ib, ic);
            AddTriangle(submesh, ia, ic, id);
        }

        /// <summary>加入一個有方向的長方體。</summary>
        public void AddBox(int submesh, Vector3 center, Quaternion rotation, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 r = rotation * new Vector3(h.x, 0, 0);
            Vector3 u = rotation * new Vector3(0, h.y, 0);
            Vector3 f = rotation * new Vector3(0, 0, h.z);

            Vector3 p000 = center - r - u - f;
            Vector3 p100 = center + r - u - f;
            Vector3 p010 = center - r + u - f;
            Vector3 p110 = center + r + u - f;
            Vector3 p001 = center - r - u + f;
            Vector3 p101 = center + r - u + f;
            Vector3 p011 = center - r + u + f;
            Vector3 p111 = center + r + u + f;

            AddQuad(submesh, p000, p010, p110, p100); // back (-f)
            AddQuad(submesh, p101, p111, p011, p001); // front (+f)
            AddQuad(submesh, p001, p011, p010, p000); // left (-r)
            AddQuad(submesh, p100, p110, p111, p101); // right (+r)
            AddQuad(submesh, p010, p011, p111, p110); // top (+u)
            AddQuad(submesh, p001, p000, p100, p101); // bottom (-u)
        }

        /// <summary>加入一個以兩點定義的長方體（例如鐵軌、電線）。</summary>
        public void AddBeam(int submesh, Vector3 from, Vector3 to, float width, float height, Vector3 upHint)
        {
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 1e-4f) return;
            Quaternion rot = Quaternion.LookRotation(dir / len, upHint);
            AddBox(submesh, (from + to) * 0.5f, rot, new Vector3(width, height, len));
        }

        /// <summary>加入圓柱（沿 up 方向）。</summary>
        public void AddCylinder(int submesh, Vector3 baseCenter, float radius, float height, int segments = 8, bool caps = true)
        {
            AddFrustum(submesh, baseCenter, radius, radius, height, segments, caps);
        }

        /// <summary>加入圓錐（樹冠用）。</summary>
        public void AddCone(int submesh, Vector3 baseCenter, float radius, float height, int segments = 8)
        {
            AddFrustum(submesh, baseCenter, radius, 0.0f, height, segments, true);
        }

        public void AddFrustum(int submesh, Vector3 baseCenter, float r0, float r1, float height, int segments, bool caps)
        {
            float slope = (r0 - r1) / Mathf.Max(height, 1e-4f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = baseCenter + d0 * r0;
                Vector3 b1 = baseCenter + d1 * r0;
                Vector3 t0 = baseCenter + d0 * r1 + Vector3.up * height;
                Vector3 t1 = baseCenter + d1 * r1 + Vector3.up * height;
                Vector3 n0 = (d0 + Vector3.up * slope).normalized;
                Vector3 n1 = (d1 + Vector3.up * slope).normalized;
                int ib0 = AddVertex(b0, n0, new Vector2(i / (float)segments, 0));
                int ib1 = AddVertex(b1, n1, new Vector2((i + 1) / (float)segments, 0));
                int it0 = AddVertex(t0, n0, new Vector2(i / (float)segments, 1));
                int it1 = AddVertex(t1, n1, new Vector2((i + 1) / (float)segments, 1));
                AddTriangle(submesh, ib0, it0, it1);
                AddTriangle(submesh, ib0, it1, ib1);
            }

            if (!caps) return;
            // 底面
            int c = AddVertex(baseCenter, Vector3.down, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                int v0 = AddVertex(baseCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r0, Vector3.down, Vector2.zero);
                int v1 = AddVertex(baseCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r0, Vector3.down, Vector2.zero);
                AddTriangle(submesh, c, v0, v1);
            }
            if (r1 > 0.001f)
            {
                Vector3 top = baseCenter + Vector3.up * height;
                int ct = AddVertex(top, Vector3.up, new Vector2(0.5f, 0.5f));
                for (int i = 0; i < segments; i++)
                {
                    float a0 = (i / (float)segments) * Mathf.PI * 2f;
                    float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                    int v0 = AddVertex(top + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r1, Vector3.up, Vector2.zero);
                    int v1 = AddVertex(top + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r1, Vector3.up, Vector2.zero);
                    AddTriangle(submesh, ct, v1, v0);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.subMeshCount = _submeshes.Count;
            for (int i = 0; i < _submeshes.Count; i++) mesh.SetTriangles(_submeshes[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>建立帶有 MeshFilter / MeshRenderer 的 GameObject。</summary>
        public GameObject ToGameObject(string name, Transform parent, Material[] materials, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }
}
