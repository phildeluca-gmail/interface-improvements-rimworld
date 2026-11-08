using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Patches
{
    // Needs tab, mood list: adds the location of the other pawn a
    // thought is about, to the right of vanilla's mood value. (The opinion text
    // was removed 2026-11-08 by the user's order.) See ii-architecture.md section 14.
    // Two postfixes, applied by hand so a missing target is logged instead
    // of throwing out of PatchAll:
    //   NeedsCardUtility.DrawThoughtGroup(Rect, Thought, Pawn)  (private) - draws the extra text
    //     to the right of the vanilla row.
    //   ITab_Pawn_Needs.UpdateSize() (protected, declared on that type) - widens
    //     only the pawn Needs tab to fit that text. NeedsCardUtility.GetSize is
    //     deliberately NOT patched: the caravan, Vehicle Framework and outpost
    //     needs panels also call it and keep their normal width.
    // Vehicle Framework is never referenced: a vehicle is recognised by the
    // name of its type in the base-class chain.
    public static class NeedsSocialPatch
    {
        // Vanilla row layout (verified): value at x+235 width 32, Ideology icon at x+277 width 20.
        private const float TextX = 305f;
        private const float CardBaseWidth = 580f;
        // card width = 570 + text width: the row is card - 225 (needs column) - 20 (margins) - 16 (scrollbar) wide, text starts at TextX.
        private const float WidthOverhead = 570f;
        // Row width = card width minus 225 (needs column), 20 (margins) and 16 (scrollbar).
        private const float RowOverhead = 261f;
        private const string SleepAloneDefName = "WantToSleepWithSpouseOrLover";
        private const string VehiclePawnTypeName = "Vehicles.VehiclePawn";

        private static readonly List<Thought> scratch = new List<Thought>();
        private static readonly List<Thought> scratchWidth = new List<Thought>();
        private static bool loggedDrawError;
        private static bool loggedSizeError;

        private static int cachePawnId = -1;
        private static float cacheTime = -1000f;
        private static float cacheWidth;

        public static void Apply(Harmony harmony)
        {
            MethodInfo draw = AccessTools.DeclaredMethod(typeof(NeedsCardUtility), "DrawThoughtGroup");
            MethodInfo size = AccessTools.DeclaredMethod(typeof(ITab_Pawn_Needs), "UpdateSize");
            if (draw == null || size == null)
            {
                Logger.Warning("NeedsSocialPatch: NeedsCardUtility.DrawThoughtGroup or ITab_Pawn_Needs.UpdateSize not found, Needs tab will not show location.");
                return;
            }
            harmony.Patch(draw, postfix: new HarmonyMethod(AccessTools.Method(typeof(NeedsSocialPatch), nameof(DrawPostfix))));
            harmony.Patch(size, postfix: new HarmonyMethod(AccessTools.Method(typeof(NeedsSocialPatch), nameof(UpdateSizePostfix))));
            Logger.Info("NeedsSocialPatch applied to NeedsCardUtility.DrawThoughtGroup and ITab_Pawn_Needs.UpdateSize.");
        }

        // Draws after vanilla has drawn the row. Text.WordWrap and anchor are
        // restored to what vanilla leaves (wrap on, anchor UpperLeft, then set
        // by the caller's loop).
        public static void DrawPostfix(Rect rect, Thought group, Pawn pawn, bool __result)
        {
            if (!__result)
            {
                return;
            }
            // Normal-width panels (caravan, vehicle, outpost) have room for only a few
            // letters, so nothing is drawn there.
            if (rect.width <= CardBaseWidth - RowOverhead)
            {
                return;
            }
            try
            {
                string location;
                if (!TryBuild(pawn, group, scratch, out location))
                {
                    return;
                }

                GameFont oldFont = Text.Font;
                TextAnchor oldAnchor = Text.Anchor;
                bool oldWrap = Text.WordWrap;
                try
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.WordWrap = false;

                    float w = Text.CalcSize(location).x;
                    Widgets.Label(new Rect(rect.x + TextX, rect.y, w + 4f, rect.height), location);
                }
                finally
                {
                    Text.Font = oldFont;
                    Text.Anchor = oldAnchor;
                    Text.WordWrap = oldWrap;
                }
            }
            catch (Exception ex)
            {
                if (!loggedDrawError)
                {
                    loggedDrawError = true;
                    Logger.Warning("NeedsSocialPatch: drawing failed once and will keep failing silently: " + ex);
                }
            }
        }

        // Widens the card to fit the longest added text among this pawn's
        // mood lines. Recomputed at most twice a second (real time, so it
        // also refreshes while the game is paused) and whenever the pawn changes.
        // ITab_Pawn_Needs.UpdateSize sets the protected field InspectTabBase.size
        // (injected here as ___size) from NeedsCardUtility.GetSize(SelPawn); SelPawn
        // is Find.Selector.SingleSelectedThing as Pawn.
        public static void UpdateSizePostfix(ref Vector2 ___size)
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || pawn.needs == null || pawn.needs.mood == null)
            {
                return;
            }
            try
            {
                float now = Time.realtimeSinceStartup;
                if (pawn.thingIDNumber != cachePawnId || now - cacheTime > 0.5f || now < cacheTime)
                {
                    float measured = MeasureWidest(pawn);
                    cachePawnId = pawn.thingIDNumber;
                    cacheTime = now;
                    cacheWidth = measured;
                }
                if (cacheWidth > 0f)
                {
                    float want = Mathf.Max(CardBaseWidth, WidthOverhead + cacheWidth);
                    float cap = Mathf.Max(CardBaseWidth, UI.screenWidth - 40f);
                    ___size = new Vector2(Mathf.Min(want, cap), ___size.y);
                }
            }
            catch (Exception ex)
            {
                if (!loggedSizeError)
                {
                    loggedSizeError = true;
                    Logger.Warning("NeedsSocialPatch: sizing failed once and will keep failing silently: " + ex);
                }
            }
        }

        private static float MeasureWidest(Pawn pawn)
        {
            List<Thought> groups = new List<Thought>();
            PawnNeedsUIUtility.GetThoughtGroupsInDisplayOrder(pawn.needs.mood, groups);
            GameFont oldFont = Text.Font;
            float widest = 0f;
            try
            {
                Text.Font = GameFont.Small;
                for (int i = 0; i < groups.Count; i++)
                {
                    string location;
                    if (!TryBuild(pawn, groups[i], scratchWidth, out location))
                    {
                        continue;
                    }
                    widest = Mathf.Max(widest, Text.CalcSize(location).x);
                }
            }
            finally
            {
                Text.Font = oldFont;
            }
            return widest;
        }

        // Works out the location text for one mood line. False means "add nothing":
        // a merged line, an invisible line, a thought not about another pawn, or
        // a pawn whose location is nothing (for example a dead one).
        private static bool TryBuild(Pawn pawn, Thought group, List<Thought> list, out string location)
        {
            location = null;
            if (pawn.needs == null || pawn.needs.mood == null)
            {
                return false;
            }
            pawn.needs.mood.thoughts.GetMoodThoughts(group, list);
            if (list.Count == 0)
            {
                return false;
            }
            // Several causes merged into one "x N" line stay as vanilla shows them.
            if (list.Count > 1)
            {
                list.Clear();
                return false;
            }
            Thought thought = PawnNeedsUIUtility.GetLeadingThoughtInGroup(list);
            list.Clear();
            if (thought == null || !thought.VisibleInNeedsTab)
            {
                return false;
            }

            if (thought.def != null && thought.def.defName == SleepAloneDefName)
            {
                return BuildSleepingAlone(pawn, out location);
            }

            Pawn other = null;
            ISocialThought social = thought as ISocialThought;
            if (social != null)
            {
                other = social.OtherPawn();
            }
            else
            {
                Thought_Memory memory = thought as Thought_Memory;
                if (memory != null)
                {
                    other = memory.otherPawn;
                }
            }
            if (other == null || other == pawn)
            {
                return false;
            }

            location = LocationOf(pawn, other);
            return !location.NullOrEmpty();
        }

        // "Sleeping alone" has no partner pawn (plain Thought_Situational). The
        // partners are the worker's own list, with the same first filter
        // (ThoughtWorker_WantToSleepWithSpouseOrLover, verified): colonist, seen by
        // the player, same slave status. World pawns (for example in flight) are
        // deliberately listed, by the user's order. Dead partners are skipped:
        // allowDead false only drops Destroyed pawns, so a dead pawn who still
        // exists gets through. The worker's last filter (would the pawn
        // willingly share a bed) is applied below.
        private static bool BuildSleepingAlone(Pawn pawn, out string location)
        {
            location = null;
            List<DirectPawnRelation> partners = LovePartnerRelationUtility.ExistingLovePartners(pawn, false);
            if (partners.NullOrEmpty())
            {
                return false;
            }
            bool slave = pawn.IsSlaveOfColony;
            string text = null;
            for (int i = 0; i < partners.Count; i++)
            {
                Pawn p = partners[i].otherPawn;
                if (p.Dead || !p.IsColonist || !p.relations.everSeenByPlayer || p.IsSlaveOfColony != slave)
                {
                    continue;
                }
                // The worker's last filter: the doer (this pawn) would willingly
                // share a bed; the event depends on whether the partner is a spouse.
                HistoryEventDef sharedBed = partners[i].def == PawnRelationDefOf.Spouse
                    ? HistoryEventDefOf.SharedBed_Spouse
                    : HistoryEventDefOf.SharedBed_NonSpouse;
                if (!new HistoryEvent(sharedBed, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                {
                    continue;
                }
                string loc = LocationOf(pawn, p);
                string part = loc.NullOrEmpty() ? p.LabelShort : p.LabelShort + ": " + loc;
                text = text == null ? part : text + "; " + part;
            }
            location = text;
            return !text.NullOrEmpty();
        }

        // Map-level location of "other" as seen from "viewer". Null or empty
        // means "show nothing".
        private static string LocationOf(Pawn viewer, Pawn other)
        {
            if (other.Dead)
            {
                return null;
            }
            Map map = other.MapHeld;
            if (map != null)
            {
                if (map == viewer.MapHeld)
                {
                    return "same map";
                }
                return map.Parent != null ? map.Parent.LabelCap.ToString() : null;
            }

            // Off every map: walk the chain of holders upward. A caravan wins
            // over a vehicle when both are found (a vehicle inside a caravan).
            Caravan caravan = null;
            Pawn vehicle = null;
            bool inPods = false;
            IThingHolder holder = other.ParentHolder;
            int guard = 0;
            while (holder != null && guard++ < 20)
            {
                if (caravan == null && holder is Caravan)
                {
                    caravan = (Caravan)holder;
                }
                if (vehicle == null && holder is Pawn && holder != other && IsVehiclePawn(holder.GetType()))
                {
                    vehicle = (Pawn)holder;
                }
                // Pods in flight: pawn -> ActiveDropPodInfo.innerContainer ->
                // ActiveDropPodInfo -> TravelingTransportPods (verified, this build).
                if (holder is TravelingTransportPods)
                {
                    inPods = true;
                }
                holder = holder.ParentHolder;
            }
            if (caravan != null)
            {
                return caravan.LabelCap;
            }
            if (vehicle != null)
            {
                // LabelShort is the name the player gave the vehicle (Pawn.Name, set by
                // Vehicle Framework's rename dialog), else the vehicle kind's label.
                return "off-map in " + vehicle.LabelShort;
            }
            if (inPods)
            {
                return "transport pod";
            }
            // Off every map, no caravan, no vehicle. A kidnapped pawn keeps the
            // player's faction (KidnappedPawnsTracker.Kidnap), so ask each faction's
            // tracker first and show the kidnapper's name.
            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; i++)
            {
                Faction f = factions[i];
                if (f.kidnapped != null && f.kidnapped.KidnappedPawnsListForReading.Contains(other))
                {
                    return f.Name;
                }
            }
            // Missing in action: no faction, or the player's own faction (off every
            // map, in no caravan, vehicle or pods, not kidnapped). Another faction
            // shows its name.
            Faction faction = other.Faction;
            if (faction == null || faction == Faction.OfPlayer)
            {
                return "MIA";
            }
            return faction.Name;
        }

        private static bool IsVehiclePawn(Type type)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                if (t.FullName == VehiclePawnTypeName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
