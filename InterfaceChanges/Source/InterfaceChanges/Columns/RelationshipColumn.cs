using System.Collections.Generic;
using RimWorld;
using Verse;

namespace InterfaceChanges.Columns
{
    // RL: the prisoner's family and other relations to your free colonists, as
    // the vanilla relationship letter words them: "brother of Abi". Built on
    // the reading in OPEN_ITEMS Q35. TO DROP THIS COLUMN: delete this file and
    // the IC_Relationship def (Defs/TabColumns.xml) and its <li> (Defs/Tabs.xml).
    // Uses PawnRelationUtility.GetMostImportantRelation(colonist, prisoner),
    // labelled for the prisoner, exactly as vanilla's own letter does.
    public class PawnColumnWorker_Relationship : PawnColumnWorker_CellText
    {
        private class Info
        {
            public string Text = "";
            public string Tip;
        }

        private readonly CellCache<Info> cache = new CellCache<Info>();

        private static Info Build(Pawn pawn)
        {
            Info info = new Info();
            if (pawn.relations == null || Find.CurrentMap == null)
            {
                return info;
            }
            List<string> lines = new List<string>();
            string best = null;
            float bestImportance = float.MinValue;
            foreach (Pawn colonist in Find.CurrentMap.mapPawns.FreeColonists)
            {
                PawnRelationDef rel = colonist.GetMostImportantRelation(pawn);
                if (rel == null)
                {
                    continue;
                }
                string line = rel.GetGenderSpecificLabel(pawn) + " of " + colonist.LabelShort;
                lines.Add(line);
                if (rel.importance > bestImportance)
                {
                    bestImportance = rel.importance;
                    best = line;
                }
            }
            if (best != null)
            {
                info.Text = lines.Count > 1 ? best + " +" + (lines.Count - 1) : best;
                info.Tip = string.Join("\n", lines.ToArray());
            }
            return info;
        }

        protected override string GetTextFor(Pawn pawn)
        {
            return cache.Get(pawn, Build).Text;
        }

        protected override string GetTip(Pawn pawn)
        {
            return cache.Get(pawn, Build).Tip;
        }
    }
}
