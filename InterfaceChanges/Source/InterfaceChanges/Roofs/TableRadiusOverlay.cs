using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using InterfaceChanges.Core;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Roofs
{
    // ii-architecture.md section 26. A toggle button (bottom left, directly above the
    // "Unsupported roofs" button) that outlines the radius within which pawns will look
    // for work at a table, centred on each player-owned bill giver on the current map.
    // No Harmony patch: GameComponentUpdate draws. The edge quads of every ring are baked
    // into ONE Mesh, rebuilt only when the set of tables or the radius changes (checked
    // every RefreshSeconds by a cheap signature); each frame is a single DrawMesh.
    public class TableRadiusOverlay : GameComponent
    {
        // Do Not Be Lazy section 47 (52.4): "A setting default is 24". DNBL has no table
        // radius setting yet (its only radius is sweepRadius, a different thing), so this
        // is the default to wire up once DNBL builds one.
        private const int DefaultRadius = 24;
        private const float RefreshSeconds = 2f;

        private static readonly Color RingColor = new Color(1f, 0.85f, 0.2f);

        private static Map cachedMap;
        private static float lastRefresh = -100f;
        private static int signature;
        private static Mesh combined;
        private static Mesh staleMesh;
        private static Material ringMaterial;
        private static float ringY;
        private static bool fieldLookedUp;
        private static FieldInfo radiusField;
        private static object dnblSettings;

        public TableRadiusOverlay(Game game)
        {
            cachedMap = null;
            // Constructed off the main thread during load: no Unity calls here. The old mesh is destroyed in GameComponentUpdate.
            if (!ReferenceEquals(combined, null)) staleMesh = combined;
            combined = null;
            signature = 0;
        }

        private static void ClearMesh()
        {
            if (combined != null) Object.Destroy(combined);
            combined = null;
            signature = 0;
        }

        // Radius source: DNBL's table-radius setting by reflection if it exists, else 24.
        // The FieldInfo is looked up once; the value is read on each refresh.
        private static int ReadRadius()
        {
            try
            {
                if (!fieldLookedUp)
                {
                    fieldLookedUp = true;
                    foreach (var mod in LoadedModManager.ModHandles)
                    {
                        if (mod.GetType().FullName != "DoNotBeLazy.Core.DoNotBeLazyMod") continue;
                        dnblSettings = mod.GetType().GetField("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                        FieldInfo f = dnblSettings?.GetType().GetField("tableRadius", BindingFlags.Public | BindingFlags.Instance);
                        if (f != null && f.FieldType == typeof(int)) radiusField = f;
                    }
                }
                if (radiusField != null) return (int)radiusField.GetValue(dnblSettings);
            }
            catch { }
            return DefaultRadius;
        }

        // Rebuilds the one combined mesh only when the set of tables or the radius changed.
        // The edge rule and look copy GenDraw.DrawFieldEdges (verified by decompile): a
        // plane10 quad on every cell side whose neighbour is outside the field, at
        // MetaOverlays altitude, TargetHighlight_Side texture, Transparent shader.
        private static void Refresh(Map m)
        {
            lastRefresh = Time.realtimeSinceStartup;
            int radius = Mathf.Min(ReadRadius(), Mathf.FloorToInt(GenRadial.MaxRadialPatternRadius));
            var tables = new List<IntVec3>();
            int sig = radius * 31 + m.uniqueID;
            foreach (Building b in m.listerBuildings.allBuildingsColonist)
            {
                if (!(b is IBillGiver) || !b.Spawned) continue;
                tables.Add(b.Position);
                sig = sig * 31 + b.Position.GetHashCode();
            }
            if (m == cachedMap && sig == signature) return;
            cachedMap = m;
            signature = sig;
            if (combined != null) Object.Destroy(combined);
            combined = null;
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
            Mesh quad = MeshPool.plane10;
            Vector3[] qv = quad.vertices;
            Vector2[] qu = quad.uv;
            int[] qt = quad.triangles;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var field = new HashSet<IntVec3>();
            bool[] need = new bool[4];
            foreach (IntVec3 center in tables)
            {
                field.Clear();
                foreach (IntVec3 c in GenRadial.RadialCellsAround(center, radius, true))
                {
                    if (c.InBounds(m)) field.Add(c);
                }
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
            }
            Logger.Info("Show table radius: rebuilt, map " + m.uniqueID + ", " + tables.Count + " table(s), radius " + radius + ", " + verts.Count / 4 + " edge segments.");
            if (verts.Count == 0) return;
            combined = new Mesh();
            if (verts.Count > 65000) combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.SetVertices(verts);
            combined.SetUVs(0, uvs);
            combined.SetTriangles(tris, 0);
        }

        public override void GameComponentOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || WorldRendererUtility.WorldRenderedNow) return;
            var settings = InterfaceChangesMod.Settings;
            bool on = settings.showTableRadius;
            var rect = new Rect(10f, UI.screenHeight - 35f - 8f - 30f - 34f, 190f, 30f);
            if (Widgets.ButtonText(rect, on ? "Show table radius: on" : "Show table radius: off"))
            {
                settings.showTableRadius = !on;
                InterfaceChangesMod.SaveSettings();
                if (!on)
                {
                    Logger.Info("Show table radius switched on.");
                    Refresh(Find.CurrentMap);
                }
                else
                {
                    ClearMesh();
                    cachedMap = null;
                    Logger.Info("Show table radius switched off.");
                }
            }
        }

        public override void GameComponentUpdate()
        {
            if (staleMesh != null) { Object.Destroy(staleMesh); staleMesh = null; }
            if (!InterfaceChangesMod.Settings.showTableRadius || Current.ProgramState != ProgramState.Playing || WorldRendererUtility.WorldRenderedNow) return;
            Map m = Find.CurrentMap;
            if (m == null) return;
            if (m != cachedMap || Time.realtimeSinceStartup - lastRefresh >= RefreshSeconds) Refresh(m);
            if (combined != null) Graphics.DrawMesh(combined, Matrix4x4.identity, ringMaterial, 0);
        }
    }
}
