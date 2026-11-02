using System.Collections.Generic;
using Verse;

namespace InterfaceChanges.Bills
{
    // One bill, stored by defName and plain values only. Mod settings load in
    // the Mod constructor, before defs exist, so nothing here may resolve a
    // def while loading (ii-architecture.md section 13). Resolved at apply time.
    public class SavedBill : IExposable
    {
        public string recipe;
        public string repeatMode;
        public string storeMode;
        public int repeatCount;
        public int targetCount;
        public bool pauseWhenSatisfied;
        public int unpauseWhenYouHave;
        public bool includeEquipped;
        public bool includeTainted;
        public bool limitToAllowedStuff;
        public bool suspended;
        public bool paused;
        public float ingredientSearchRadius;
        public string hpRange;
        public string qualityRange;
        public string skillRange;
        public bool slavesOnly;
        public bool mechsOnly;
        public bool nonMechsOnly;
        public bool isProduction;
        public string filterHp;
        public string filterQuality;
        public List<string> allowedDefs = new List<string>();
        public List<string> disallowedSpecials = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref recipe, "recipe");
            Scribe_Values.Look(ref repeatMode, "repeatMode");
            Scribe_Values.Look(ref storeMode, "storeMode");
            Scribe_Values.Look(ref repeatCount, "repeatCount");
            Scribe_Values.Look(ref targetCount, "targetCount");
            Scribe_Values.Look(ref pauseWhenSatisfied, "pauseWhenSatisfied");
            Scribe_Values.Look(ref unpauseWhenYouHave, "unpauseWhenYouHave");
            Scribe_Values.Look(ref includeEquipped, "includeEquipped");
            Scribe_Values.Look(ref includeTainted, "includeTainted");
            Scribe_Values.Look(ref limitToAllowedStuff, "limitToAllowedStuff");
            Scribe_Values.Look(ref suspended, "suspended");
            Scribe_Values.Look(ref paused, "paused");
            Scribe_Values.Look(ref ingredientSearchRadius, "ingredientSearchRadius");
            Scribe_Values.Look(ref hpRange, "hpRange");
            Scribe_Values.Look(ref qualityRange, "qualityRange");
            Scribe_Values.Look(ref skillRange, "skillRange");
            Scribe_Values.Look(ref slavesOnly, "slavesOnly");
            Scribe_Values.Look(ref mechsOnly, "mechsOnly");
            Scribe_Values.Look(ref nonMechsOnly, "nonMechsOnly");
            Scribe_Values.Look(ref isProduction, "isProduction");
            Scribe_Values.Look(ref filterHp, "filterHp");
            Scribe_Values.Look(ref filterQuality, "filterQuality");
            Scribe_Collections.Look(ref allowedDefs, "allowedDefs", LookMode.Value);
            Scribe_Collections.Look(ref disallowedSpecials, "disallowedSpecials", LookMode.Value);
            if (allowedDefs == null) allowedDefs = new List<string>();
            if (disallowedSpecials == null) disallowedSpecials = new List<string>();
        }
    }

    // A named standard bill set. sourceDef (the building def it was saved
    // from) only orders the menu; it never decides eligibility.
    public class BillSet : IExposable
    {
        public string name = "Standard bills";
        public string sourceDef;
        public List<SavedBill> bills = new List<SavedBill>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref sourceDef, "sourceDef");
            Scribe_Collections.Look(ref bills, "bills", LookMode.Deep);
            if (bills == null) bills = new List<SavedBill>();
        }
    }
}
