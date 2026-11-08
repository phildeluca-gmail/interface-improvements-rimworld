using System;
using System.Collections.Generic;
using System.Linq;
using InterfaceChanges.Core;
using InterfaceChanges.Dialogs;
using InterfaceChanges.Utility;
using RimWorld;
using Verse;

namespace InterfaceChanges.Bills
{
    // Copy, paste, save and apply for the bills of one building. See
    // ii-architecture.md section 13. Every public entry point logs one line
    // for the event it performs and tells the player (message plus log).
    public static class BillTools
    {
        // Copies of the bills last taken with "Copy all bills". Held in
        // memory only, shared by every building, gone when the game closes.
        private static readonly List<Bill> clipboard = new List<Bill>();
        private static string clipboardSource = "";

        private class Candidate
        {
            public string recipeName;
            public RecipeDef recipe;
            public Func<Bill> make;
        }

        public static int ClipboardCount
        {
            get { return clipboard.Count; }
        }

        public static string ClipboardSource
        {
            get { return clipboardSource ?? ""; }
        }

        // Null when the building can still take bills; otherwise why not.
        public static string InvalidReason(Thing thing)
        {
            if (thing == null || thing.Destroyed) return "the building no longer exists";
            if (!thing.Spawned) return "the building is not on the map";
            if (thing.Faction != Faction.OfPlayer) return "the building is not yours";
            return null;
        }

        // Clones in the clipboard point at the old game's zones and pawns, so
        // the list is emptied whenever a game is started or loaded
        // (HotkeyComponent.FinalizeInit). Logged only if something was held.
        public static void ClearClipboard()
        {
            if (clipboard.Count == 0) return;
            int n = clipboard.Count;
            clipboard.Clear();
            clipboardSource = "";
            Logger.Info("Bill clipboard cleared (" + n + " bills) because a game was started or loaded.");
        }

        public static IBillGiver GiverOf(Thing thing)
        {
            return thing as IBillGiver;
        }

        public static void CopyAll(Thing thing)
        {
            IBillGiver giver = GiverOf(thing);
            int total = giver.BillStack.Count;
            var copies = new List<Bill>();
            int uncopyable = 0;
            foreach (Bill bill in giver.BillStack.Bills)
            {
                if (!CanCopy(bill)) { uncopyable++; continue; }
                // Bill.Clone is the game's own copy used by its Copy button.
                copies.Add(bill.Clone());
            }
            clipboard.Clear();
            clipboard.AddRange(copies);
            clipboardSource = thing.LabelShortCap;
            string text = "Copied " + copies.Count + " of " + total + " bills from " + thing.LabelShortCap
                + (uncopyable > 0 ? " (" + uncopyable + " cannot be copied)" : "") + ".";
            Report(thing, text);
        }

        // Bill.CanCopy is protected; read it the way the game's own copy
        // button would. A failed read counts as copyable (Clone is guarded).
        private static bool CanCopy(Bill bill)
        {
            var prop = HarmonyLib.AccessTools.Property(typeof(Bill), "CanCopy");
            return prop == null || (bool)prop.GetValue(bill, null);
        }

        public static void PasteClipboard(Thing thing, bool replace)
        {
            var list = clipboard.Select(b => new Candidate
            {
                recipeName = b.recipe.defName,
                recipe = b.recipe,
                make = () => { Bill c = b.Clone(); c.InitializeAfterClone(); return c; }
            }).ToList();
            Apply(thing, "clipboard from " + clipboardSource, list, replace, false);
        }

