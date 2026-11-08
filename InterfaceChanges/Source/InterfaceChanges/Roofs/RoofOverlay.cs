using System.Diagnostics;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Roofs
{
    // ii-architecture.md section 18. A toggle button (bottom left, above the main
    // button bar) that paints every roofed cell: red where the game would collapse
    // the roof (RoofCollapseUtility.WithinRangeOfRoofHolder is false, i.e. no
    // roof-holding edifice within 6.9 cells through connected roof), green where
    // it stands. Roofs whose RoofDef cannot collapse (overhead mountain) are green.
    //
    // Computed when switched on, then only when marked dirty AND at most once a
    // second. Dirty is set by three postfixes, all doing nothing while the overlay
    // is off: Building.SpawnSetup and Building.DeSpawn (only for buildings whose
    // def.holdsRoof) and RoofGrid.SetRoof (a roof built, removed or collapsed).
    // Drawing is a CellBoolDrawer: MarkForDraw each frame, mesh rebuilt only on
    // SetDirty. No per-frame scan.
    public class RoofOverlay : GameComponent
    {
        private const byte Supported = 1;
        private const byte Collapsing = 2;
        private const float MinSecondsBetweenRecomputes = 1f;

        private static bool on;
        private static bool dirty;
        private static Map map;
        private static byte[] state;
        private static CellBoolDrawer drawer;
        private static float lastComputeTime;
        private static float lastLogTime = -100f;
        private static int skippedLogs;

        public RoofOverlay(Game game)
        {
            on = false;
            dirty = false;
            map = null;
            state = null;
            drawer = null;
        }

        public static void Apply(Harmony harmony)
        {
            var spawn = AccessTools.DeclaredMethod(typeof(Building), "SpawnSetup");
            var despawn = AccessTools.DeclaredMethod(typeof(Building), "DeSpawn");
            var setRoof = AccessTools.DeclaredMethod(typeof(RoofGrid), "SetRoof");
            if (spawn == null || despawn == null || setRoof == null)
            {
                Logger.Warning("RoofOverlay: Building.SpawnSetup, Building.DeSpawn or RoofGrid.SetRoof not found, the overlay will not refresh by itself.");
            }
            if (spawn != null) harmony.Patch(spawn, postfix: new HarmonyMethod(AccessTools.Method(typeof(RoofOverlay), nameof(BuildingPostfix))));
            if (despawn != null) harmony.Patch(despawn, postfix: new HarmonyMethod(AccessTools.Method(typeof(RoofOverlay), nameof(BuildingPostfix))));
            if (setRoof != null) harmony.Patch(setRoof, postfix: new HarmonyMethod(AccessTools.Method(typeof(RoofOverlay), nameof(RoofPostfix))));
            Logger.Info("RoofOverlay patches applied.");
        }

        // Postfix on Building.SpawnSetup and Building.DeSpawn.
        public static void BuildingPostfix(Building __instance)
        {
            if (on && __instance.def.holdsRoof) dirty = true;
        }

        // Postfix on RoofGrid.SetRoof.
        public static void RoofPostfix()
        {
            if (on) dirty = true;
        }

        public override void GameComponentOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || WorldRendererUtility.WorldRenderedNow) return;
            var rect = new Rect(10f, UI.screenHeight - 35f - 8f - 30f, 190f, 30f);
            if (Widgets.ButtonText(rect, on ? "Unsupported roofs: on" : "Unsupported roofs: off"))
            {
                on = !on;
                if (on)
                {
                    Recompute(Find.CurrentMap, "button switched on");
                }
                else
                {
                    state = null;
                    drawer = null;
                    drawerMap = null;
                    map = null;
                    Logger.Info("Unsupported roofs overlay switched off.");
                }
            }
        }

        public override void GameComponentUpdate()
        {
            if (!on || Current.ProgramState != ProgramState.Playing || WorldRendererUtility.WorldRenderedNow) return;
            Map current = Find.CurrentMap;
            if (current == null) return;
            if (current != map)
            {
                Recompute(current, "map changed");
            }
            else if (dirty && Time.realtimeSinceStartup - lastComputeTime >= MinSecondsBetweenRecomputes)
            {
                Recompute(current, "building or roof changed");
            }
            if (drawer == null) return;
            drawer.MarkForDraw();
            drawer.CellBoolDrawerUpdate();
        }

        private static void Recompute(Map m, string reason)
        {
            var watch = Stopwatch.StartNew();
            int cells = m.cellIndices.NumGridCells;
            byte[] result = new byte[cells];
            int roofed = 0, red = 0;
            RoofGrid grid = m.roofGrid;
            for (int i = 0; i < cells; i++)
            {
                RoofDef roof = grid.RoofAt(i);
                if (roof == null || m.fogGrid.IsFogged(i)) continue;
                roofed++;
                IntVec3 c = m.cellIndices.IndexToCell(i);
                if (roof.canCollapse && !RoofCollapseUtility.WithinRangeOfRoofHolder(c, m))
                {
                    result[i] = Collapsing;
                    red++;
                }
                else
                {
                    result[i] = Supported;
                }
            }
            map = m;
            state = result;
            dirty = false;
            lastComputeTime = Time.realtimeSinceStartup;
            if (drawer == null || drawerMap != m)
            {
                drawerMap = m;
                drawer = new CellBoolDrawer(i => state[i] != 0, () => Color.white,
                    i => state[i] == Collapsing ? new Color(1f, 0.2f, 0.2f) : new Color(0.3f, 1f, 0.4f),
                    m.Size.x, m.Size.z, 0.4f);
            }
            drawer.SetDirty();
            watch.Stop();

            // Rebuilds from building changes can repeat while a colony is under construction: one line per 5 s.
            bool always = reason != "building or roof changed";
            if (always || Time.realtimeSinceStartup - lastLogTime >= 5f)
            {
                Logger.Info("Unsupported roofs (" + reason + "): map " + m.uniqueID + ", " + cells + " cells scanned, " + roofed
                    + " roofed, " + red + " would collapse (red), " + (roofed - red) + " supported (green), " + watch.ElapsedMilliseconds
                    + " ms" + (skippedLogs > 0 ? ", " + skippedLogs + " earlier rebuild(s) not logged" : "") + ".");
                lastLogTime = Time.realtimeSinceStartup;
                skippedLogs = 0;
            }
            else
            {
                skippedLogs++;
            }
        }

        private static Map drawerMap;
    }
}
