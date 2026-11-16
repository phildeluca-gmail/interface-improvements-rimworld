using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Columns
{
    // Shared base for the text columns of the Prisoners and Raiders tabs
    // (ii-architecture.md section 21). Draws a single clipped line and a
    // tooltip. GetTextFor / GetTip are called every frame for every visible
    // row, so anything costly sits behind CellCache.
    public abstract class PawnColumnWorker_CellText : PawnColumnWorker_Text
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            Rect line = new Rect(rect.x, rect.y, rect.width, Mathf.Min(rect.height, 30f));
            string text = GetTextFor(pawn);
            if (text == null)
            {
                return;
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(line, text.Truncate(line.width));
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
            if (Mouse.IsOver(line))
            {
                string tip = GetTip(pawn);
                if (!tip.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(line, tip);
                }
            }
        }
    }

    // Per-pawn value cache, refreshed at most once per 60 game ticks, so a
    // table redraw (every frame) does not rebuild strings. Keyed by thingIDNumber.
    public class CellCache<T> where T : class
    {
        private struct Entry
        {
            public int Tick;
            public T Value;
        }

        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();

        public T Get(Pawn pawn, Func<Pawn, T> build)
        {
            int now = Find.TickManager.TicksGame;
            Entry e;
            if (entries.TryGetValue(pawn.thingIDNumber, out e) && now - e.Tick < 60 && now >= e.Tick)
            {
                return e.Value;
            }
            e = new Entry { Tick = now, Value = build(pawn) };
            entries[pawn.thingIDNumber] = e;
            return e.Value;
        }
    }
}
