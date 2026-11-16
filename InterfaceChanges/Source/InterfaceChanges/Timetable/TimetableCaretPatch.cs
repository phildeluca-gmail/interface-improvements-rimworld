using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Timetable
{
    // Patches RimWorld.PawnColumnWorker_Timetable.DoHeader - the method that
    // draws the row of numbers 0..23 above the schedule grid in the Schedule
    // tab - so a small upward triangle marks the hour happening now.
    //
    // Ordered 2026-09-12: "I want to add a carat to the scheduler that shows
    // what hour it currently is", and when asked what it should look like,
    // "just a caret above the numbers". Architecture sections 4 and 4.3.
    //
    // WHY A POSTFIX AND WHY THIS METHOD. Vanilla's DoHeader is handed the
    // header Rect, so there is no screen position to find or guess - column i
    // is at rect.x + i * rect.width / 24f. Read off lib\Assembly-CSharp.dll:
    //
    //   public override void DoHeader(Rect rect, PawnTable table)
    //   {
    //       float num = rect.x;
    //       Text.Font = GameFont.Tiny;
    //       Text.Anchor = TextAnchor.LowerCenter;
    //       float num2 = rect.width / 24f;
    //       for (int i = 0; i < 24; i++)
    //       {
    //           Widgets.Label(new Rect(num, rect.y, num2, rect.height + 3f), i.ToString());
    //           num += num2;
    //       }
    //       Text.Font = GameFont.Small;
    //       Text.Anchor = TextAnchor.UpperLeft;
    //   }
    //
    // WHY THE MARKER IS MEASURED UP FROM THE DIGITS, NOT DOWN FROM THE TOP.
    // The first build drew "^" anchored UpperCenter at rect.y and nothing
    // showed. Read off the same DLL, 2026-09-13:
    //   - PawnTable.PawnTableOnGUI hands every header the same height, the
    //     largest GetMinHeaderHeight of any column. The Restrict table has an
    //     AllowedArea column, whose minimum is 65, so this rect is 65 tall and
    //     the digits sit at its bottom.
    //   - MainTabWindow_Schedule.DoWindowContents calls the base (which draws
    //     the table at the top of the window, ExtraTopSpace 0) and THEN
    //     TimeAssignmentSelector.DrawTimeAssignmentSelectorGrid(0, 0, 191, 65),
    //     which lays the Anything/Work/Recreation/Sleep(/Meditate) buttons in
    //     one row of 95.5-wide boxes across the top ~30 pixels - 382 or 477
    //     pixels wide, over the left of the hour columns.
    // So the top of this rect is exactly where those opaque buttons are drawn
    // a moment later, and the caret was painted over. The strip that really is
    // free lies between the bottom of the buttons and the top of the digits.
    [HarmonyPatch(typeof(PawnColumnWorker_Timetable), nameof(PawnColumnWorker_Timetable.DoHeader))]
    public static class TimetableCaretPatch
    {
        // Rows of the triangle, widths 1, 3, 5, 7, 9. Sized to stay inside
        // one hour column, which is 15 to 25 pixels wide (GetMinWidth 360,
        // GetMaxWidth 600, over 24).
        private const int MarkerRows = 5;

        // Bright amber: none of the four schedule colours, so it cannot be
        // mistaken for a block of the grid. A struct, so no allocation.
        private static readonly Color MarkerColor = new Color(1f, 0.8f, 0.1f);

        public static void Postfix(Rect rect, PawnTable table)
        {
            // GenLocalDate.HourOfDay(Map) forwards to HourOfDay(map.Tile) - the
            // hour belongs to the map's TILE, not to the world, so a colony at
            // another longitude reads a different hour. Find.CurrentMap is the
            // map the Schedule tab is listing.
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }

            int hour = GenLocalDate.HourOfDay(map);
            float colWidth = rect.width / 24f;
            float centreX = rect.x + (hour + 0.5f) * colWidth;

            // Vanilla labels the digits LowerCenter in a box ending at
            // rect.yMax + 3, so one Tiny line up from there is the top of the
            // digits. The triangle's bottom row sits just above that. Clamped
            // to the rect's top so a shorter header can never push it upward
            // out of the column.
            float digitsTop = rect.yMax + 3f - Text.LineHeightOf(GameFont.Tiny);
            float top = Mathf.Max(rect.y, digitsTop - MarkerRows);

            // Widgets.DrawBoxSolid sets GUI.color, draws BaseContent.WhiteTex
            // and restores GUI.color itself - no font glyph, so nothing to clip.
            for (int row = 0; row < MarkerRows; row++)
            {
                // widest row on top, one pixel at the bottom: a V pointing down at the hour
                float width = 1 + 2 * (MarkerRows - 1 - row);
                Widgets.DrawBoxSolid(new Rect(Mathf.Round(centreX - width / 2f), top + row, width, 1f), MarkerColor);
            }
        }
    }
}
