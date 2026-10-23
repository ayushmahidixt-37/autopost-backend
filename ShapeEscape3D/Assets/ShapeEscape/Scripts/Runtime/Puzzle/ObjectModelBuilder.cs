using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Builds the smooth, fully coloured "revealed" model of an everyday object from its template primitives.
    /// Meshes are merged per palette colour (one draw call per colour) and cached per object.
    ///
    /// To use hand-made art instead, place a prefab at Resources/ShapeEscape/Models/{objectId} authored in
    /// template space (Y up, ~2 units tall, same origin as the template). It is used automatically and the
    /// puzzle engine is unaffected.
    /// </summary>
    public static class ObjectModelBuilder
    {
        sealed class CachedModel
        {
            public readonly List<KeyValuePair<int, Mesh>> parts = new List<KeyValuePair<int, Mesh>>();
        }

        static readonly Dictionary<string, CachedModel> Cache = new Dictionary<string, CachedModel>();

        public const string OverrideModelPath = "ShapeEscape/Models/";

        /// <summary>Creates a model GameObject in template space under <paramref name="parent"/>.</summary>
        public static GameObject Build(ObjectTemplate template, Transform parent)
        {
            var root = new GameObject("Model " + template.id);
            root.transform.SetParent(parent, false);

            var prefab = Resources.Load<GameObject>(OverrideModelPath + template.id);
            if (prefab != null)
            {
                Object.Instantiate(prefab, root.transform, false);
                return root;
            }

            var cached = GetOrBuild(template);
            foreach (var part in cached.parts)
            {
                var go = new GameObject("Part " + part.Key);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = part.Value;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = MaterialLibrary.Lit(PuzzleBoardView.PaletteColor(template, part.Key), 0.45f);
            }
            return root;
        }

        /// <summary>
        /// Positions a template-space model so it lines up exactly with the puzzle pieces of <paramref name="board"/>.
        /// </summary>
        public static void AlignToBoard(Transform model, PuzzleBoardView board)
        {
            var def = board.Definition;
            float inv = 1f / Mathf.Max(0.0001f, def.cellSize);
            model.localScale = Vector3.one * inv;
            model.localPosition = -new Vector3(def.origin.x, def.origin.y, def.origin.z) * inv - board.GridCenter;
            model.localRotation = Quaternion.identity;
        }

        /// <summary>Centres a template-space model at its parent's origin and scales it to <paramref name="height"/>.</summary>
        public static void FitToHeight(Transform model, ObjectTemplate template, float height)
        {
            Float3 min, max;
            template.GetSolidBounds(out min, out max);
            var size = new Vector3(max.x - min.x, max.y - min.y, max.z - min.z);
            float s = height / Mathf.Max(0.01f, Mathf.Max(size.y, Mathf.Max(size.x, size.z) * 0.8f));
            var center = new Vector3(min.x + max.x, min.y + max.y, min.z + max.z) * 0.5f;
            model.localScale = Vector3.one * s;
            model.localPosition = -center * s;
            model.localRotation = Quaternion.identity;
        }

        static CachedModel GetOrBuild(ObjectTemplate template)
        {
            CachedModel cached;
            if (Cache.TryGetValue(template.id, out cached) && AllMeshesAlive(cached)) return cached;

            cached = new CachedModel();
            var builders = new SortedDictionary<int, MeshBuilder>();
            foreach (var prim in template.primitives)
            {
                if (prim.role == PrimitiveRole.Carve) continue;
                MeshBuilder mb;
                if (!builders.TryGetValue(prim.palette, out mb))
                {
                    mb = new MeshBuilder();
                    builders[prim.palette] = mb;
                }
                mb.Append(MeshFactory.ForPrimitive(prim), Matrix4x4.identity);
            }
            foreach (var kv in builders)
                cached.parts.Add(new KeyValuePair<int, Mesh>(kv.Key, kv.Value.ToMesh(template.id + "_" + kv.Key)));
            Cache[template.id] = cached;
            return cached;
        }

        static bool AllMeshesAlive(CachedModel m)
        {
            foreach (var p in m.parts) if (p.Value == null) return false;
            return true;
        }
    }
}
