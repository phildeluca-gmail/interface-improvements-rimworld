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
                if (BillTools.ClipboardCount > 0)
                {
                    yield return new Command_PasteBills(parent);
                }
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
            defaultDesc = "Copy the bills of this building, or open the standard bill sets. Right-click shows the same menu.";
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

            yield return new FloatMenuOption("Copy all bills" + (hasBills ? "" : " (none to copy)"),
                hasBills ? (System.Action)(() => BillTools.CopyAll(t)) : null);
            yield return new FloatMenuOption("Standard bills...", () =>
            {
                Utility.Logger.Info("Standard bills dialog opened on " + t.LabelShortCap + ".");
                Find.WindowStack.Add(new Dialog_StandardBills(t));
            });
        }
    }

    // Second button, shown only while bills are copied. When several buildings
    // are selected the game merges their identical buttons into one and, by
    // default, clicks every one of them (Gizmo.alsoClickIfOtherInGroupClicked),
    // which would open one dialog per building. So the click is turned into a
    // single group action: ProcessGroupInput receives the merged group (the
    // clicked button included) and opens one dialog for all of them.
    public class Command_PasteBills : Command_Action
    {
        private readonly Thing thing;

        public Command_PasteBills(Thing thing)
        {
            this.thing = thing;
            defaultLabel = "Paste bills";
            string source = BillTools.ClipboardSource;
            defaultDesc = "Paste the " + BillTools.ClipboardCount + " copied bills"
                + (source.Length > 0 ? " (from " + source + ")" : "")
                + " onto this building, adding to its bills or replacing them.";
            icon = TexButton.Paste;
            alsoClickIfOtherInGroupClicked = false;
            action = () => { };
        }

        public override void ProcessGroupInput(Event ev, List<Gizmo> group)
        {
            var targets = new List<Thing> { thing };
            foreach (Gizmo g in group)
            {
                var other = g as Command_PasteBills;
                if (other != null && !targets.Contains(other.thing)) targets.Add(other.thing);
            }
            int count = BillTools.ClipboardCount;
            string names = string.Join(", ", targets.Select(t => t.LabelShortCap).ToArray());
            string log = "Paste bills pressed on " + names + " (" + targets.Count + " buildings), "
                + count + " copied bills from " + BillTools.ClipboardSource + ".";
            string title = "Paste " + count + " copied bills to "
                + (targets.Count == 1 ? targets[0].LabelShortCap : targets.Count + " buildings") + ":";
            Utility.Logger.Info(log);
            Find.WindowStack.Add(new Dialog_AddOrReplace(title, replace =>
            {
                foreach (Thing t in targets) BillTools.PasteClipboard(t, replace);
            }, targets, names));
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
