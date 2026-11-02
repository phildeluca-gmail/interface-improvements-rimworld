using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Columns
{
    // Text column for the Assign tab: the pawn's title as the Rename dialog
    // shows it. Pawn_StoryTracker.title is the player's edit (null when never
    // edited); Pawn_StoryTracker.TitleDefault is the backstory title.
    // Verified by IL: Title returns title when non-null, else TitleDefault.
    // A blank edit is also treated as unset here.
    public class PawnColumnWorker_Title : PawnColumnWorker_Text
    {
        protected override string GetTextFor(Pawn pawn)
        {
            Pawn_StoryTracker story = pawn?.story;
            if (story == null)
            {
                return "";
            }
            string text = string.IsNullOrWhiteSpace(story.title) ? story.TitleDefault : story.title;
            return text.NullOrEmpty() ? "" : text.CapitalizeFirst();
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            base.DoCell(rect, pawn, table);
            string text = GetTextFor(pawn);
            if (!text.NullOrEmpty() && Text.CalcSize(text).x > rect.width)
            {
                TooltipHandler.TipRegion(rect, text);
            }
        }
    }
}
