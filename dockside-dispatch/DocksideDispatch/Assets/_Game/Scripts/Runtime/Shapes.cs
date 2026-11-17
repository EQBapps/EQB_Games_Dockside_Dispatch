using System.Collections.Generic;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Builds the prototype's low-poly look from simple meshes, sharing one material per colour.
    /// Final art will replace these with modelled assets; the layout code stays the same.
    /// </summary>
    public static class Shapes
    {
        private static Palette _palette;
        private static readonly Dictionary<(Color, bool), Material> Materials = new Dictionary<(Color, bool), Material>();
        private static Mesh _cube, _cylinder, _sphere, _prism, _quad;

        public static void Init(Palette palette)
        {
            _palette = palette;
            Materials.Clear();
        }

        public static Mesh CubeMesh => _cube ? _cube : (_cube = BuiltIn(PrimitiveType.Cube));
        public static Mesh CylinderMesh => _cylinder ? _cylinder : (_cylinder = BuiltIn(PrimitiveType.Cylinder));
        public static Mesh SphereMesh => _sphere ? _sphere : (_sphere = BuiltIn(PrimitiveType.Sphere));
        public static Mesh QuadMesh => _quad ? _quad : (_quad = BuiltIn(PrimitiveType.Quad));
        public static Mesh PrismMesh => _prism ? _prism : (_prism = BuildPrism());

        private static Mesh BuiltIn(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        public static Material Mat(Color color, bool unlit = false)
        {
            if (Materials.TryGetValue((color, unlit), out var m) && m) return m;
            Material baseMat = unlit ? _palette?.unlitBase : _palette?.litBase;
            if (baseMat != null) m = new Material(baseMat);
            else
            {
                var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
            }
            m.name = (unlit ? "Unlit_" : "Lit_") + ColorUtility.ToHtmlStringRGB(color);
            SetColor(m, color);
            if (!unlit && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            Materials[(color, unlit)] = m;
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static GameObject Make(string name, Mesh mesh, Transform parent, Vector3 localPos, Vector3 scale, Color color,
                                      bool unlit = false, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat(color, unlit);
            r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Color color, bool unlit = false, bool shadows = true)
            => Make(name, CubeMesh, parent, pos, size, color, unlit, shadows);

        /// <summary>Cylinder with its axis along Y; size.y is the full height.</summary>
        public static GameObject Cylinder(string name, Transform parent, Vector3 pos, Vector3 size, Color color, bool unlit = false, bool shadows = true)
            => Make(name, CylinderMesh, parent, pos, new Vector3(size.x, size.y * 0.5f, size.z), color, unlit, shadows);

        public static GameObject Prism(string name, Transform parent, Vector3 pos, Vector3 size, Color color, bool unlit = false, bool shadows = true)
            => Make(name, PrismMesh, parent, pos, size, color, unlit, shadows);

        /// <summary>Flat symbol for a colour (colour-blind support): circle, triangle, square, diamond, plus.</summary>
        public static GameObject Symbol(int colorIndex, Transform parent, Vector3 pos, float size, Color color)
        {
            var root = new GameObject("Symbol");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            const float t = 0.04f; // thickness
            switch (Mathf.Abs(colorIndex) % 5)
            {
                case 0: Cylinder("Circle", root.transform, Vector3.zero, new Vector3(size, t, size), color, false, false); break;
                case 1:
                    var tri = Prism("Triangle", root.transform, Vector3.zero, new Vector3(size, t, size), color, false, false);
                    break;
                case 2: Box("Square", root.transform, Vector3.zero, new Vector3(size * 0.82f, t, size * 0.82f), color, false, false); break;
                case 3:
                    var d = Box("Diamond", root.transform, Vector3.zero, new Vector3(size * 0.7f, t, size * 0.7f), color, false, false);
                    d.transform.localRotation = Quaternion.Euler(0, 45, 0);
                    break;
                default:
                    Box("PlusA", root.transform, Vector3.zero, new Vector3(size, t, size * 0.32f), color, false, false);
                    Box("PlusB", root.transform, Vector3.zero, new Vector3(size * 0.32f, t, size), color, false, false);
                    break;
            }
            return root;
        }

        /// <summary>Triangular prism, base on XZ plane pointing towards +Z, unit size, centred.</summary>
        private static Mesh BuildPrism()
        {
            var a = new Vector3(0f, 0f, 0.5f);
            var b = new Vector3(0.5f, 0f, -0.45f);
            var c = new Vector3(-0.5f, 0f, -0.45f);
            var up = new Vector3(0, 0.5f, 0);
            var down = new Vector3(0, -0.5f, 0);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                int i = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
            void QuadFace(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) { Tri(p0, p1, p2); Tri(p0, p2, p3); }
            Tri(a + up, b + up, c + up);           // top
            Tri(a + down, c + down, b + down);     // bottom
            QuadFace(a + down, a + up, c + up, c + down);
            QuadFace(c + down, c + up, b + up, b + down);
            QuadFace(b + down, b + up, a + up, a + down);
            var mesh = new Mesh { name = "Prism" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
