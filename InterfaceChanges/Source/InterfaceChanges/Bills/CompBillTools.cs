using System.Collections.Generic;
using System.Linq;
using InterfaceChanges.Core;
using InterfaceChanges.Dialogs;
using InterfaceChanges.Utility;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Bills
{
    public class CompProperties_BillTools : CompProperties
    {
        public CompProperties_BillTools()
        {
            compClass = typeof(CompBillTools);
        }
    }

    // Gives every building that holds bills the "Bills" button. Added to the
    // defs at startup by BillToolsStartup; no Harmony patch (section 13).
    public class CompBillTools : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction == Faction.OfPlayer && parent is IBillGiver && parent is Building)
            {
                yield return new Command_BillTools(parent);
            }
        }
    }

    // Left click opens the menu; right click on the button offers the same
    // options (Gizmo.RightClickFloatMenuOptions, virtual, verified).
    public class Command_BillTools : Command_Action
    {
        private readonly Thing thing;

        public Command_BillTools(Thing thing)
        {
            this.thing = thing;
            defaultLabel = "Copy all bills";
            defaultDesc = "Copy the bills of this building, apply copied bills here, or open the standard bill sets. Right-click shows the same menu.";
            icon = TexButton.Copy;
            action = () => Find.WindowStack.Add(new FloatMenu(Options().ToList()));
        }

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get { return Options(); }
        }

        private IEnumerable<FloatMenuOption> Options()
        {
            Thing t = thing;
            IBillGiver giver = (IBillGiver)t;
            bool hasBills = giver.BillStack.Count > 0;
            bool clip = BillTools.ClipboardCount > 0;

            yield return new FloatMenuOption("Copy all bills" + (hasBills ? "" : " (none to copy)"),
                hasBills ? (System.Action)(() => BillTools.CopyAll(t)) : null);
            yield return new FloatMenuOption("Apply copied bills" + (clip ? " (" + BillTools.ClipboardCount + ")" : " (nothing copied)"),
                clip ? (System.Action)(() => Find.WindowStack.Add(new Dialog_AddOrReplace(
                    "Apply " + BillTools.ClipboardCount + " copied bills to " + t.LabelShortCap + ":",
                    replace => BillTools.PasteClipboard(t, replace), t))) : null);
            yield return new FloatMenuOption("Standard bills...", () =>
            {
                Utility.Logger.Info("Standard bills dialog opened on " + t.LabelShortCap + ".");
                Find.WindowStack.Add(new Dialog_StandardBills(t));
            });
        }
    }

    // Runs once after all defs are loaded and adds the comp to every
    // ThingDef whose class holds bills.
    [StaticConstructorOnStartup]
    public static class BillToolsStartup
    {
        static BillToolsStartup()
        {
            int added = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.thingClass == null
                    || !typeof(IBillGiver).IsAssignableFrom(def.thingClass)
                    || !typeof(ThingWithComps).IsAssignableFrom(def.thingClass)
                    || !typeof(Building).IsAssignableFrom(def.thingClass)
                    || typeof(Pawn).IsAssignableFrom(def.thingClass)
                    || typeof(Corpse).IsAssignableFrom(def.thingClass))
                {
                    continue;
                }
                if (def.comps == null) def.comps = new List<CompProperties>();
                if (def.comps.Any(c => c.compClass == typeof(CompBillTools))) continue;
                def.comps.Add(new CompProperties_BillTools());
                added++;
            }
            Utility.Logger.Info("Bills button added to " + added + " building defs that hold bills.");
        }
    }
}