        public static BillSet SaveAsSet(Thing thing, string name)
        {
            string invalid = InvalidReason(thing);
            if (invalid != null)
            {
                Logger.Tell("Cannot save bills: " + invalid + ".", thing != null ? new LookTargets(thing) : LookTargets.Invalid);
                return null;
            }
            if (NameTaken(name, null))
            {
                Logger.Tell("A standard bill set named \"" + name + "\" already exists. Choose another name.", new LookTargets(thing));
                return null;
            }
            IBillGiver giver = GiverOf(thing);
            var set = new BillSet { name = name, sourceDef = thing.def.defName };
            int lost = 0;
            foreach (Bill bill in giver.BillStack.Bills)
            {
                set.bills.Add(BillSnapshot.Capture(bill));
                if (BillSnapshot.LosesReferences(bill)) lost++;
            }
            InterfaceChangesMod.Settings.billSets.Add(set);
            InterfaceChangesMod.SaveSettings();
            string text = "Saved standard bill set \"" + name + "\" from " + thing.LabelShortCap + ": " + set.bills.Count + " bills"
                + (lost > 0 ? "; " + lost + " had a named pawn or a zone/storage that is not stored and will use the default" : "") + ".";
            Report(thing, text);
            return set;
        }

        public static void ApplySet(Thing thing, BillSet set, bool replace)
        {
            var list = set.bills.Select(sb => new Candidate
            {
                recipeName = sb.recipe,
                recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(sb.recipe),
                make = () => BillSnapshot.Restore(sb)
            }).ToList();
            Apply(thing, "set \"" + set.name + "\"", list, replace, true);
        }

        public static void DeleteSet(BillSet set)
        {
            InterfaceChangesMod.Settings.billSets.Remove(set);
            InterfaceChangesMod.SaveSettings();
            Logger.Info("Standard bill set \"" + set.name + "\" deleted (" + set.bills.Count + " bills) from the Standard bills dialog.");
        }

        // True when another set (not "except") already has this name, ignoring case.
        private static bool NameTaken(string name, BillSet except)
        {
            return InterfaceChangesMod.Settings.billSets.Any(s => s != except
                && string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase));
        }

        public static bool RenameSet(BillSet set, string newName)
        {
            if (NameTaken(newName, set))
            {
                Logger.Tell("A standard bill set named \"" + newName + "\" already exists. The set was not renamed.", LookTargets.Invalid);
                return false;
            }
            string old = set.name;
            set.name = newName;
            InterfaceChangesMod.SaveSettings();
            Logger.Info("Standard bill set \"" + old + "\" renamed to \"" + newName + "\".");
            return true;
        }

        // Null when the set can be applied here; otherwise the reason it cannot.
        public static string UnusableReason(Thing thing, BillSet set)
        {
            if (set.bills.Count == 0) return "empty set";
            if (!CanDoAny(thing, set)) return "no recipe fits this building";
            List<RecipeDef> recipes = thing.def.AllRecipes;
            bool anyNow = set.bills.Any(b => recipes.Any(r => r.defName == b.recipe && r.AvailableNow));
            return anyNow ? null : "nothing in it is researched yet";
        }

        public static bool CanDoAny(Thing thing, BillSet set)
        {
            List<RecipeDef> recipes = thing.def.AllRecipes;
            return set.bills.Any(b => recipes.Any(r => r.defName == b.recipe));
        }

