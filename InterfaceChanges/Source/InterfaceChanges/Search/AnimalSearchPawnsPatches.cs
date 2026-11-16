using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace InterfaceChanges.Search
{
    // Patches the Pawns getter on MainTabWindow_Animals so the Animals tab shows
    // only the rows the typed text matches.
    //
    // WHY THIS TYPE AND NOT ITS BASE. MainTabWindow_Animals DECLARES its own
    // override of Pawns - verified by reflection on lib\Assembly-CSharp.dll on
    // 2026-09-13, DeclaredOnly. A patch on MainTabWindow_PawnTable.Pawns would
    // be applied to a method this tab never calls, and the symptom would be
    // SILENCE: the mod would log "Loaded", the box would draw, typing would do
    // nothing, and no error would appear anywhere. That is exactly how Standard
    // Cargo shipped with no user interface. CLAUDE.md section 4, architecture
    // 6.3.
    //
    // TargetMethod rather than [HarmonyPatch(typeof(...), "Pawns",
    // MethodType.Getter)] so the resolve is written out and fails loudly at load
    // if the property ever moves, instead of quietly finding the base one.
    [HarmonyPatch]
    public static class AnimalsPawnsPatch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.DeclaredProperty(typeof(MainTabWindow_Animals), "Pawns").GetGetMethod(true);
        }

        public static void Postfix(ref IEnumerable<Pawn> __result)
        {
            __result = AnimalSearch.Apply(__result, AnimalSearch.Animals);
        }
    }

    // Same thing for the Wildlife tab, and it has to be a second patch class for
    // the same reason: MainTabWindow_Wildlife declares its OWN override of Pawns
    // too. Two declared overrides, two patches.
    //
    // The two tabs share the matching rule but not the widget, so this one reads
    // AnimalSearch.Wildlife and the one above reads AnimalSearch.Animals. That
    // is what "the animals and wildlife should be on different tabs" means in
    // code - a filter typed here cannot reach the other list.
    [HarmonyPatch]
    public static class WildlifePawnsPatch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.DeclaredProperty(typeof(MainTabWindow_Wildlife), "Pawns").GetGetMethod(true);
        }

        public static void Postfix(ref IEnumerable<Pawn> __result)
        {
            __result = AnimalSearch.Apply(__result, AnimalSearch.Wildlife);
        }
    }
}
