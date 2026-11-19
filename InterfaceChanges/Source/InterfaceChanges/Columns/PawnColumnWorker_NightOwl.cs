using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Columns
{
    // Schedule tab ("Restrict" PawnTableDef): an owl icon for pawns with the
    // Night Owl trait. Verified: TraitDefOf has no NightOwl, so the def is
    // looked up by defName "NightOwl" (Core Traits_Singular.xml);
    // TraitSet.HasTrait(TraitDef) exists. ii-architecture.md section 34.
    public class PawnColumnWorker_NightOwl : PawnColumnWorker
    {
        private static TraitDef nightOwl;
        private static bool looked;
        private static Texture2D icon;

        private static bool IsNightOwl(Pawn pawn)
        {
            if (!looked)
            {
                looked = true;
                nightOwl = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
            }
            return nightOwl != null && pawn?.story?.traits != null && pawn.story.traits.HasTrait(nightOwl);
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (!IsNightOwl(pawn))
            {
                return;
            }
            if (icon == null)
            {
                icon = ContentFinder<Texture2D>.Get("UI/IINightOwl", false) ?? BaseContent.BadTex;
            }
            float size = Mathf.Min(24f, rect.width, rect.height);
            Rect r = new Rect(rect.x + (rect.width - size) / 2f, rect.y + (rect.height - size) / 2f, size, size);
            GUI.DrawTexture(r, icon);
            TooltipHandler.TipRegion(rect, "Night owl");
        }

        public override int GetMinWidth(PawnTable table)
        {
            return 28;
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return 28;
        }

        public override int GetOptimalWidth(PawnTable table)
        {
            return 28;
        }

        // Night owls first.
        public override int Compare(Pawn a, Pawn b)
        {
            return IsNightOwl(b).CompareTo(IsNightOwl(a));
        }
    }

    // Logs once, after defs and XML patches are loaded (the Mod constructor
    // runs before patching, so it cannot tell whether the column landed).
    [StaticConstructorOnStartup]
    public static class NightOwlColumnStartup
    {
        static NightOwlColumnStartup()
        {
            PawnTableDef table = DefDatabase<PawnTableDef>.GetNamedSilentFail("Restrict");
            PawnColumnDef column = DefDatabase<PawnColumnDef>.GetNamedSilentFail("IC_NightOwl");
            int index = (table != null && table.columns != null && column != null) ? table.columns.IndexOf(column) : -1;
            if (index < 0)
            {
                Utility.Logger.Warning("Night owl column NOT in the Schedule tab (Restrict table missing or patch did not apply).");
                return;
            }
            Utility.Logger.Info("Night owl column added to the Schedule tab at position " + index + ".");
        }
    }
}
