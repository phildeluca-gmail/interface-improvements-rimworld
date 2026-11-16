using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using RimWorld;
using Verse;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Patches
{
    // Assign main tab: on opening, scrolls the pawn table so the first selected
    // colonist's row is centred. See ii-architecture.md section 20.
    // Postfix on MainTabWindow_PawnTable.PostOpen (public override, declared on that
    // base type; MainTabWindow_Assign does not override it), filtered to Assign
    // instances. One log line per opening, nothing per frame.
    public static class AssignScrollPatch
    {
        private static FieldInfo tableField;
        private static FieldInfo scrollField;
        private static FieldInfo rowHeightsField;

        public static void Apply(Harmony harmony)
        {
            MethodInfo postOpen = AccessTools.DeclaredMethod(typeof(MainTabWindow_PawnTable), "PostOpen");
            tableField = AccessTools.Field(typeof(MainTabWindow_PawnTable), "table");
            scrollField = AccessTools.Field(typeof(PawnTable), "scrollPosition");
            rowHeightsField = AccessTools.Field(typeof(PawnTable), "cachedRowHeights");
            if (postOpen == null || tableField == null || scrollField == null || rowHeightsField == null)
            {
                Logger.Warning("AssignScrollPatch: PostOpen, table, scrollPosition or cachedRowHeights not found, Assign tab will not centre the selected pawn.");
                return;
            }
            harmony.Patch(postOpen, postfix: new HarmonyMethod(AccessTools.Method(typeof(AssignScrollPatch), nameof(PostOpenPostfix))));
            Logger.Info("AssignScrollPatch applied to MainTabWindow_PawnTable.PostOpen (Assign tab only).");
        }

        public static void PostOpenPostfix(MainTabWindow_PawnTable __instance)
        {
            if (!(__instance is MainTabWindow_Assign))
            {
                return;
            }
            try
            {
                PawnTable table = tableField.GetValue(__instance) as PawnTable;
                if (table == null)
                {
                    Logger.Info("Assign tab scroll skipped: no table.");
                    return;
                }
                // PawnsListForReading recaches the dirty table (PostOpen just set it dirty).
                List<Pawn> pawns = table.PawnsListForReading;
                Pawn target = null;
                int index = -1;
                List<Pawn> selected = Find.Selector.SelectedPawns;
                for (int i = 0; i < selected.Count && target == null; i++)
                {
                    int found = pawns.IndexOf(selected[i]);
                    if (found >= 0)
                    {
                        target = selected[i];
                        index = found;
                    }
                }
                if (target == null)
                {
                    Logger.Info("Assign tab scroll skipped: " + (selected.Count == 0 ? "no pawn selected." : "no selected pawn is listed."));
                    return;
                }
                List<float> heights = rowHeightsField.GetValue(table) as List<float>;
                if (heights == null || heights.Count != pawns.Count)
                {
                    Logger.Info("Assign tab scroll skipped: row heights not available.");
                    return;
                }
                // The game draws rows at integer heights.
                float top = 0f;
                for (int i = 0; i < index; i++)
                {
                    top += (int)heights[i];
                }
                float rowHeight = (int)heights[index];
                float viewHeight = (int)table.Size.y - (int)table.HeaderHeight;
                float contentHeight = (int)table.HeightNoScrollbar - (int)table.HeaderHeight;
                float y = Mathf.Clamp(top + rowHeight / 2f - viewHeight / 2f, 0f, Mathf.Max(0f, contentHeight - viewHeight));
                Vector2 scroll = (Vector2)scrollField.GetValue(table);
                scrollField.SetValue(table, new Vector2(scroll.x, y));
                Logger.Info("Assign tab scrolled to " + target.LabelShort + " (row " + index + " of " + pawns.Count + ", scroll y " + y.ToString("0") + ").");
            }
            catch (Exception ex)
            {
                Logger.Warning("AssignScrollPatch: failed: " + ex);
            }
        }
    }
}
