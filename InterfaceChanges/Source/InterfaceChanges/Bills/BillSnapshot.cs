using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace InterfaceChanges.Bills
{
    // Converts a live Bill to a SavedBill and back. Public members are read
    // directly; the filter's allowedDefs and disallowedSpecialFilters are
    // fields reached with AccessTools. All verified by reflection on
    // Assembly-CSharp 1.5.
    public static class BillSnapshot
    {
        public static SavedBill Capture(Bill bill)
        {
            var s = new SavedBill
            {
                recipe = bill.recipe.defName,
                suspended = bill.suspended,
                ingredientSearchRadius = bill.ingredientSearchRadius,
                skillRange = bill.allowedSkillRange.ToString(),
                slavesOnly = bill.SlavesOnly,
                mechsOnly = bill.MechsOnly,
                nonMechsOnly = bill.NonMechsOnly
            };
            ThingFilter f = bill.ingredientFilter;
            if (f != null)
            {
                foreach (ThingDef d in f.AllowedThingDefs) s.allowedDefs.Add(d.defName);
                var specials = AccessTools.Field(typeof(ThingFilter), "disallowedSpecialFilters").GetValue(f) as List<SpecialThingFilterDef>;
                if (specials != null) foreach (SpecialThingFilterDef sp in specials) s.disallowedSpecials.Add(sp.defName);
                s.filterHp = f.AllowedHitPointsPercents.ToString();
                s.filterQuality = f.AllowedQualityLevels.ToString();
            }
            Bill_Production p = bill as Bill_Production;
            if (p != null)
            {
                s.isProduction = true;
                s.repeatMode = p.repeatMode?.defName;
                s.storeMode = p.GetStoreMode()?.defName;
                s.repeatCount = p.repeatCount;
                s.targetCount = p.targetCount;
                s.pauseWhenSatisfied = p.pauseWhenSatisfied;
                s.unpauseWhenYouHave = p.unpauseWhenYouHave;
                s.includeEquipped = p.includeEquipped;
                s.includeTainted = p.includeTainted;
                s.limitToAllowedStuff = p.limitToAllowedStuff;
                s.paused = p.paused;
                s.hpRange = p.hpRange.ToString();
                s.qualityRange = p.qualityRange.ToString();
            }
            return s;
        }

        // True when saving this bill drops a named pawn or a zone/storage
        // reference. Counted by the caller for the message.
        public static bool LosesReferences(Bill bill)
        {
            Bill_Production p = bill as Bill_Production;
            bool zone = p != null && (p.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile || p.GetIncludeSlotGroup() != null);
            return zone || bill.PawnRestriction != null;
        }

        // The filter caches nothing we know of, but call whatever refresh it
        // offers (found by reflection) after the direct field writes.
        private static void RefreshFilter(ThingFilter f)
        {
            var m = AccessTools.Method(typeof(ThingFilter), "RecalculateDisplayRootCategory");
            if (m != null && m.GetParameters().Length == 0) m.Invoke(f, null);
            var cb = AccessTools.Field(typeof(ThingFilter), "settingsChangedCallback");
            var act = cb != null ? cb.GetValue(f) as System.Action : null;
            if (act != null) act();
        }

        // Null when the recipe is not loaded in this game.
        public static Bill Restore(SavedBill s)
        {
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(s.recipe);
            if (recipe == null) return null;
            Bill bill = BillUtility.MakeNewBill(recipe, null);
            bill.suspended = s.suspended;
            bill.ingredientSearchRadius = s.ingredientSearchRadius;
            if (!s.skillRange.NullOrEmpty()) bill.allowedSkillRange = IntRange.FromString(s.skillRange);
            if (s.slavesOnly) bill.SetAnySlaveRestriction();
            else if (s.mechsOnly) bill.SetAnyMechRestriction();
            else if (s.nonMechsOnly) bill.SetAnyNonMechRestriction();

            ThingFilter f = bill.ingredientFilter;
            if (f != null && s.allowedDefs != null)
            {
                var allowed = AccessTools.Field(typeof(ThingFilter), "allowedDefs").GetValue(f) as HashSet<ThingDef>;
                if (allowed != null)
                {
                    allowed.Clear();
                    foreach (string n in s.allowedDefs)
                    {
                        ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                        // Only defs the recipe can use.
                        if (d != null && (recipe.fixedIngredientFilter == null || recipe.fixedIngredientFilter.Allows(d))) allowed.Add(d);
                    }
                }
                var specials = AccessTools.Field(typeof(ThingFilter), "disallowedSpecialFilters").GetValue(f) as List<SpecialThingFilterDef>;
                if (specials != null)
                {
                    specials.Clear();
                    foreach (string n in s.disallowedSpecials)
                    {
                        SpecialThingFilterDef d = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(n);
                        if (d != null) specials.Add(d);
                    }
                }
                if (!s.filterHp.NullOrEmpty()) f.AllowedHitPointsPercents = FloatRange.FromString(s.filterHp);
                if (!s.filterQuality.NullOrEmpty()) f.AllowedQualityLevels = QualityRange.FromString(s.filterQuality);
                RefreshFilter(f);
            }

            Bill_Production p = bill as Bill_Production;
            if (p != null && s.isProduction)
            {
                BillRepeatModeDef rm = DefDatabase<BillRepeatModeDef>.GetNamedSilentFail(s.repeatMode);
                if (rm != null) p.repeatMode = rm;
                BillStoreModeDef sm = DefDatabase<BillStoreModeDef>.GetNamedSilentFail(s.storeMode);
                // SpecificStockpile needs a zone that no longer exists; keep the default.
                if (sm != null && sm != BillStoreModeDefOf.SpecificStockpile) p.SetStoreMode(sm, null);
                p.repeatCount = s.repeatCount;
                p.targetCount = s.targetCount;
                p.pauseWhenSatisfied = s.pauseWhenSatisfied;
                p.unpauseWhenYouHave = s.unpauseWhenYouHave;
                p.includeEquipped = s.includeEquipped;
                p.includeTainted = s.includeTainted;
                p.limitToAllowedStuff = s.limitToAllowedStuff;
                p.paused = s.paused;
                if (!s.hpRange.NullOrEmpty()) p.hpRange = FloatRange.FromString(s.hpRange);
                if (!s.qualityRange.NullOrEmpty()) p.qualityRange = QualityRange.FromString(s.qualityRange);
            }
            return bill;
        }
    }
}
