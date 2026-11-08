using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InterfaceChanges.Core;
using UnityEngine;
using Verse;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.MessageKinds
{
    // ii-architecture.md section 17. One prefix on Verse.Messages.Message(Message, bool),
    // the method every overload ends in. It works out the calling method (the "kind"),
    // lists the kind in the settings the first time it is seen, and skips the message
    // when the player has unticked the kind. Returning false from the prefix skips
    // the archive entry, the on-screen message and the sound together.
    public static class MessageFilter
    {
        // Q11.5 answered "11.5 b" (2026-11-07): an unchecked kind is not shown and
        // makes no sound, but IS added to the message history (Find.Archive), as
        // vanilla does when historical is true. Set true to drop it entirely.
        private const bool UncheckedSkipsHistory = false;

        // Assemblies of our own mods: controlled from each mod's own checkbox,
        // never listed here (ordered 2026-11-07).
        private static readonly HashSet<string> OwnAssemblies = new HashSet<string>
        {
            "DoNotBeLazy", "InterfaceChanges", "BeLazy", "PawnHotgroups", "HighlightCorpsesWithTech",
            "RimWarOdds", "StandardCargoForVehicles", "DialogImprovements", "SettlementInformation",
            "ShowWhatsTattered", "NotifyRipe", "ConsumptionRates"
        };

        private static readonly ConditionalWeakTable<Message, string> kindOf = new ConditionalWeakTable<Message, string>();
        private static readonly Dictionary<string, MessageKindEntry> index = new Dictionary<string, MessageKindEntry>();
        private static readonly Dictionary<string, int> suppressedCounts = new Dictionary<string, int>();
        private static readonly HashSet<string> loggedSuppressed = new HashSet<string>();
        private static FieldInfo liveMessagesField;
        private static HashSet<string> loadedAssemblies;
        private static bool settingsDirty;
        private static bool failureLogged;
        private static List<MessageKindEntry> sorted;
        private static int sortedCount = -1;
        private static Vector2 scroll;

        public static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(Messages), nameof(Messages.Message), new[] { typeof(Message), typeof(bool) });
            liveMessagesField = AccessTools.Field(typeof(Messages), "liveMessages");
            if (target == null)
            {
                Logger.Warning("MessageFilter: Messages.Message(Message, bool) not found, message checkboxes disabled.");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(MessageFilter), nameof(Prefix))));
            Logger.Info("MessageFilter applied to Messages.Message(Message, bool).");
        }

        public static bool Prefix(Message msg, bool historical)
        {
            try
            {
                if (msg == null || InterfaceChangesMod.Settings == null || !InterfaceChangesMod.Settings.filterMessages) return true;
                string assembly, label;
                string key = Identify(out assembly, out label);
                if (key == null) return true;
                kindOf.Remove(msg);
                kindOf.Add(msg, key);
                MessageKindEntry entry = Register(key, assembly, label, msg.text);
                if (entry.shown) return true;

                int n;
                suppressedCounts.TryGetValue(key, out n);
                suppressedCounts[key] = n + 1;
                if (loggedSuppressed.Add(key))
                {
                    Logger.Info("Message kind " + label + " [" + assembly + "] is unchecked: hiding its messages. First one: " + Trim(msg.text));
                }
                if (!UncheckedSkipsHistory && historical && Find.Archive != null)
                {
                    // Vanilla archives a repeat once while the first is live (Messages.AcceptsMessage).
                    // A hidden message never goes live, so mirror that: skip the archive add when the
                    // same text and targets were archived within the 13 s a message stays live.
                    if (!RecentlyArchived(msg)) Find.Archive.Add(msg);
                }
                return false;
            }
            catch (Exception e)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    Logger.Warning("MessageFilter prefix failed, messages pass through: " + e);
                }
                return true;
            }
        }

        private struct RecentHidden
        {
            public string text;
            public LookTargets targets;
            public float time;
        }

        private static readonly List<RecentHidden> recentHidden = new List<RecentHidden>();
        private const float LiveSeconds = 13f; // Message.DefaultMessageLifespan

        // True when the same text and targets were archived as hidden within LiveSeconds;
        // otherwise records this one.
        private static bool RecentlyArchived(Message msg)
        {
            float now = Time.realtimeSinceStartup;
            recentHidden.RemoveAll(r => now - r.time > LiveSeconds);
            for (int i = 0; i < recentHidden.Count; i++)
            {
                RecentHidden r = recentHidden[i];
                if (r.text == msg.text && LookTargets.SameTargets(r.targets, msg.lookTargets))
                {
                    // Vanilla resets a live message's timer on each repeat; do the same.
                    r.time = now;
                    recentHidden[i] = r;
                    return true;
                }
            }
            recentHidden.Add(new RecentHidden { text = msg.text, targets = msg.lookTargets, time = now });
            return false;
        }

        // The calling method: the first stack frame that is not Verse.Messages, not this
        // class and not Harmony. Compiler-generated lambdas, iterators and local functions
        // are mapped back to the method that contains them. Null for our own mods.
        private static string Identify(out string assembly, out string label)
        {
            assembly = null;
            label = null;
            var trace = new StackTrace(false);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                // A Harmony-patched caller shows as a generated method with no declaring type;
                // resolve it back to the original so the key does not shift with other mods.
                MethodBase m = Harmony.GetOriginalMethodFromStackframe(trace.GetFrame(i));
                Type t = m == null ? null : m.DeclaringType;
                if (t == null || t == typeof(Messages) || t == typeof(MessageFilter)) continue;
                string asm = t.Assembly.GetName().Name;
                if (asm.StartsWith("0Harmony", StringComparison.Ordinal) || asm.StartsWith("MonoMod", StringComparison.Ordinal)) continue;
                if (OwnAssemblies.Contains(asm)) return null;

                string method = m.Name;
                if (method.StartsWith("<", StringComparison.Ordinal)) method = Between(method);
                while (t.DeclaringType != null && t.Name.StartsWith("<", StringComparison.Ordinal))
                {
                    if (m.Name == "MoveNext") method = Between(t.Name);
                    t = t.DeclaringType;
                }
                assembly = asm;
                label = t.FullName + "." + method;
                return asm + "|" + label;
            }
            return null;
        }

        private static string Between(string name)
        {
            int end = name.IndexOf('>');
            return end > 1 ? name.Substring(1, end - 1) : name;
        }

        private static MessageKindEntry Register(string key, string assembly, string label, string text)
        {
            MessageKindEntry entry;
            if (index.TryGetValue(key, out entry)) return entry;
            List<MessageKindEntry> list = InterfaceChangesMod.Settings.messageKinds;
            foreach (MessageKindEntry e in list)
            {
                if (e.key == key) { index[key] = e; return e; }
            }
            entry = new MessageKindEntry { key = key, assembly = assembly, label = label, sample = Trim(text), shown = true };
            list.Add(entry);
            index[key] = entry;
            settingsDirty = true;
            Logger.Info("New message kind found: " + label + " [" + assembly + "]. Sample: " + entry.sample);
            return entry;
        }

        private static string Trim(string s)
        {
            if (s == null) return "";
            s = s.Replace('\n', ' ');
            return s.Length > 120 ? s.Substring(0, 120) : s;
        }

        // Called from the game component: writes the settings after a new kind was found.
        public static void FlushIfDirty()
        {
            if (!settingsDirty) return;
            settingsDirty = false;
            InterfaceChangesMod.SaveSettings();
        }

        // On saving the game: how many messages each unchecked kind hid.
        public static void LogSuppressedCounts()
        {
            if (suppressedCounts.Count == 0) return;
            foreach (var pair in suppressedCounts)
            {
                Logger.Info("Message kind " + pair.Key + " hid " + pair.Value + " message(s) since the last save.");
            }
            suppressedCounts.Clear();
        }

        // Unticking a kind removes its messages already on screen. Messages.liveMessages is private.
        private static void RemoveLive(MessageKindEntry entry)
        {
            var live = liveMessagesField == null ? null : liveMessagesField.GetValue(null) as List<Message>;
            if (live == null) return;
            int removed = live.RemoveAll(m =>
            {
                string k;
                return kindOf.TryGetValue(m, out k) && k == entry.key;
            });
            Logger.Info("Message kind " + entry.label + " [" + entry.assembly + "] unchecked: removed " + removed + " message(s) from the screen.");
        }

        // Kinds whose mod is not loaded any more drop out of the list.
        private static void Prune()
        {
            if (loadedAssemblies == null)
            {
                loadedAssemblies = new HashSet<string>();
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) loadedAssemblies.Add(a.GetName().Name);
            }
            List<MessageKindEntry> list = InterfaceChangesMod.Settings.messageKinds;
            int removed = list.RemoveAll(e => e.assembly == null || !loadedAssemblies.Contains(e.assembly));
            if (removed > 0)
            {
                index.Clear();
                sortedCount = -1;
                Logger.Info("Dropped " + removed + " message kind(s) whose mod is no longer loaded.");
            }
        }

        public static void DoSettingsList(Rect rect)
        {
            Prune();
            List<MessageKindEntry> list = InterfaceChangesMod.Settings.messageKinds;
            if (sorted == null || sortedCount != list.Count)
            {
                sorted = new List<MessageKindEntry>(list);
                sorted.Sort((a, b) =>
                {
                    int c = string.Compare(a.assembly, b.assembly, StringComparison.OrdinalIgnoreCase);
                    return c != 0 ? c : string.Compare(a.label, b.label, StringComparison.Ordinal);
                });
                sortedCount = list.Count;
            }

            Text.Font = GameFont.Small;
            bool master = InterfaceChangesMod.Settings.filterMessages;
            bool masterWas = master;
            Widgets.CheckboxLabeled(new Rect(rect.x, rect.y, rect.width, 24f), "Filter messages", ref master);
            if (master != masterWas)
            {
                InterfaceChangesMod.Settings.filterMessages = master;
                Logger.Info("Message filtering turned " + (master ? "on" : "off") + ".");
            }
            rect = new Rect(rect.x, rect.y + 28f, rect.width, Mathf.Max(0f, rect.height - 28f));
            var prevColor = GUI.color;
            if (!master) GUI.color = new Color(1f, 1f, 1f, 0.5f);
            DoKindList(rect, master);
            GUI.color = prevColor;
        }

        private static void DoKindList(Rect rect, bool enabled)
        {
            List<MessageKindEntry> list = InterfaceChangesMod.Settings.messageKinds;
            var head = new Rect(rect.x, rect.y, rect.width, 24f);
            Widgets.Label(head, "On-screen messages: tick to show, untick to hide. Our own mods are not listed.");
            var outRect = new Rect(rect.x, rect.y + 28f, rect.width, Mathf.Max(0f, rect.height - 28f));
            if (sorted.Count == 0)
            {
                Widgets.Label(outRect, "None found yet. Kinds appear here as messages show in a game.");
                return;
            }
            const float rowH = 24f;
            var view = new Rect(0f, 0f, outRect.width - 16f, sorted.Count * rowH);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            for (int i = 0; i < sorted.Count; i++)
            {
                MessageKindEntry e = sorted[i];
                var row = new Rect(0f, i * rowH, view.width, rowH);
                bool was = e.shown;
                bool now = was;
                string text = e.label + " [" + e.assembly + "]  " + e.sample;
                Widgets.CheckboxLabeled(row, text.Truncate(view.width - 40f), ref now, !enabled);
                TooltipHandler.TipRegion(row, e.label + " [" + e.assembly + "]\n" + e.sample);
                if (now != was)
                {
                    e.shown = now;
                    if (!now) RemoveLive(e);
                    else Logger.Info("Message kind " + e.label + " [" + e.assembly + "] checked: showing its messages.");
                }
            }
            Widgets.EndScrollView();
        }
    }

    // Flushes settings after a new kind was found, and reports hidden counts when the game is saved.
    public class MessageFilterComponent : GameComponent
    {
        private int ticks;

        public MessageFilterComponent(Game game) { }

        public override void GameComponentTick()
        {
            ticks++;
            if (ticks % 600 == 0) MessageFilter.FlushIfDirty();
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) MessageFilter.LogSuppressedCounts();
        }
    }
}
