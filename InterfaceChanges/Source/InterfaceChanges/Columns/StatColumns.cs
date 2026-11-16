using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Columns
{
    // One of the 12 skills; the skill comes from the def's name suffix
    // (IC_Skill_Shooting -> Shooting). Level plus a passion flame, "-" when the
    // skill is disabled. Defined in Defs/TabColumns.xml.
    public class PawnColumnWorker_Skill : PawnColumnWorker
    {
        private SkillDef skillDef;

        private SkillDef Skill
        {
            get
            {
                if (skillDef == null)
                {
                    skillDef = DefDatabase<SkillDef>.GetNamed(def.defName.Substring("IC_Skill_".Length));
                }
                return skillDef;
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), def.width);
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            Rect line = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, 30f));
            SkillRecord rec = pawn.skills?.GetSkill(Skill);
            string text = rec == null || rec.TotallyDisabled ? "-" : rec.Level.ToString();
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(new Rect(line.x + 2f, line.y, 24f, line.height), text);
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
            if (rec != null && !rec.TotallyDisabled && rec.passion != Passion.None)
            {
                Texture2D icon = rec.passion == Passion.Major ? SkillUI.PassionMajorIcon : SkillUI.PassionMinorIcon;
                GUI.DrawTexture(new Rect(line.xMax - 17f, line.y + (line.height - 14f) / 2f, 14f, 14f), icon);
            }
            if (Mouse.IsOver(line))
            {
                string tip = Skill.LabelCap;
                if (rec != null)
                {
                    tip += rec.TotallyDisabled ? ": disabled" : ": level " + rec.Level
                        + (rec.passion == Passion.None ? "" : ", " + (rec.passion == Passion.Major ? "major" : "minor") + " passion");
                }
                TooltipHandler.TipRegion(line, tip);
            }
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return Score(a).CompareTo(Score(b));
        }

        private int Score(Pawn p)
        {
            SkillRecord rec = p.skills?.GetSkill(Skill);
            if (rec == null || rec.TotallyDisabled)
            {
                return -1;
            }
            return rec.Level * 4 + (int)rec.passion;
        }
    }

    // HE: overall health, the same percentage vanilla draws as the name bar.
    public class PawnColumnWorker_Health : PawnColumnWorker_CellText
    {
        protected override string GetTextFor(Pawn pawn)
        {
            return pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent();
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return a.health.summaryHealth.SummaryHealthPercent.CompareTo(b.health.summaryHealth.SummaryHealthPercent);
        }
    }

    // MO: mood bar level; blank when the pawn has no mood need.
    public class PawnColumnWorker_Mood : PawnColumnWorker_CellText
    {
        protected override string GetTextFor(Pawn pawn)
        {
            Need_Mood mood = pawn.needs?.mood;
            return mood == null ? "" : mood.CurLevelPercentage.ToStringPercent();
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return (a.needs?.mood?.CurLevel ?? -1f).CompareTo(b.needs?.mood?.CurLevel ?? -1f);
        }
    }

    // RE: recruitment resistance (Pawn_GuestTracker.Resistance).
    public class PawnColumnWorker_Resistance : PawnColumnWorker_CellText
    {
        protected override string GetTextFor(Pawn pawn)
        {
            return pawn.guest == null ? "-" : pawn.guest.Resistance.ToString("F1");
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return (a.guest?.Resistance ?? -1f).CompareTo(b.guest?.Resistance ?? -1f);
        }
    }

    // WI: enslavement will (Pawn_GuestTracker.will, -1 means never set).
    public class PawnColumnWorker_Will : PawnColumnWorker_CellText
    {
        protected override string GetTextFor(Pawn pawn)
        {
            return pawn.guest == null || pawn.guest.will < 0f ? "-" : pawn.guest.will.ToString("F1");
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return (a.guest?.will ?? -1f).CompareTo(b.guest?.will ?? -1f);
        }
    }

    // IT: the prisoner's interaction mode (recruit, convert, maintain only...).
    public class PawnColumnWorker_Interaction : PawnColumnWorker_CellText
    {
        protected override string GetTextFor(Pawn pawn)
        {
            PrisonerInteractionModeDef mode = pawn.guest?.ExclusiveInteractionMode;
            return mode == null ? "-" : mode.LabelCap.ToString();
        }
    }

    // WE: the equipped primary weapon.
    public class PawnColumnWorker_Weapon : PawnColumnWorker_CellText
    {
        protected override string GetTextFor(Pawn pawn)
        {
            ThingWithComps w = pawn.equipment?.Primary;
            return w == null ? "-" : w.def.LabelCap.ToString();
        }

        protected override string GetTip(Pawn pawn)
        {
            ThingWithComps w = pawn.equipment?.Primary;
            return w == null ? null : w.LabelCap.ToString();
        }
    }

    // AM: sharp-damage armor on the torso. GetStatValue on a pawn does not add
    // worn armor (StatWorker only adds equippedStatOffsets), so this combines
    // the pawn's own rating with each torso-covering garment the way layered
    // armor works: 1 - product(1 - rating). UNVERIFIED against the game's own
    // figure; the tooltip lists the pieces.
    public class PawnColumnWorker_Armor : PawnColumnWorker_CellText
    {
        private class Info
        {
            public float Total;
            public string Text;
            public string Tip;
        }

        private readonly CellCache<Info> cache = new CellCache<Info>();

        private static Info Build(Pawn pawn)
        {
            float pass = 1f - Mathf.Clamp01(pawn.GetStatValue(StatDefOf.ArmorRating_Sharp));
            StringBuilder tip = new StringBuilder();
            List<Apparel> worn = pawn.apparel?.WornApparel;
            if (worn != null)
            {
                for (int i = 0; i < worn.Count; i++)
                {
                    Apparel a = worn[i];
                    if (!a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        continue;
                    }
                    float r = Mathf.Clamp01(a.GetStatValue(StatDefOf.ArmorRating_Sharp));
                    pass *= 1f - r;
                    if (r > 0f)
                    {
                        tip.AppendLine(a.LabelCap + ": " + r.ToStringPercent());
                    }
                }
            }
            float total = 1f - pass;
            return new Info
            {
                Total = total,
                Text = total.ToStringPercent(),
                Tip = tip.Length == 0 ? null : "Sharp armor, torso\n" + tip.ToString().TrimEnd()
            };
        }

        protected override string GetTextFor(Pawn pawn)
        {
            return cache.Get(pawn, Build).Text;
        }

        protected override string GetTip(Pawn pawn)
        {
            return cache.Get(pawn, Build).Tip;
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return cache.Get(a, Build).Total.CompareTo(cache.Get(b, Build).Total);
        }
    }
}
