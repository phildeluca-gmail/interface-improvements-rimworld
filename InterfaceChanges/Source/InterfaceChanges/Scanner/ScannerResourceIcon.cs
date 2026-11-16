using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Scanner
{
    public class CompProperties_ScannerResourceIcon : CompProperties
    {
        public CompProperties_ScannerResourceIcon()
        {
            compClass = typeof(CompScannerResourceIcon);
        }
    }

    // ii-architecture.md section 19. Draws the icon of the resource a Long
    // Range Mineral Scanner is set to at the centre of the building.
    // Verified against lib\Assembly-CSharp.dll 2026-11-10:
    //  - CompLongRangeMineralScanner.targetMineable is a private ThingDef, a
    //    MINEABLE (e.g. MineableGold); the resource itself is
    //    targetMineable.building.mineableThing (the game's own gizmo uses its
    //    uiIcon the same way).
    //  - The scanner def inherits drawerType MapMeshOnly from BuildingBase, and
    //    Thing.DynamicDrawPhase skips MapMeshOnly things, so ThingComp.PostDraw
    //    would never run. Startup below sets that one def to MapMeshAndRealTime:
    //    the building stays in the map mesh and gains a per-frame PostDraw.
    public class CompScannerResourceIcon : ThingComp
    {
        private const float IconSize = 1.1f;

        private static readonly FieldInfo targetField =
            AccessTools.Field(typeof(CompLongRangeMineralScanner), "targetMineable");

        private static readonly Dictionary<ThingDef, Material> materials = new Dictionary<ThingDef, Material>();

        private ThingDef lastTarget;
        private CompLongRangeMineralScanner scanner;

        public override void PostDraw()
        {
            base.PostDraw();
            if (scanner == null)
            {
                scanner = parent.GetComp<CompLongRangeMineralScanner>();
                if (scanner == null || targetField == null) return;
            }
            var target = targetField.GetValue(scanner) as ThingDef;
            ThingDef resource = target?.building?.mineableThing;
            if (target != lastTarget)
            {
                lastTarget = target;
                // Once per change of target, never per frame.
                Utility.Logger.Info("Mineral scanner " + parent.ThingID + " icon now shows "
                    + (resource != null ? resource.defName : "nothing") + ".");
            }
            if (resource == null || resource.uiIcon == null) return;

            Material mat;
            if (!materials.TryGetValue(resource, out mat))
            {
                mat = MaterialPool.MatFrom(resource.uiIcon, ShaderDatabase.Transparent, Color.white);
                materials[resource] = mat;
            }
            Vector3 pos = parent.DrawPos;
            pos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(IconSize, 1f, IconSize)), mat, 0);
        }
    }

    // Runs once after all defs load: adds the comp to the scanner def and
    // switches its drawerType so PostDraw is called (see above).
    [StaticConstructorOnStartup]
    public static class ScannerResourceIconStartup
    {
        static ScannerResourceIconStartup()
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("LongRangeMineralScanner");
            if (def == null)
            {
                Utility.Logger.Info("LongRangeMineralScanner def not found; resource icon not added.");
                return;
            }
            if (def.comps == null) def.comps = new List<CompProperties>();
            bool has = false;
            foreach (CompProperties c in def.comps)
            {
                if (c.compClass == typeof(CompScannerResourceIcon)) has = true;
            }
            if (!has) def.comps.Add(new CompProperties_ScannerResourceIcon());
            DrawerType before = def.drawerType;
            if (def.drawerType == DrawerType.MapMeshOnly) def.drawerType = DrawerType.MapMeshAndRealTime;
            Utility.Logger.Info("Mineral scanner resource icon added; drawerType " + before + " -> " + def.drawerType + ".");
        }
    }
}
