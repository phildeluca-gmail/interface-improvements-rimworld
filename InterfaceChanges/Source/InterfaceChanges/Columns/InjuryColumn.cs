using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Columns
{
    // IJ: injuries, location and type only, e.g. "LA Gun". Limbs as initials
    // (RL LL RA LA, shoulder = arm), other parts full label, then three letters of the injury kind.
    // Missing parts read "gone". Identical entries merge as "LA Cut x3".
    // The cell tooltip lists every injury by location and kind only (def label, never Hediff.LabelCap, which can carry the weapon or animal that caused it - ordered 2026-11-19). Scars (permanent injuries)
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

        // Limbs (def Leg, Arm, Hand, Foot, Shoulder) become first letters of each word of the label:
        // "right leg" -> RL, "left hand" -> LH, "right foot" -> RF. A shoulder reads as the arm on its side
        // ("right shoulder" -> RA). Every other part keeps its full label. Verified against
        // Core Defs Bodies_Humanlike.xml customLabels, 2026-11-19.
        private static string PartCode(BodyPartRecord part)
        {
            if (part == null)
            {
                return "Whole body";
            }
            string label = part.Label ?? "";
            string def = part.def != null ? part.def.defName : "";
            if (def == "Shoulder")
            {
                // Use the label of the arm this shoulder carries, so the side reads
                // right in any language; fall back to swapping the English word.
                BodyPartRecord arm = part.parts?.Find(p => p.def != null && p.def.defName == "Arm");
                label = arm != null ? (arm.Label ?? "") : label.Replace("shoulder", "arm");
            }
            else if (def != "Leg" && def != "Arm" && def != "Hand" && def != "Foot")
            {
                return label;
            }
            StringBuilder sb = new StringBuilder();
            foreach (string word in label.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                sb.Append(char.ToUpper(word[0]));
            }
            return sb.Length > 0 ? sb.ToString() : label;
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
                Add(counts, severity, PartCode(missing.Part) + " gone", 1000f);
                tip.AppendLine("gone (" + PartCode(missing.Part) + ")");
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
                tip.AppendLine(injury.def.LabelCap + " (" + PartCode(injury.Part) + ")" + " " + injury.Severity.ToString("F1"));
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
