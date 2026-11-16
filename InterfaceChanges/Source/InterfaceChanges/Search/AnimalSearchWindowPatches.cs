using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace InterfaceChanges.Search
{
    // Patches MainTabWindow_PawnTable.ExtraTopSpace to open a strip above the
    // table for the search box.
    //
    // WHY THE BASE TYPE HERE, WHEN THE Pawns PATCHES ARE ON THE DERIVED ONES.
    // Opposite situation, same rule: patch whatever type actually declares the
    // method that runs. NEITHER MainTabWindow_Animals NOR MainTabWindow_Wildlife
    // declares an override of ExtraTopSpace - verified DeclaredOnly on
    // lib\Assembly-CSharp.dll, 2026-09-13 - so the base body is the one both
    // tabs run, and there is no derived method to patch.
    //
    // The price is that this body also runs for Work, Assign, Schedule and
    // Mechs, which share the base class and are not in scope. WidgetFor returns
    // null for those and nothing changes. That check is the first line for a
    // reason.
    //
    // ExtraTopSpace is read in two places, which is why patching it is enough on
    // its own: MainTabWindow_PawnTable.DoWindowContents draws the table at
    // rect.y + ExtraTopSpace, and CreateTable subtracts it when it sizes the
    // table. So the same postfix moves the table down AND shrinks it to fit,
    // and it cannot push the bottom of the table off the window.
    [HarmonyPatch(typeof(MainTabWindow_PawnTable), "ExtraTopSpace", MethodType.Getter)]
    public static class ExtraTopSpacePatch
    {
        public static void Postfix(MainTabWindow_PawnTable __instance, ref float __result)
        {
            if (AnimalSearch.WidgetFor(__instance) == null)
            {
                return;
            }

            __result += AnimalSearch.RowHeight;
        }
    }

    // Patches MainTabWindow_PawnTable.DoWindowContents to draw the box in the
    // strip the patch above opened.
    //
    // WHY THE BASE TYPE. MainTabWindow_Wildlife declares no DoWindowContents at
    // all, so the base body is what runs for it and there is nothing else to
    // patch. MainTabWindow_Animals DOES declare an override - but its first
    // instruction is a non-virtual call straight to this base method, read out
    // of its IL on 2026-09-13, so the patched body runs for that tab too.
    //
    // That IL check is the whole reason this is allowed to sit on the base. If
    // the Animals override had NOT called base, this patch would have drawn
    // nothing in the Animals tab and said nothing about it. Architecture 6.2,
    // fact 4.
    //
    // WHY A PREFIX, WHEN CLAUDE.md PREFERS POSTFIXES. It was a postfix, and in
    // the Animals tab the box lost keyboard focus after every letter typed.
    // Unity keeps keyboard focus as a control id number, and hands ids out in
    // the order controls are drawn. As a postfix the text field was drawn AFTER
    // the pawn table, so its id depended on how many checkboxes and buttons the
    // table drew first - and every keystroke changes that, because it filters
    // rows out. Animals rows carry many such controls, Wildlife rows few.
    // Drawn first, nothing variable comes before the text field and its id
    // holds. Vanilla does the same: Window.InnerWindowOnGUI draws a window's
    // own search box before DoWindowContents. Architecture 6.9.
    //
    // It is still one patch on the same method, it returns void so the
    // original always runs, and it draws in the same place as before.
    //
    // Ordering note, and it decides where the box goes: this prefix runs at
    // the start of the BASE method, so in the Animals tab it draws BEFORE that
    // tab's own "Manage auto-slaughter" button. The button is at the top left
    // and up to 260 wide, so the box is right-aligned to stay out from under
    // it. AnimalSearch.BoxRect.
    [HarmonyPatch(typeof(MainTabWindow_PawnTable), nameof(MainTabWindow_PawnTable.DoWindowContents))]
    public static class DoWindowContentsPatch
    {
        public static void Prefix(MainTabWindow_PawnTable __instance, Rect rect)
        {
            QuickSearchWidget widget = AnimalSearch.WidgetFor(__instance);
            if (widget == null)
            {
                return;
            }

            // The two Actions are how the vanilla widget reports a change. Both
            // do the same thing here: tell the window its rows are stale so the
            // table recaches and reads the filtered Pawns getter again.
            //
            // Notify_PawnsChanged is public and its entire body is SetDirty(),
            // which calls table.SetDirty() and then SetInitialSizeAndPosition() -
            // so the window resizes to the shorter list at the same time. Read
            // out of its IL; no reflection needed to reach it.
            widget.OnGUI(
                AnimalSearch.BoxRect(rect),
                __instance.Notify_PawnsChanged,
                __instance.Notify_PawnsChanged);
        }
    }
}
