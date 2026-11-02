using System;
using System.Collections.Generic;
using InterfaceChanges.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Drop
{
    // The "Drop everything" order, ic-architecture.md section 4. A standalone
    // copy of Do Not Be Lazy's; nothing here refers to that mod.
    //
    // Queue order follows Pawn_JobTracker.TryTakeOrderedJob: the first call per
    // pawn passes requestQueueing false (replaces the current job), every later
    // call passes true (appends).
    public static class DropEverything
    {
        private static readonly string launchStamp = DateTime.Now.ToString("HHmm");
        private static int orderCounter;
        private const int GiveUpTicks = 15000; // 6 in-game hours

        // Each order remembers the exact jobs it queued, so it ends when those
        // are gone, not when the whole queue is empty (other mods or the game
        // may queue their own jobs behind ours and keep the queue non-empty).
        private class PendingOrder
        {
            public string id;
            public List<Job> jobs = new List<Job>();
            // JobMaker pools finished Job objects and reuses them, so a bare
            // reference can match an unrelated later job. The def is recorded
            // at queue time and must still match.
            public Dictionary<Job, JobDef> defs = new Dictionary<Job, JobDef>();
            public int startTick;
            // Worn apparel and the equipped weapon this order queued, so the end line can say which came off.
            public List<Thing> queuedGear = new List<Thing>();
            // Inventory stacks the drop jobs actually dropped, reported by
            // JobDriver_DropInventoryStack; written once in the end state.
            public List<string> droppedStacks = new List<string>();
        }

        private static readonly Dictionary<Pawn, PendingOrder> pending = new Dictionary<Pawn, PendingOrder>();

        public static void Queue(Pawn pawn)
        {
            if (pawn?.jobs == null)
            {
                return;
            }

            bool keepWeapon = InterfaceChangesMod.Settings.keepEquippedWeapons;
            string orderId = "IC-" + launchStamp + "-" + (++orderCounter);
            Logger.Info("ORDER " + orderId + " issued: " + pawn.LabelShort + " drop everything (keep equipped weapon: " + (keepWeapon ? "on" : "off") + ")");

            if (pending.TryGetValue(pawn, out PendingOrder previous))
            {
                Logger.Info("ORDER " + previous.id + " ended (replaced by new order " + orderId + "): " + pawn.LabelShort + " drop everything");
                LogEndState(pawn, previous);
                pending.Remove(pawn);
            }

            var order = new PendingOrder { id = orderId, startTick = Find.TickManager.TicksGame };
            bool firstJob = true;
            bool startedLogged = false;
            int apparelQueued = 0, apparelLocked = 0, apparelWarcasket = 0;
            int weaponQueued = 0, weaponKept = 0;
            int inventoryQueued = 0;
            // Item names by outcome, 2026-10-25: listed in the one "pressed" line instead of one line per item.
            var namesLocked = new List<string>();
            var namesWarcasket = new List<string>();
            var namesApparelQueued = new List<string>();
            var namesRefused = new List<string>();
            var namesInventory = new List<string>();
            string weaponNote = "";

            if (pawn.apparel != null)
            {
                // Snapshot - WornApparel is the tracker's live list.
                foreach (Apparel apparel in new List<Apparel>(pawn.apparel.WornApparel))
                {
                    if (pawn.apparel.IsLocked(apparel))
                    {
                        apparelLocked++;
                        namesLocked.Add(apparel.LabelCap);
                        NameAndSkip(pawn, apparel, "locked");
                        continue;
                    }

                    if (WarcasketCompat.IsWarcasket(apparel))
                    {
                        apparelWarcasket++;
                        namesWarcasket.Add(apparel.LabelCap);
                        NameAndSkip(pawn, apparel, "a warcasket");
                        continue;
                    }

                    string label = apparel.LabelCap;
                    Job job = JobMaker.MakeJob(JobDefOf.RemoveApparel, apparel);
                    if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: !firstJob))
                    {
                        firstJob = false;
                        order.jobs.Add(job);
                        order.defs[job] = job.def;
                        apparelQueued++;
                        order.queuedGear.Add(apparel);
                        LogStarted(ref startedLogged, orderId, pawn, job);
                        namesApparelQueued.Add(label);
                    }
                    else
                    {
                        namesRefused.Add(label + " (remove-apparel refused)");
                    }
                }
            }

            if (pawn.equipment?.Primary != null)
            {
                ThingWithComps weapon = pawn.equipment.Primary;
                string label = weapon.LabelCap;
                if (keepWeapon)
                {
                    weaponKept++;
                    weaponNote = label + " kept (equipped weapon)";
                }
                else
                {
                    Job job = JobMaker.MakeJob(JobDefOf.DropEquipment, weapon);
                    if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: !firstJob))
                    {
                        firstJob = false;
                        order.jobs.Add(job);
                        order.defs[job] = job.def;
                        weaponQueued++;
                        order.queuedGear.Add(weapon);
                        LogStarted(ref startedLogged, orderId, pawn, job);
                        weaponNote = label + " queued to be dropped";
                    }
                    else
                    {
                        weaponNote = label + " NOT queued (drop-equipment refused)";
                    }
                }
            }

            if (pawn.inventory != null)
            {
                // Inventory weapons are dropped like anything else; only the
                // equipped weapon is covered by the setting.
                foreach (Thing thing in new List<Thing>(pawn.inventory.innerContainer))
                {
                    Job job = JobMaker.MakeJob(IcJobDefOf.IC_DropInventoryStack, thing);
                    if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing: !firstJob))
                    {
                        firstJob = false;
                        order.jobs.Add(job);
                        order.defs[job] = job.def;
                        inventoryQueued++;
                        namesInventory.Add(thing.stackCount > 1 ? thing.LabelShortCap + " x" + thing.stackCount : thing.LabelShortCap);
                        LogStarted(ref startedLogged, orderId, pawn, job);
                    }
                }
            }

            int totalQueued = apparelQueued + weaponQueued + inventoryQueued;
            string outcome = totalQueued == 0
                ? "nothing to drop - left: locked " + NameList(namesLocked) + " | warcasket " + NameList(namesWarcasket)
                    + " | refused " + NameList(namesRefused) + " | weapon: " + (weaponNote.Length == 0 ? "none" : weaponNote)
                :"apparel " + apparelQueued + " queued (" + apparelLocked + " locked, " + apparelWarcasket + " warcasket skipped), "
                    + "weapon " + weaponQueued + " queued, " + weaponKept + " kept, inventory " + inventoryQueued + " queued"
                    + " | apparel queued: " + NameList(namesApparelQueued)
                    + " | locked: " + NameList(namesLocked)
                    + " | warcasket skipped: " + NameList(namesWarcasket)
                    + " | refused: " + NameList(namesRefused)
                    + " | weapon: " + (weaponNote.Length == 0 ? "none" : weaponNote)
                    + " | inventory queued: " + NameList(namesInventory);
            Logger.Info(pawn.LabelShort + ": drop everything pressed - " + outcome);

            if (totalQueued == 0)
            {
                Logger.Info("ORDER " + orderId + " ended (failed to start: nothing to drop or every order refused): " + pawn.LabelShort + " drop everything");
            }
            else
            {
                pending[pawn] = order;
            }
        }

        private static string NameList(List<string> names)
        {
            return names.Count == 0 ? "none" : string.Join(", ", names.ToArray());
        }

        // Called by JobDriver_DropInventoryStack after a stack is dropped. The
        // per-stack log line was replaced (2026-10-25) by this record, written
        // in the order's end state, which PollOrders always reaches.
        public static void NoteDropped(Pawn pawn, string stackLabel)
        {
            if (pawn != null && pending.TryGetValue(pawn, out PendingOrder order))
            {
                order.droppedStacks.Add(stackLabel);
            }
        }

        private static void LogStarted(ref bool startedLogged, string orderId, Pawn pawn, Job job)
        {
            if (!startedLogged)
            {
                startedLogged = true;
                Logger.Info("ORDER " + orderId + " started: " + pawn.LabelShort + " " + job.def.defName + " " + job.targetA);
            }
        }

        // Called every 60 ticks. The vanilla drop jobs have no end hook short
        // of a patch, so an order ends when none of the jobs it queued is the
        // current job or still in the queue.
        public static void PollOrders()
        {
            if (pending.Count == 0)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            var done = new List<Pawn>();
            foreach (KeyValuePair<Pawn, PendingOrder> pair in pending)
            {
                Pawn pawn = pair.Key;
                PendingOrder order = pair.Value;
                string reason = null;
                if (pawn == null || pawn.Destroyed || pawn.Dead)
                {
                    reason = "pawn dead or gone";
                }
                else if (pawn.Downed)
                {
                    reason = "pawn downed";
                }
                else
                {
                    order.jobs.RemoveAll(j => !StillQueued(pawn, j, order.defs[j]));
                    if (order.jobs.Count == 0)
                    {
                        reason = "nothing left that can be dropped - every queued drop job has run or been replaced";
                    }
                    else if (now - order.startTick > GiveUpTicks)
                    {
                        reason = "gave up after " + (now - order.startTick) + " ticks with " + order.jobs.Count + " drop job(s) still queued";
                    }
                }

                if (reason != null)
                {
                    Logger.Info("ORDER " + order.id + " ended (" + reason + "): " + pawn?.LabelShort + " drop everything");
                    LogEndState(pawn, order);
                    done.Add(pawn);
                }
            }

            foreach (Pawn pawn in done)
            {
                pending.Remove(pawn);
            }
        }

        // Never lets a read failure escape PollOrders or Queue.
        private static void LogEndState(Pawn pawn, PendingOrder order)
        {
            try
            {
                Logger.Info(DescribeEndState(pawn, order));
            }
            catch (System.Exception ex)
            {
                Logger.Info("ORDER " + order.id + " end state: could not be read (" + ex.GetType().Name + ")");
            }
        }

        // End-state record for an order: read from the pawn at the moment the
        // order ends. Gear the order queued is split into off and still on.
        private static string DescribeEndState(Pawn pawn, PendingOrder order)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
            {
                return "ORDER " + order.id + " end state: pawn dead or gone, nothing to read";
            }

            var wornNow = new List<Thing>();
            if (pawn.apparel != null)
            {
                foreach (Apparel a in pawn.apparel.WornApparel)
                {
                    wornNow.Add(a);
                }
            }

            ThingWithComps weapon = pawn.equipment?.Primary;
            var off = new List<string>();
            var stillOn = new List<string>();
            foreach (Thing t in order.queuedGear)
            {
                bool held = wornNow.Contains(t) || (weapon != null && weapon == t);
                (held ? stillOn : off).Add(t.LabelShortCap);
            }

            var worn = new List<string>();
            foreach (Thing t in wornNow)
            {
                worn.Add(t.LabelShortCap);
            }

            var inv = new List<string>();
            if (pawn.inventory != null)
            {
                foreach (Thing t in pawn.inventory.innerContainer)
                {
                    inv.Add(t.stackCount > 1 ? t.LabelShortCap + " x" + t.stackCount : t.LabelShortCap);
                }
            }

            return "ORDER " + order.id + " end state: " + pawn.LabelShort
                + " | taken off/dropped (" + off.Count + "): " + (off.Count == 0 ? "none" : string.Join(", ", off.ToArray()))
                + " | queued but still on (" + stillOn.Count + "): " + (stillOn.Count == 0 ? "none" : string.Join(", ", stillOn.ToArray()))
                + " | now wearing (" + worn.Count + "): " + (worn.Count == 0 ? "none" : string.Join(", ", worn.ToArray()))
                + " | weapon: " + (weapon == null ? "none" : weapon.LabelShortCap)
                + " | inventory dropped (" + order.droppedStacks.Count + "): " + NameList(order.droppedStacks)
                + " | inventory (" + inv.Count + "): " + (inv.Count == 0 ? "empty" : string.Join(", ", inv.ToArray()));
        }

        private static bool StillQueued(Pawn pawn, Job job, JobDef def)
        {
            if (pawn.jobs == null || job.def != def)
            {
                return false;
            }

            if (pawn.jobs.curJob == job)
            {
                return true;
            }

            // JobQueue.Contains is the game's own identity test and skips null entries.
            return pawn.jobs.jobQueue != null && pawn.jobs.jobQueue.Contains(job);
        }

        // Called when a game is loaded or started: pawns held from the
        // previous game are stale and would never end.
        public static void EndAllForLoad()
        {
            foreach (KeyValuePair<Pawn, PendingOrder> pair in pending)
            {
                Logger.Info("ORDER " + pair.Value.id + " ended (game loaded): " + pair.Key?.LabelShort + " drop everything");
            }

            pending.Clear();
        }

        private static void NameAndSkip(Pawn pawn, Apparel apparel, string reason)
        {
            // On-screen only; the item is named in the "drop everything pressed" log line (2026-10-25).
            Messages.Message(pawn.LabelShort + ": leaving " + apparel.LabelCap + " - " + reason + ".", pawn, MessageTypeDefOf.RejectInput, false);
        }
    }
}
