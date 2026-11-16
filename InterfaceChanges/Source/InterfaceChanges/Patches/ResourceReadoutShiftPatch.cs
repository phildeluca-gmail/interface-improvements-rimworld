using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Patches
{
    // Prefix and finalizer on RimWorld.ResourceReadout.ResourceReadoutOnGUI(),
    // the one method that draws the top-left resource list (it calls
    // DoReadoutSimple / DoReadoutCategorized; verified by reflection on
    // lib\Assembly-CSharp.dll). The prefix puts a translate-right matrix on
    // GUI.matrix, so everything the readout draws moves 10 px right. The
    // matrix also transforms Event.current.mousePosition inside the call, so
    // hover, click and scroll hit-tests move with the drawing. The finalizer
    // (runs even if the original throws) restores the old matrix. No
    // transpiler. See ii-architecture.md section 23.
    public static class ResourceReadoutShiftPatch
    {
        private const float ShiftX = 10f;

        public static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.DeclaredMethod(typeof(ResourceReadout), "ResourceReadoutOnGUI");
            if (target == null)
            {
                Logger.Warning("ResourceReadoutShiftPatch: ResourceReadout.ResourceReadoutOnGUI not found, resource list will not be moved.");
                return;
            }

            harmony.Patch(target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ResourceReadoutShiftPatch), nameof(Prefix))),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(ResourceReadoutShiftPatch), nameof(Finalizer))));
            Logger.Info("ResourceReadoutShiftPatch applied to ResourceReadout.ResourceReadoutOnGUI (" + ShiftX + " px right).");
        }

        // __state carries the matrix to restore
        public static void Prefix(out Matrix4x4 __state)
        {
            __state = GUI.matrix;
            GUI.matrix = __state * Matrix4x4.Translate(new Vector3(ShiftX, 0f, 0f));
        }

        public static void Finalizer(Matrix4x4 __state)
        {
            GUI.matrix = __state;
        }
    }
}
