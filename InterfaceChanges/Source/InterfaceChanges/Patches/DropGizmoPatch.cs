using System.Collections.Generic;
using HarmonyLib;
using InterfaceChanges.Drop;
using InterfaceChanges.Utility;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Patches
{
    // Postfix on Verse.Pawn.GetGizmos: adds "Drop everything" for colonists.
    // The groupKey differs from Do Not Be Lazy's copy of the button, so the
    // two never merge if both mods are loaded. GizmoGridDrawer merges
    // same-groupKey gizmos into one button but still runs every pawn's
    // action on click, which gives each selected pawn its own queue.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class DropGizmoPatch
    {
        private const int GroupKey = 0x49434450; // "ICDP"

        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
        {
            foreach (Gizmo gizmo in gizmos)
            {
                yield return gizmo;
            }

            if (!DropButtonCompat.SuppressDropButton && __instance != null && __instance.IsColonist && __instance.Spawned && !__instance.Dead)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Drop everything",
                    defaultDesc = "Drop every carried item and worn apparel piece. "
                        + "Locked apparel and warcaskets are left where they are. "
                        + "The equipped weapon is kept unless the setting in Mod options says otherwise.",
                    icon = TexButton.Drop,
                    groupKey = GroupKey,
                    action = () => DropEverything.Queue(__instance)
                };
            }

            // Find bed (ii-architecture.md section 22): only when exactly this one
            // pawn is selected, so the click has one pawn's bed to jump to.
            if (__instance != null && __instance.IsColonist && !__instance.Dead
                && Find.Selector.SingleSelectedThing == __instance)
            {
                yield return MakeFindBed(__instance);
            }
        }

        private static Gizmo MakeFindBed(Pawn pawn)
        {
            Building_Bed bed = pawn.ownership != null ? pawn.ownership.OwnedBed : null;
            Command_Action cmd = new Command_Action
            {
                defaultLabel = "Find bed",
                defaultDesc = "Jump to this pawn's own bed and select it.",
                icon = TexButton.Search,
                groupKey = 0x49434642, // "ICFB"
                action = () => FindBed(pawn)
            };
            if (bed == null)
            {
                cmd.Disable(pawn.LabelShort + " has no bed assigned.");
            }
            return cmd;
        }

        private static void FindBed(Pawn pawn)
        {
            Building_Bed bed = pawn.ownership != null ? pawn.ownership.OwnedBed : null;
            if (bed == null)
            {
                Logger.Info("Find bed: " + pawn.LabelShort + " has no owned bed, nothing selected.");
                return;
            }
            if (bed.Map == null || !bed.Spawned)
            {
                Logger.Info("Find bed: " + pawn.LabelShort + "'s bed " + bed.LabelCap + " is not spawned on a map, nothing selected.");
                return;
            }
            CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(bed));
            Logger.Info("Find bed: jumped to and selected " + pawn.LabelShort + "'s bed " + bed.LabelCap + " at " + bed.Position + " on " + bed.Map);
        }
    }
}
