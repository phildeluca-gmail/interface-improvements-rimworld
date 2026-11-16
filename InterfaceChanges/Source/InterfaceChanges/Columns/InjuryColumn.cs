using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Columns
{
    // IJ: injuries, location and type only, e.g. "LSho Gun". Side letter (L/R)
    // plus three letters of the part, then three letters of the injury kind.
    // Missing parts read "Mis". Identical entries merge as "LArm Cut x3".
    // The cell tooltip lists every injury in full. Scars (permanent injuries)
    // are skipped. Ordered most severe first, missing parts before injuries.
    public class PawnColumnWorker_Injuries : PawnColumnWorker
    {
        private class Info
        {
            public List<string> Entries = new List<string>();
            public string Tip;
            public int Count;
        }

        private readonly CellCache<Info> cache = new CellCache<Info>();

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), def.width);
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            Rect line = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, 30f));
            Info info = cache.Get(pawn, Build);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(line, Fit(info.Entries, line.width));
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
            if (info.Tip != null && Mouse.IsOver(line))
            {
                TooltipHandler.TipRegion(line, info.Tip);
            }
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return cache.Get(a, Build).Count.CompareTo(cache.Get(b, Build).Count);
        }

        // Draws as many entries as fit, then "+n" for the rest.
        private static string Fit(List<string> entries, float width)
        {
            string text = "";
            for (int i = 0; i < entries.Count; i++)
            {
                string next = text.Length == 0 ? entries[i] : text + ", " + entries[i];
                string more = i < entries.Count - 1 ? " +" + (entries.Count - 1 - i) : "";
                if (Text.CalcSize(next + more).x > width && text.Length > 0)
                {
                    return text + " +" + (entries.Count - i);
                }
                text = next;
            }
            return text;
        }

        private static string PartCode(BodyPartRecord part)
        {
            if (part == null)
            {
                return "Who";
            }
            string label = part.Label ?? "";
            string side = "";
            if (label.StartsWith("left "))
            {
                side = "L";
                label = label.Substring(5);
            }
            else if (label.StartsWith("right "))
            {
                side = "R";
                label = label.Substring(6);
            }
            string word = label.Length > 3 ? label.Substring(0, 3) : label;
            return side + word.CapitalizeFirst();
        }

        private static string KindCode(string label)
        {
            if (label.NullOrEmpty())
            {
                return "???";
            }
            return (label.Length > 3 ? label.Substring(0, 3) : label).CapitalizeFirst();
        }

        private static Info Build(Pawn pawn)
        {
            Info info = new Info();
            HediffSet set = pawn.health?.hediffSet;
            if (set == null)
            {
                return info;
            }
            Dictionary<string, int> counts = new Dictionary<string, int>();
            Dictionary<string, float> severity = new Dictionary<string, float>();
            StringBuilder tip = new StringBuilder();

            foreach (Hediff_MissingPart missing in set.GetMissingPartsCommonAncestors())
            {
                Add(counts, severity, PartCode(missing.Part) + " Mis", 1000f);
                tip.AppendLine(missing.LabelCap + (missing.Part != null ? " (" + missing.Part.LabelCap + ")" : ""));
                info.Count++;
            }
            foreach (Hediff h in set.hediffs)
            {
                Hediff_Injury injury = h as Hediff_Injury;
                if (injury == null || !injury.Visible || injury.IsPermanent())
                {
                    continue;
                }
                Add(counts, severity, PartCode(injury.Part) + " " + KindCode(injury.def.label), injury.Severity);
                tip.AppendLine(injury.LabelCap + (injury.Part != null ? " (" + injury.Part.LabelCap + ")" : "") + " " + injury.Severity.ToString("F1"));
                info.Count++;
            }

            List<string> keys = new List<string>(counts.Keys);
            keys.Sort((x, y) => severity[y].CompareTo(severity[x]));
            foreach (string key in keys)
            {
                info.Entries.Add(counts[key] > 1 ? key + " x" + counts[key] : key);
            }
            info.Tip = tip.Length == 0 ? null : tip.ToString().TrimEnd();
            return info;
        }

        private static void Add(Dictionary<string, int> counts, Dictionary<string, float> severity, string key, float sev)
        {
            int c;
            counts.TryGetValue(key, out c);
            counts[key] = c + 1;
            float s;
            if (!severity.TryGetValue(key, out s) || sev > s)
            {
                severity[key] = sev;
            }
        }
    }
}
