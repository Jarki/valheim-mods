using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fitness
{
    // All geometry is original, metre-scale Unity Y-up geometry. Wood and deer-hide
    // materials are borrowed from native prefabs at runtime, never redistributed.
    internal sealed class TreadmillVisual : MonoBehaviour
    {
        private const float HalfLength = 1.15f;
        private const float RollerRadius = 0.15f;
        private const float RollerHeight = 0.32f;
        private const int SlatCount = 40;
        private const float LoopLength = 4f * HalfLength + 2f * Mathf.PI * RollerRadius;
        private static Mesh frameMesh;
        private static Mesh slatMesh;
        private static Mesh rollerMesh;
        private static int liveVisuals;
        private static bool releaseRequested;

        [SerializeField] private Transform[] slats = Array.Empty<Transform>();
        [SerializeField] private Transform[] rollers = Array.Empty<Transform>();
        private bool counted;
        private float travel;

        internal static GameObject Build(GameObject prefab, Material wood, Material hide)
        {
            releaseRequested = false;
            EnsureMeshes();
            var root = new GameObject("FitnessWoodwork");
            root.transform.SetParent(prefab.transform, false);
            AddMesh(root.transform, "Frame", frameMesh, wood, Vector3.zero, Vector3.one);
            var visual = root.AddComponent<TreadmillVisual>();
            visual.slats = new Transform[SlatCount];
            for (int i = 0; i < SlatCount; ++i)
                visual.slats[i] = AddMesh(root.transform, "BeltSlat", slatMesh, hide,
                    Vector3.zero, Vector3.one);
            visual.rollers = new Transform[2];
            for (int i = 0; i < 2; ++i)
                visual.rollers[i] = AddMesh(root.transform, "WoodenRoller", rollerMesh, wood,
                    new Vector3(0f, RollerHeight, i == 0 ? -HalfLength : HalfLength), Vector3.one);
            visual.PoseBelt();
            AddCollider(prefab, new Vector3(0f, 0.23f, 0f), new Vector3(1.5f, 0.46f, 2.65f));
            for (int side = -1; side <= 1; side += 2)
            {
                AddCollider(prefab, new Vector3(side * 0.83f, 0.4f, 0f), new Vector3(0.18f, 0.3f, 2.95f));
                AddCollider(prefab, new Vector3(side * 0.83f, 1.08f, 0.95f), new Vector3(0.14f, 1.4f, 0.14f));
                AddCollider(prefab, new Vector3(side * 0.83f, 1.56f, 0.2f), new Vector3(0.12f, 0.12f, 1.65f));
            }
            AddCollider(prefab, new Vector3(0f, 1.76f, 0.95f), new Vector3(1.8f, 0.14f, 0.14f));
            return root;
        }

        private static Transform AddMesh(Transform parent, string name, Mesh mesh, Material wood,
            Vector3 position, Vector3 scale)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = wood;
            return part.transform;
        }

        private static void AddCollider(GameObject root, Vector3 center, Vector3 size)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private void Awake()
        {
            counted = true;
            ++liveVisuals;
        }

        internal void Advance(float speed, float dt)
        {
            travel = Mathf.Repeat(travel + speed * dt, LoopLength);
            PoseBelt();
            float angle = -travel / RollerRadius * Mathf.Rad2Deg;
            for (int i = 0; i < rollers.Length; ++i)
                rollers[i].localRotation = Quaternion.AngleAxis(angle, Vector3.right);
        }

        private void PoseBelt()
        {
            for (int i = 0; i < slats.Length; ++i)
            {
                float distance = Mathf.Repeat(i * LoopLength / SlatCount + travel, LoopLength);
                float z;
                float y;
                float angle;
                float straight = 2f * HalfLength;
                float arc = Mathf.PI * RollerRadius;
                if (distance < straight)
                {
                    z = HalfLength - distance;
                    y = RollerHeight + RollerRadius;
                    angle = 0f;
                }
                else if (distance < straight + arc)
                {
                    float theta = (distance - straight) / RollerRadius;
                    z = -HalfLength - Mathf.Sin(theta) * RollerRadius;
                    y = RollerHeight + Mathf.Cos(theta) * RollerRadius;
                    angle = theta * Mathf.Rad2Deg;
                }
                else if (distance < 2f * straight + arc)
                {
                    z = -HalfLength + distance - straight - arc;
                    y = RollerHeight - RollerRadius;
                    angle = 180f;
                }
                else
                {
                    float theta = (distance - 2f * straight - arc) / RollerRadius;
                    z = HalfLength + Mathf.Sin(theta) * RollerRadius;
                    y = RollerHeight - Mathf.Cos(theta) * RollerRadius;
                    angle = 180f + theta * Mathf.Rad2Deg;
                }
                slats[i].localPosition = new Vector3(0f, y, z);
                slats[i].localRotation = Quaternion.AngleAxis(-angle, Vector3.right);
            }
        }

        private static void EnsureMeshes()
        {
            if (frameMesh) return;
            var frame = new Geometry();
            for (int side = -1; side <= 1; side += 2)
            {
                frame.Box(new Vector3(side * 0.83f, 0.4f, 0f), new Vector3(0.18f, 0.26f, 2.95f));
                for (int end = -1; end <= 1; end += 2)
                    frame.Box(new Vector3(side * 0.83f, 0.13f, end * 1.08f), new Vector3(0.26f, 0.26f, 0.42f));
                frame.Box(new Vector3(side * 0.83f, 1.08f, 0.95f), new Vector3(0.14f, 1.4f, 0.14f));
                frame.Box(new Vector3(side * 0.83f, 1.56f, 0.2f), new Vector3(0.12f, 0.12f, 1.65f));
                frame.Box(new Vector3(side * 0.83f, 1.01f, -0.56f), new Vector3(0.1f, 1.1f, 0.1f));
            }
            frame.Box(new Vector3(0f, 1.76f, 0.95f), new Vector3(1.8f, 0.14f, 0.14f));
            frame.Box(new Vector3(0f, 0.2f, -1.05f), new Vector3(1.7f, 0.14f, 0.18f));
            frame.Box(new Vector3(0f, 0.2f, 1.05f), new Vector3(1.7f, 0.14f, 0.18f));
            frameMesh = frame.Finish("FitnessOriginalWoodFrame");
            var slat = new Geometry();
            slat.Box(Vector3.zero, new Vector3(1.42f, 0.055f, 0.115f));
            // The native hide01 texture has a cutout outline; sample only its opaque body.
            slat.MapBoxUV(new Rect(36f / 128f, 24f / 128f, 56f / 128f, 64f / 128f));
            slatMesh = slat.Finish("FitnessOriginalBeltSlat");
            var roller = new Geometry();
            roller.CylinderX(1.55f, RollerRadius, 16);
            rollerMesh = roller.Finish("FitnessOriginalWoodRoller");
        }

        internal static void Shutdown()
        {
            releaseRequested = true;
            ReleaseUnusedMeshes();
        }

        private void OnDestroy()
        {
            if (counted)
            {
                counted = false;
                --liveVisuals;
            }
            ReleaseUnusedMeshes();
        }

        private static void ReleaseUnusedMeshes()
        {
            // Existing built pieces continue to own their shared geometry during unload.
            if (!releaseRequested || liveVisuals != 0) return;
            if (frameMesh) Object.Destroy(frameMesh);
            if (slatMesh) Object.Destroy(slatMesh);
            if (rollerMesh) Object.Destroy(rollerMesh);
            frameMesh = slatMesh = rollerMesh = null;
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
                float width, float height)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; ++i) normals.Add(normal);
                uv.Add(Vector2.zero); uv.Add(new Vector2(width, 0f));
                uv.Add(new Vector2(width, height)); uv.Add(new Vector2(0f, height));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }

            internal void Box(Vector3 center, Vector3 size)
            {
                Vector3 a = center - size * 0.5f;
                Vector3 b = center + size * 0.5f;
                Quad(new Vector3(a.x,b.y,a.z), new Vector3(a.x,b.y,b.z), new Vector3(b.x,b.y,b.z), new Vector3(b.x,b.y,a.z), Vector3.up, size.z, size.x);
                Quad(new Vector3(a.x,a.y,b.z), new Vector3(a.x,a.y,a.z), new Vector3(b.x,a.y,a.z), new Vector3(b.x,a.y,b.z), Vector3.down, size.z, size.x);
                Quad(new Vector3(a.x,a.y,a.z), new Vector3(a.x,b.y,a.z), new Vector3(b.x,b.y,a.z), new Vector3(b.x,a.y,a.z), Vector3.back, size.y, size.x);
                Quad(new Vector3(b.x,a.y,b.z), new Vector3(b.x,b.y,b.z), new Vector3(a.x,b.y,b.z), new Vector3(a.x,a.y,b.z), Vector3.forward, size.y, size.x);
                Quad(new Vector3(a.x,a.y,b.z), new Vector3(a.x,b.y,b.z), new Vector3(a.x,b.y,a.z), new Vector3(a.x,a.y,a.z), Vector3.left, size.y, size.z);
                Quad(new Vector3(b.x,a.y,a.z), new Vector3(b.x,b.y,a.z), new Vector3(b.x,b.y,b.z), new Vector3(b.x,a.y,b.z), Vector3.right, size.y, size.z);
            }

            internal void MapBoxUV(Rect region)
            {
                for (int first = 0; first < uv.Count; first += 4)
                {
                    uv[first] = new Vector2(region.xMin, region.yMin);
                    uv[first + 1] = new Vector2(region.xMax, region.yMin);
                    uv[first + 2] = new Vector2(region.xMax, region.yMax);
                    uv[first + 3] = new Vector2(region.xMin, region.yMax);
                }
            }

            internal void CylinderX(float length, float radius, int sides)
            {
                for (int i = 0; i < sides; ++i)
                {
                    float a = i * 2f * Mathf.PI / sides;
                    float b = (i + 1) * 2f * Mathf.PI / sides;
                    Vector3 leftA = new Vector3(-length * 0.5f, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                    Vector3 leftB = new Vector3(-length * 0.5f, Mathf.Cos(b) * radius, Mathf.Sin(b) * radius);
                    Vector3 rightA = leftA + Vector3.right * length;
                    Vector3 rightB = leftB + Vector3.right * length;
                    Vector3 normal = new Vector3(0f, Mathf.Cos((a+b)*0.5f), Mathf.Sin((a+b)*0.5f));
                    Quad(leftA, leftB, rightB, rightA, normal, radius * (b-a), length);
                    Triangle(new Vector3(-length * 0.5f,0f,0f), leftB, leftA, Vector3.left);
                    Triangle(new Vector3(length * 0.5f,0f,0f), rightA, rightB, Vector3.right);
                }
            }

            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(b.y,b.z)); uv.Add(new Vector2(c.y,c.z));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }

            internal Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
