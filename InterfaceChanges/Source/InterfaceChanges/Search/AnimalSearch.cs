using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Search
{
    // The two search boxes and the matching rule behind them. No patches here -
    // those are in AnimalSearchPawnsPatches and AnimalSearchWindowPatches.
    // Architecture section 6.
    public static class AnimalSearch
    {
        // One widget per tab, each holding its own text. Ordered 2026-09-13:
        // "The animals and wildlife should be on different tabs." Typing in one
        // leaves the other exactly as it was.
        //
        // Static, so the text survives the tab being closed and reopened.
        // Architecture 6.6 - the alternative needed a fifth Harmony patch just
        // to throw a string away, and the box is drawn with the text still in
        // it so nothing is hidden from the player.
        public static readonly QuickSearchWidget Animals = new QuickSearchWidget();
        public static readonly QuickSearchWidget Wildlife = new QuickSearchWidget();

        // How much vertical room the box needs, and how far the table moves down
        // to give it. Read by BOTH window patches - the one that makes the room
        // and the one that draws into it - so they cannot drift apart.
        //
        // QuickSearchWidget.WidgetHeight is a public static float on the vanilla
        // widget, 24 in this build. The extra 10 is padding, and it also happens
        // to clear the Animals tab's 32-tall "Manage auto-slaughter" button,
        // which vanilla draws straight on top of the table. Architecture 6.4.
        public static float RowHeight
        {
            get { return QuickSearchWidget.WidgetHeight + 10f; }
        }

        // Only these two tabs get a box. Everything else deriving from
        // MainTabWindow_PawnTable - Work, Assign, Schedule, Mechs - is out of
        // scope and must be left alone, and the two window patches sit on that
        // shared base class, so this check is the thing keeping them out.
        public static QuickSearchWidget WidgetFor(MainTabWindow_PawnTable window)
        {
            if (window is MainTabWindow_Animals)
            {
                return Animals;
            }

            if (window is MainTabWindow_Wildlife)
            {
                return Wildlife;
            }

            return null;
        }

        // Narrows a tab's rows to the ones the typed text matches, and tells the
        // widget whether anything survived so it can go red the way the trade
        // dialog's does.
        //
        // Materialises into a list on purpose. The caller is a Pawns getter,
        // which the table reads when it recaches and at no other time - so this
        // is one allocation per keystroke, not one per frame. Deferred LINQ
        // would have made noResultsMatched impossible to set here anyway.
        public static IEnumerable<Pawn> Apply(IEnumerable<Pawn> source, QuickSearchWidget widget)
        {
            if (source == null || widget == null || !widget.filter.Active)
            {
                return source;
            }

            List<Pawn> kept = new List<Pawn>();
            foreach (Pawn p in source)
            {
                if (Matches(p, widget.filter))
                {
                    kept.Add(p);
                }
            }

            widget.noResultsMatched = kept.Count == 0;
            return kept;
        }

        // A row survives on either its name or its kind.
        //
        // Kind is what makes the Wildlife tab work at all - a wild animal
        // usually has no Name, so a name-only filter would match nothing there,
        // and "animal type" is what the ask put first: "I want the ability to
        // type an animal type or name into a search field".
        private static bool Matches(Pawn p, QuickSearchFilter filter)
        {
            if (p == null)
            {
                return false;
            }

            // Verse.Name, null for most wild animals. ToStringShort is the given
            // name on its own - "Zac" - without the species in parentheses.
            if (p.Name != null && Hit(filter, p.Name.ToStringShort))
            {
                return true;
            }

            // "muffalo", "thrumbo", "yorkshire terrier"
            if (Hit(filter, p.KindLabel))
            {
                return true;
            }

            // what the row itself is labelled with; falls back to the kind when
            // the animal is unnamed
            if (Hit(filter, p.LabelShortCap))
            {
                return true;
            }

            // the species, for the case where the PawnKindDef and the ThingDef
            // disagree - happens with some modded animals
            if (p.def != null && Hit(filter, p.def.label))
            {
                return true;
            }

            return false;
        }

        // QuickSearchFilter.Matches was not read, only its signature, so what it
        // does with a null is unknown. Not finding out in someone's game.
        private static bool Hit(QuickSearchFilter filter, string s)
        {
            return !string.IsNullOrEmpty(s) && filter.Matches(s);
        }

        // Where the box goes inside the strip the ExtraTopSpace patch opened up.
        //
        // Right-aligned rather than left because the Animals tab draws its
        // "Manage auto-slaughter" button at the top LEFT, up to 260 wide, and it
        // draws it after our patch has drawn the box - so a left-aligned box would be
        // painted over. Same place on both tabs this way.
        public static Rect BoxRect(Rect inRect)
        {
            float width = Mathf.Min(240f, inRect.width * 0.4f);
            float y = inRect.y + (RowHeight - QuickSearchWidget.WidgetHeight) / 2f;
            return new Rect(inRect.xMax - width - 4f, y, width, QuickSearchWidget.WidgetHeight);
        }
    }
}
