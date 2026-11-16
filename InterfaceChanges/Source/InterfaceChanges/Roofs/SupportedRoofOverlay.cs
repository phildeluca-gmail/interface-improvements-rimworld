using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using InterfaceChanges.Core;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Roofs
{
    // ii-architecture.md section 27. While one or more roof-holding buildings
    // (def.holdsRoof: walls, columns...) are selected, outlines the cells whose roof
    // they support. Vanilla rule, copied from RoofCollapseUtility.WithinRangeOfRoofHolder
    // (decompiled): a cell c is supported by holder S when a flood fill from c, through
    // roofed cells (and c itself) that are within 6.9 cells (straight line, InHorDistOf) of c,
    // reaches a cell that is S or cardinally next to S, and S itself is within 6.9 of c.
    // No Harmony patch. The outline is baked into one Mesh, rebuilt only when the selected
    // set of holders changes; each frame is one DrawMesh.
    public class SupportedRoofOverlay : GameComponent
    {
        private const float Range = RoofCollapseUtility.RoofMaxSupportDistance;
        private static readonly Color RingColor = new Color(0.3f, 0.9f, 1f);

        private static int signature;
        private static Map cachedMap;
        private static Mesh combined;
        private static Mesh staleMesh;
        private static Material ringMaterial;
        private static float ringY;
        private static readonly List<IntVec3> candidates = new List<IntVec3>();
        private static int gridHash;
        private static int lastGridCheckFrame;
        private static readonly List<Building> holders = new List<Building>();

        public SupportedRoofOverlay(Game game)
        {
            // Constructed off the main thread during load: no Unity calls here.
            if (!ReferenceEquals(combined, null)) staleMesh = combined;
            combined = null;
            signature = 0;
            cachedMap = null;
        }

        // Selected spawned holders on the current map, and a signature of that set.
        private static int Collect(Map m)
        {
            holders.Clear();
            int sig = 17;
            List<object> sel = Find.Selector.SelectedObjectsListForReading;
            for (int i = 0; i < sel.Count; i++)
            {
                Building b = sel[i] as Building;
                if (b == null || !b.Spawned || b.Map != m || !b.def.holdsRoof) continue;
                holders.Add(b);
                sig = sig * 31 + b.thingIDNumber;
            }
            return holders.Count == 0 ? 0 : sig;
        }

        // Hash of roofed / No Roof state over the candidate cells (cheap, checked once a second).
        private static int GridHash(Map m)
        {
            int h = 17;
            for (int i = 0; i < candidates.Count; i++)
            {
                IntVec3 c = candidates[i];
                h = h * 31 + (c.Roofed(m) ? 1 : 0) + (m.areaManager.NoRoof[c] ? 2 : 0);
            }
            return h;
        }

        private static void Rebuild(Map m)
        {
            if (combined != null) Object.Destroy(combined);
            combined = null;
            if (holders.Count == 0) return;
            if (ringMaterial == null)
            {
                MaterialRequest req = default(MaterialRequest);
                req.shader = ShaderDatabase.Transparent;
                req.color = RingColor;
                req.BaseTexPath = "UI/Overlays/TargetHighlight_Side";
                ringMaterial = MaterialPool.MatFrom(req);
                ringMaterial.GetTexture("_MainTex").wrapMode = TextureWrapMode.Clamp;
                ringY = Rand.ValueSeeded(RingColor.ToOpaque().GetHashCode()) * (1f / 26f) / 10f;
            }

            // Candidates: every cell within range of any selected holder. Each is flooded once,
            // against the set of selected-holder cells. assumeNonNoRoofCellsAreRoofed = true is
            // vanilla's roof-planning rule: unroofed ground not in the No Roof area counts.
            var holderCells = new HashSet<IntVec3>();
            foreach (Building s in holders) holderCells.Add(s.Position);
            candidates.Clear();
            var seen = new HashSet<IntVec3>();
            foreach (IntVec3 sp in holderCells)
            {
                foreach (IntVec3 c in GenRadial.RadialCellsAround(sp, Range, true))
                {
                    if (c.InBounds(m) && seen.Add(c)) candidates.Add(c);
                }
            }
            var field = new HashSet<IntVec3>();
            var visited = new HashSet<IntVec3>();
            var queue = new Queue<IntVec3>();
            foreach (IntVec3 c in candidates)
            {
                visited.Clear();
                queue.Clear();
                visited.Add(c);
                queue.Enqueue(c);
                bool found = false;
                while (queue.Count > 0 && !found)
                {
                    IntVec3 x = queue.Dequeue();
                    for (int i = 0; i < 5; i++)
                    {
                        IntVec3 c2 = x + GenAdj.CardinalDirectionsAndInside[i];
                        if (holderCells.Contains(c2) && c2.InHorDistOf(c, Range)) { found = true; break; }
                    }
                    if (found) break;
                    for (int i = 0; i < 4; i++)
                    {
                        IntVec3 n = x + GenAdj.CardinalDirections[i];
                        if (!n.InBounds(m) || visited.Contains(n) || !n.InHorDistOf(c, Range)) continue;
                        if (!(n.Roofed(m) || !m.areaManager.NoRoof[n])) continue;
                        visited.Add(n);
                        queue.Enqueue(n);
                    }
                }
                if (found) field.Add(c);
            }
            gridHash = GridHash(m);

            Mesh quad = MeshPool.plane10;
            Vector3[] qv = quad.vertices;
            Vector2[] qu = quad.uv;
            int[] qt = quad.triangles;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            bool[] need = new bool[4];
            foreach (IntVec3 c in field)
            {
                need[0] = c.z < m.Size.z - 1 && !field.Contains(new IntVec3(c.x, 0, c.z + 1));
                need[1] = c.x < m.Size.x - 1 && !field.Contains(new IntVec3(c.x + 1, 0, c.z));
                need[2] = c.z > 0 && !field.Contains(new IntVec3(c.x, 0, c.z - 1));
                need[3] = c.x > 0 && !field.Contains(new IntVec3(c.x - 1, 0, c.z));
                for (int k = 0; k < 4; k++)
                {
                    if (!need[k]) continue;
                    Vector3 pos = c.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays) + new Vector3(0f, ringY, 0f);
                    Quaternion rot = new Rot4(k).AsQuat;
                    int baseIndex = verts.Count;
                    for (int v = 0; v < qv.Length; v++)
                    {
                        verts.Add(pos + rot * qv[v]);
                        uvs.Add(qu[v]);
                    }
                    for (int t = 0; t < qt.Length; t++) tris.Add(baseIndex + qt[t]);
                }
            }
            if (verts.Count == 0) return;
            combined = new Mesh();
            if (verts.Count > 65000) combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.SetVertices(verts);
            combined.SetUVs(0, uvs);
            combined.SetTriangles(tris, 0);
        }

        public override void GameComponentUpdate()
        {
            if (staleMesh != null) { Object.Destroy(staleMesh); staleMesh = null; }
            if (Current.ProgramState != ProgramState.Playing || WorldRendererUtility.WorldRenderedNow) return;
            Map m = Find.CurrentMap;
            if (m == null) return;
            if (!InterfaceChangesMod.Settings.showRoofSupport)
            {
                if (combined != null) { Object.Destroy(combined); combined = null; }
                signature = 0;
                return;
            }
            try
            {
                int sig = Collect(m);
                bool gridChanged = false;
                if (sig != 0 && Time.frameCount - lastGridCheckFrame >= 60)
                {
                    lastGridCheckFrame = Time.frameCount;
                    gridChanged = GridHash(m) != gridHash;
                }
                if (sig != signature || m != cachedMap || gridChanged)
                {
                    signature = sig;
                    cachedMap = m;
                    Rebuild(m);
                }
                if (combined != null && sig != 0) Graphics.DrawMesh(combined, Matrix4x4.identity, ringMaterial, 0);
            }
            catch (System.Exception e)
            {
                if (combined != null) Object.Destroy(combined);
                combined = null;
                Logger.Warning("Supported roof overlay failed: " + e);
            }
        }
    }
}