        private static void Apply(Thing thing, string source, List<Candidate> list, bool replace, bool isSet)
        {
            string invalid = InvalidReason(thing);
            if (invalid != null)
            {
                Logger.Tell("Cannot change bills from " + source + ": " + invalid + ".", thing != null ? new LookTargets(thing) : LookTargets.Invalid);
                return;
            }
            IBillGiver giver = GiverOf(thing);
            BillStack stack = giver.BillStack;
            List<RecipeDef> recipes = thing.def.AllRecipes;
            int removed = 0;
            if (list.Count == 0)
            {
                Report(thing, "Nothing to paste on " + thing.LabelShortCap + " from " + source + "; no bills were changed.");
                return;
            }
            // Check every candidate before touching the stack, so a replace
            // never deletes bills when nothing valid would take their place.
            int notHere = 0, unavailable = 0, notResearched = 0, failed = 0, limit = 0, cleared = 0, invalidFailed = 0;
            var valid = new List<KeyValuePair<Candidate, Bill>>();
            foreach (Candidate c in list)
            {
                if (c.recipe == null) { unavailable++; continue; }
                if (!recipes.Contains(c.recipe)) { notHere++; continue; }
                if (!c.recipe.AvailableNow) { notResearched++; continue; }
                Bill made;
                try { made = c.make(); }
                catch (Exception e)
                {
                    Logger.Warning("Could not build bill " + c.recipeName + ": " + e.Message);
                    failed++;
                    continue;
                }
                if (made == null) { failed++; continue; }
                valid.Add(new KeyValuePair<Candidate, Bill>(c, made));
            }
            if (valid.Count == 0)
            {
                Report(thing, "Nothing was pasted on " + thing.LabelShortCap + " from " + source + "; no bills were changed ("
                    + BuildReasons(notHere, 0, unavailable, notResearched, failed) + ").");
                return;
            }
            if (replace)
            {
                // BillStack.Delete is the game's own removal (tells the bill
                // and the giver); iterate a copy because it edits the list.
                foreach (Bill old in stack.Bills.ToList())
                {
                    stack.Delete(old);
                    removed++;
                }
            }
            int added = 0;
            foreach (var kv in valid)
            {
                if (stack.Count >= BillStack.MaxCount) { limit++; continue; }
                stack.AddBill(kv.Value);
                // Our clipboard is not in BillUtility.GlobalBills, so nothing has
                // validated it: this clears a dead pawn or a deleted stockpile.
                // Needs billStack, which AddBill just set.
                // A modded override that throws must not leave a Replace half done.
                object[] before = References(kv.Value);
                try { kv.Value.ValidateSettings(); }
                catch (Exception e)
                {
                    Logger.Warning("ValidateSettings failed on pasted bill " + kv.Key.recipeName + ": " + e.Message);
                    invalidFailed++;
                }
                if (!before.SequenceEqual(References(kv.Value))) cleared++;
                added++;
            }
            int skipped = notHere + limit + unavailable + notResearched + failed;
            string reasons = BuildReasons(notHere, limit, unavailable, notResearched, failed);
            string text = (replace ? "Replaced" : "Added") + " bills on " + thing.LabelShortCap + " from " + source + ": "
                + added + " pasted"
                + (replace ? ", " + removed + " old removed" : "")
                + (skipped > 0 ? ", " + skipped + " skipped (" + reasons + ")" : "")
                + (cleared > 0 ? ", " + cleared + " had a pawn or stockpile that is not available here, cleared" : "")
                + (invalidFailed > 0 ? ", " + invalidFailed + " could not be checked" : "")
                + (isSet && added > 0 ? ". Pawn and zone settings are not stored in standard sets." : ".");
            Report(thing, text);
        }

        // The pawn and stockpile references ValidateSettings may clear
        // (public accessors: PawnRestriction, GetSlotGroup, GetIncludeSlotGroup).
        private static object[] References(Bill bill)
        {
            var prod = bill as Bill_Production;
            return new object[]
            {
                bill.PawnRestriction,
                bill.GetSlotGroup(),
                prod != null ? prod.GetIncludeSlotGroup() : null
            };
        }

        private static string BuildReasons(int notHere, int limit, int unavailable, int notResearched, int failed)
        {
            var reasons = new List<string>();
            if (notHere > 0) reasons.Add(notHere + " not available here");
            if (limit > 0) reasons.Add(limit + " over the limit of " + BillStack.MaxCount);
            if (unavailable > 0) reasons.Add(unavailable + " recipe not loaded in this game");
            if (notResearched > 0) reasons.Add(notResearched + " not researched");
            if (failed > 0) reasons.Add(failed + " could not be built");
            return string.Join("; ", reasons.ToArray());
        }

        private static void Report(Thing thing, string text)
        {
            Logger.Report(text, thing);
        }
    }
}
