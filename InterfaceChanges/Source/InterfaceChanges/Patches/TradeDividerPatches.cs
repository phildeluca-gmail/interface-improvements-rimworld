using System.Collections;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Patches
{
    // ii-architecture.md section 16. Draws a thin line across the top of a
    // trade, caravan or transport row whose category differs from the row
    // above it. Two postfixes, applied by hand so a missing target logs
    // instead of throwing:
    //   TransferableOneWayWidget.DoRow (private) - caravan, transport, etc.
    //   TradeUI.DrawTradeableRow (public static) - the trade dialog.
    // Neither row method is handed the previous row, so it is found in the
    // widget's private lists by reflection, using the row index. "Category"
    // is the vanilla ordering: TransferableComparer_Category.Compare.
    // Only drawn while the first sorter is Category. No setting (ordered
    // always on). No logging per row.
    public static class TradeDividerPatches
    {
        private static FieldInfo widgetSorter1;
        private static FieldInfo widgetSections;
        private static FieldInfo sectionList;
        private static FieldInfo tradeSorter1;
        private static FieldInfo tradeList;

        public static void Apply(Harmony harmony)
        {
            widgetSorter1 = AccessTools.Field(typeof(TransferableOneWayWidget), "sorter1");
            widgetSections = AccessTools.Field(typeof(TransferableOneWayWidget), "sections");
            System.Type section = AccessTools.Inner(typeof(TransferableOneWayWidget), "Section");
            sectionList = section == null ? null : AccessTools.Field(section, "cachedTransferables");
            tradeSorter1 = AccessTools.Field(typeof(Dialog_Trade), "sorter1");
            tradeList = AccessTools.Field(typeof(Dialog_Trade), "cachedTradeables");

            MethodInfo doRow = AccessTools.DeclaredMethod(typeof(TransferableOneWayWidget), "DoRow");
            if (doRow == null || widgetSorter1 == null || widgetSections == null || sectionList == null)
            {
                Logger.Warning("TradeDividerPatches: TransferableOneWayWidget.DoRow or its fields not found, no dividers in caravan lists.");
            }
            else
            {
                harmony.Patch(doRow, postfix: new HarmonyMethod(AccessTools.Method(typeof(TradeDividerPatches), nameof(WidgetRowPostfix))));
                Logger.Info("TradeDividerPatches applied to TransferableOneWayWidget.DoRow.");
            }

            MethodInfo tradeRow = AccessTools.DeclaredMethod(typeof(TradeUI), "DrawTradeableRow");
            if (tradeRow == null || tradeSorter1 == null || tradeList == null)
            {
                Logger.Warning("TradeDividerPatches: TradeUI.DrawTradeableRow or Dialog_Trade fields not found, no dividers in the trade list.");
            }
            else
            {
                harmony.Patch(tradeRow, postfix: new HarmonyMethod(AccessTools.Method(typeof(TradeDividerPatches), nameof(TradeRowPostfix))));
                Logger.Info("TradeDividerPatches applied to TradeUI.DrawTradeableRow.");
            }
        }

        public static void WidgetRowPostfix(TransferableOneWayWidget __instance, Rect rect, TransferableOneWay trad, int index)
        {
            if (index <= 0 || Event.current.type != EventType.Repaint) return;
            if (widgetSorter1.GetValue(__instance) != TransferableSorterDefOf.Category) return;
            var sections = widgetSections.GetValue(__instance) as IList;
            if (sections == null) return;
            for (int i = 0; i < sections.Count; i++)
            {
                var list = sectionList.GetValue(sections[i]) as IList;
                if (list != null && index < list.Count && list[index] == trad)
                {
                    MaybeDraw(rect, list[index - 1] as Transferable, trad);
                    return;
                }
            }
        }

        public static void TradeRowPostfix(Rect rect, Tradeable trad, int index)
        {
            if (index <= 0 || Event.current.type != EventType.Repaint) return;
            Dialog_Trade dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
            if (dialog == null) return;
            if (tradeSorter1.GetValue(dialog) != TransferableSorterDefOf.Category) return;
            var list = tradeList.GetValue(dialog) as IList;
            // Other callers (the silver row) pass their own index; only draw for a row of the main list.
            if (list == null || index >= list.Count || list[index] != trad) return;
            MaybeDraw(rect, list[index - 1] as Transferable, trad);
        }

        private static void MaybeDraw(Rect rect, Transferable previous, Transferable current)
        {
            if (previous == null || current == null || !previous.HasAnyThing || !current.HasAnyThing) return;
            ThingDef a = previous.ThingDef;
            ThingDef b = current.ThingDef;
            if (a == null || b == null) return;
            if (TransferableComparer_Category.Compare(a, b) == 0) return;
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            GUI.color = old;
        }
    }
}
