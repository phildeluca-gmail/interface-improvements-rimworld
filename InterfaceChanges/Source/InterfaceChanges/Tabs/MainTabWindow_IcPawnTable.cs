using System.Collections.Generic;
using InterfaceChanges.Utility;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Tabs
{
    // Base for the Prisoners and Raiders tabs (ii-architecture.md section 21).
    // Same pieces as vanilla Animals/Wildlife: a MainTabWindow_PawnTable subclass
    // supplying the table def and the pawns. Adds a search box (name or kind)
    // drawn in a strip above the table - implemented here rather than by
    // patching, so Dialog Improvements is untouched. Zero Harmony patches.
    public abstract class MainTabWindow_IcPawnTable : MainTabWindow_PawnTable
    {
        private readonly QuickSearchWidget search = new QuickSearchWidget();

        protected abstract string TabName { get; }

        protected abstract IEnumerable<Pawn> SourcePawns();

        protected override float ExtraTopSpace
        {
            get { return QuickSearchWidget.WidgetHeight + 10f; }
        }

        // Read by the table only when it recaches, not per frame.
        protected override IEnumerable<Pawn> Pawns
        {
            get { return Filter(SourcePawns()); }
        }

        public override void PostOpen()
        {
            search.Reset();
            base.PostOpen();
            int n = 0;
            foreach (Pawn unused in Pawns)
            {
                n++;
            }
            InterfaceChanges.Utility.Logger.Info(TabName + " tab opened: " + n + " pawns listed.");
        }

        // The box is drawn BEFORE the table: Unity hands out control ids in draw
        // order, and the table's control count changes as the filter narrows
        // the rows (Dialog Improvements architecture section 6.9).
        public override void DoWindowContents(Rect rect)
        {
            float width = Mathf.Min(240f, rect.width * 0.4f);
            float y = rect.y + (ExtraTopSpace - QuickSearchWidget.WidgetHeight) / 2f;
            search.OnGUI(new Rect(rect.xMax - width - 4f, y, width, QuickSearchWidget.WidgetHeight), Notify_PawnsChanged, Notify_PawnsChanged);
            base.DoWindowContents(rect);
        }

        private IEnumerable<Pawn> Filter(IEnumerable<Pawn> source)
        {
            if (!search.filter.Active)
            {
                search.noResultsMatched = false;
                return source;
            }
            List<Pawn> kept = new List<Pawn>();
            foreach (Pawn p in source)
            {
                if (Matches(p))
                {
                    kept.Add(p);
                }
            }
            search.noResultsMatched = kept.Count == 0;
            return kept;
        }

        private bool Matches(Pawn p)
        {
            if (p.Name != null && (Hit(p.Name.ToStringShort) || Hit(p.Name.ToStringFull)))
            {
                return true;
            }
            return Hit(p.KindLabel) || Hit(p.LabelShortCap) || (p.def != null && Hit(p.def.label));
        }

        private bool Hit(string s)
        {
            return !string.IsNullOrEmpty(s) && search.filter.Matches(s);
        }
    }

    // Prisoners tab: prisoners and slaves of the colony on the current map.
    public class MainTabWindow_Prisoners : MainTabWindow_IcPawnTable
    {
        protected override string TabName { get { return "Prisoners"; } }

        protected override PawnTableDef PawnTableDef
        {
            get { return DefDatabase<PawnTableDef>.GetNamed("IC_Prisoners"); }
        }

        protected override IEnumerable<Pawn> SourcePawns()
        {
            MapPawns pawns = Find.CurrentMap.mapPawns;
            List<Pawn> result = new List<Pawn>(pawns.PrisonersOfColonySpawned);
            result.AddRange(pawns.SlavesOfColonySpawned);
            return result;
        }
    }

    // Raiders tab: every hostile humanlike on the current map, quest enemies
    // included. No fog filter (the user plays without fog). Prisoners held by
    // us are excluded through HostFaction.
    public class MainTabWindow_Raiders : MainTabWindow_IcPawnTable
    {
        protected override string TabName { get { return "Raiders"; } }

        protected override PawnTableDef PawnTableDef
        {
            get { return DefDatabase<PawnTableDef>.GetNamed("IC_Raiders"); }
        }

        protected override IEnumerable<Pawn> SourcePawns()
        {
            Map map = Find.CurrentMap;
            List<Pawn> result = new List<Pawn>();
            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (!p.Dead && p.RaceProps.Humanlike && p.HostFaction == null && p.HostileTo(Faction.OfPlayer))
                {
                    result.Add(p);
                }
            }
            return result;
        }
    }
}
