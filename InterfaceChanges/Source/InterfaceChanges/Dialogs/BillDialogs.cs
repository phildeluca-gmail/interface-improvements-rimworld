using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using InterfaceChanges.Bills;
using InterfaceChanges.Core;
using UnityEngine;
using Verse;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Dialogs
{
    // Asks for a name, then calls back. Used for "Save bills as standard..."
    // and for renaming a saved set.
    public class Dialog_NameBillSet : Window
    {
        private readonly Action<string> onAccept;
        private readonly string title;
        private string text;
        private bool focused;

        public Dialog_NameBillSet(string title, string initial, Action<string> onAccept)
        {
            this.title = title;
            this.text = initial;
            this.onAccept = onAccept;
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(360f, 150f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 26f), title);
            GUI.SetNextControlName("BillSetName");
            text = Widgets.TextField(new Rect(0f, 32f, inRect.width, 30f), text);
            if (!focused)
            {
                GUI.FocusControl("BillSetName");
                focused = true;
            }
            bool enter = Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 32f, inRect.width, 32f), "OK") || enter)
            {
                string trimmed = (text ?? "").Trim();
                if (trimmed.Length == 0)
                {
                    Logger.Tell("Give the set a name.", LookTargets.Invalid);
                    return;
                }
                Close();
                onAccept(trimmed);
            }
        }
    }

    // Small choice shown once when applying: add to the existing bills or
    // replace them. Used by "Apply copied bills".
    public class Dialog_AddOrReplace : Window
    {
        private readonly string title;
        private readonly Action<bool> onChoose;
        private readonly Thing thing;

        public Dialog_AddOrReplace(string title, Action<bool> onChoose, Thing thing)
        {
            this.title = title;
            this.onChoose = onChoose;
            this.thing = thing;
            forcePause = false;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(380f, 170f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (BillTools.InvalidReason(thing) != null) { Close(); return; }
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 50f), title);
            float w = (inRect.width - 8f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 70f, w, 32f), "Add to existing bills"))
            {
                Close();
                onChoose(false);
            }
            if (Widgets.ButtonText(new Rect(w + 8f, inRect.height - 70f, w, 32f), "Replace existing bills"))
            {
                Close();
                onChoose(true);
            }
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 32f, inRect.width, 28f), "Cancel"))
            {
                Close();
            }
        }
    }

    // The "Standard bills" dialog, laid out like Standard Cargo for Vehicles
    // template dialog: sets on the left, the selected set's bills on the
    // right. Sets for this building's def sort first; sets it cannot use are
    // greyed with the reason. One add/replace toggle serves Apply.
    public class Dialog_StandardBills : Window
    {
        private readonly Thing thing;
        private BillSet selected;
        private bool replace;
        private Vector2 listScroll;
        private Vector2 billScroll;
        private List<BillSet> ordered = new List<BillSet>();
        private readonly Dictionary<BillSet, string> reasons = new Dictionary<BillSet, string>();

        public Dialog_StandardBills(Thing thing)
        {
            this.thing = thing;
            Rebuild();
            forcePause = false;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(720f, 500f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (BillTools.InvalidReason(thing) != null)
            {
                Close();
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width - 200f, 34f), "Standard bills - " + thing.LabelShortCap);
            Text.Font = GameFont.Small;
            IBillGiver giver = (IBillGiver)thing;
            bool hasBills = giver.BillStack.Count > 0;
            if (Widgets.ButtonText(new Rect(inRect.width - 190f, 2f, 160f, 28f), "Save current bills", true, true, hasBills) && hasBills)
            {
                Thing t = thing;
                Find.WindowStack.Add(new Dialog_NameBillSet("Name for the new standard bill set",
                    t.def.label.CapitalizeFirst() + " bills", n => { BillSet made = BillTools.SaveAsSet(t, n); if (made != null) selected = made; Rebuild(); }));
            }

            float top = 40f;
            float leftW = 240f;
            Rect left = new Rect(0f, top, leftW, inRect.height - top);
            Rect right = new Rect(leftW + 8f, top, inRect.width - leftW - 8f, inRect.height - top);
            DrawList(left);
            DrawDetail(right);
        }

        // Rebuilt on open, save, rename, delete and apply, not every frame.
        private void Rebuild()
        {
            reasons.Clear();
            foreach (BillSet set in InterfaceChangesMod.Settings.billSets)
            {
                reasons[set] = BillTools.UnusableReason(thing, set);
            }
            ordered = InterfaceChangesMod.Settings.billSets
                .OrderBy(s => reasons[s] != null ? 2 : (s.sourceDef == thing.def.defName ? 0 : 1))
                .ToList();
        }

        private void DrawList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            var sets = ordered;
            Rect inner = rect.ContractedBy(4f);
            if (sets.Count == 0)
            {
                Widgets.Label(inner, "None saved yet. Use \"Save current bills\".");
                return;
            }
            const float rowH = 44f;
            Rect view = new Rect(0f, 0f, inner.width - 16f, sets.Count * rowH);
            Widgets.BeginScrollView(inner, ref listScroll, view);
            for (int i = 0; i < sets.Count; i++)
            {
                BillSet set = sets[i];
                Rect row = new Rect(0f, i * rowH, view.width, rowH - 2f);
                string reason = reasons.ContainsKey(set) ? reasons[set] : null;
                if (set == selected) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                GUI.color = reason != null ? Color.grey : Color.white;
                Widgets.Label(new Rect(row.x + 4f, row.y, row.width - 8f, 22f), set.name);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(row.x + 4f, row.y + 22f, row.width - 8f, 20f),
                    reason ?? (set.bills.Count + " bills" + (set.sourceDef == thing.def.defName ? ", from this building type" : "")));
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row)) selected = set;
            }
            Widgets.EndScrollView();
        }

        private void DrawDetail(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(6f);
            if (selected == null || !InterfaceChangesMod.Settings.billSets.Contains(selected))
            {
                selected = null;
                Widgets.Label(inner, "Select a set on the left to see its bills.");
                return;
            }
            Widgets.Label(new Rect(inner.x, inner.y, inner.width - 170f, 26f), selected.name);
            BillSet target = selected;
            if (Widgets.ButtonText(new Rect(inner.xMax - 164f, inner.y, 78f, 26f), "Rename"))
            {
                Find.WindowStack.Add(new Dialog_NameBillSet("Rename standard bill set", target.name, n => { BillTools.RenameSet(target, n); Rebuild(); }));
            }
            if (Widgets.ButtonText(new Rect(inner.xMax - 82f, inner.y, 82f, 26f), "Delete"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Delete the standard bill set \"" + target.name + "\"?", () => { BillTools.DeleteSet(target); Rebuild(); }, true, null));
            }

            float footer = 70f;
            Rect list = new Rect(inner.x, inner.y + 32f, inner.width, inner.height - 32f - footer);
            Widgets.DrawBox(list);
            Rect listIn = list.ContractedBy(2f);
            const float rowH = 24f;
            Rect view = new Rect(0f, 0f, listIn.width - 16f, selected.bills.Count * rowH);
            Widgets.BeginScrollView(listIn, ref billScroll, view);
            for (int i = 0; i < selected.bills.Count; i++)
            {
                SavedBill b = selected.bills[i];
                RecipeDef r = DefDatabase<RecipeDef>.GetNamedSilentFail(b.recipe);
                string label = r != null ? r.label.CapitalizeFirst() : b.recipe + " (not loaded)";
                bool here = r != null && thing.def.AllRecipes.Contains(r);
                GUI.color = here ? Color.white : Color.grey;
                Widgets.Label(new Rect(4f, i * rowH, view.width - 8f, rowH),
                    label + " - " + b.repeatMode + (here ? "" : " (not available here)"));
                GUI.color = Color.white;
            }
            Widgets.EndScrollView();

            string reason = reasons.ContainsKey(selected) ? reasons[selected] : null;
            float y = inner.yMax - footer + 6f;
            Widgets.CheckboxLabeled(new Rect(inner.x, y, inner.width, 24f), "Replace existing bills (off = add to them)", ref replace);
            Rect apply = new Rect(inner.x, y + 28f, inner.width, 30f);
            if (Widgets.ButtonText(apply, "Apply to this building", true, true, reason == null) && reason == null)
            {
                BillTools.ApplySet(thing, selected, replace);
                Rebuild();
            }
            if (reason != null) TooltipHandler.TipRegion(apply, reason);
        }
    }
}
