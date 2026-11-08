using System.Collections.Generic;
using InterfaceChanges.Bills;
using UnityEngine;
using RimWorld;
using Verse;

namespace InterfaceChanges.Core
{
    public class InterfaceChangesSettings : ModSettings
    {
        // Default ON, ordered 2026-10-05: "The default setting for both is
        // KEEP equipped weapons."
        public bool keepEquippedWeapons = true;

        // Larger colonist bar group, ic-architecture.md section 9. Default on:
        // the ask was for it to happen, not to be found and switched on.
        public bool enlargeCurrentMapGroup = true;
        public float mapGroupSizePercent = 150f;
        // Ordered 2026-10-10: the enlarged group may wrap across up to this many rows.
        public int mapGroupMaxRows = 5;

        // Standard bill sets, ii-architecture.md section 13. Global, so they
        // outlive any save, like Standard Cargo's templates.
        public List<BillSet> billSets = new List<BillSet>();

        // On-screen message kinds, ii-architecture.md section 17.
        // Master switch for the message list, ii-architecture.md section 17 (13.1, 2026-11-07).
        public bool filterMessages = true;
        public List<InterfaceChanges.MessageKinds.MessageKindEntry> messageKinds = new List<InterfaceChanges.MessageKinds.MessageKindEntry>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref keepEquippedWeapons, "keepEquippedWeapons", true);
            Scribe_Values.Look(ref enlargeCurrentMapGroup, "enlargeCurrentMapGroup", true);
            Scribe_Values.Look(ref mapGroupSizePercent, "mapGroupSizePercent", 150f);
            Scribe_Values.Look(ref mapGroupMaxRows, "mapGroupMaxRows", 5);
            Scribe_Collections.Look(ref billSets, "billSets", LookMode.Deep);
            if (billSets == null) billSets = new List<BillSet>();
            Scribe_Values.Look(ref filterMessages, "filterMessages", true);
            Scribe_Collections.Look(ref messageKinds, "messageKinds", LookMode.Deep);
            if (messageKinds == null) messageKinds = new List<InterfaceChanges.MessageKinds.MessageKindEntry>();
        }

        public void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Drop everything keeps equipped weapons", ref keepEquippedWeapons,
                "On (default): the weapon a pawn is holding stays in their hands; everything else is dropped. Weapons carried in the inventory are still dropped. Off: the equipped weapon is dropped too.");
            listing.GapLine();
            bool wasOn = enlargeCurrentMapGroup;
            float wasPercent = mapGroupSizePercent;
            int wasRows = mapGroupMaxRows;
            listing.CheckboxLabeled("Enlarge the colonist bar group of the map on screen", ref enlargeCurrentMapGroup,
                "The group of colonists for the map you are looking at grows; the other groups slide outward and shrink only if they would run off the screen. Needs two or more groups (another map or a caravan).");
            if (enlargeCurrentMapGroup)
            {
                listing.Label("Size: " + mapGroupSizePercent.ToString("F0") + "% of vanilla's size");
                mapGroupSizePercent = Mathf.Round(listing.Slider(mapGroupSizePercent, 100f, 300f));
                listing.Label("Most rows the enlarged group may use: " + mapGroupMaxRows);
                mapGroupMaxRows = Mathf.Clamp(Mathf.RoundToInt(listing.Slider(mapGroupMaxRows, 1f, 5f)), 1, 5);
            }
            float used = listing.CurHeight;
            listing.End();
            InterfaceChanges.MessageKinds.MessageFilter.DoSettingsList(new Rect(inRect.x, inRect.y + used + 8f, inRect.width, inRect.height - used - 8f));
            if ((wasOn != enlargeCurrentMapGroup || wasPercent != mapGroupSizePercent || wasRows != mapGroupMaxRows)
                && Current.ProgramState == ProgramState.Playing)
            {
                Find.ColonistBar?.MarkColonistsDirty();
            }
        }
    }
}
