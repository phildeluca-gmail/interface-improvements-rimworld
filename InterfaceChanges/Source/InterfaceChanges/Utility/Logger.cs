using RimWorld;
using Verse;

namespace InterfaceChanges.Utility
{
    // Named Logger, not Log - Verse.Log is in scope in every file here.
    // Every line is per event (a startup summary, a key press, a button
    // press, an order) and never per frame, tick or candidate.
    public static class Logger
    {
        private const string Prefix = "[InterfaceChanges] ";

        public static void Info(string text)
        {
            Log.Message(Prefix + text);
        }

        public static void Warning(string text)
        {
            Log.Warning(Prefix + text);
        }

        // Shown to the player and written to the log too - a message on
        // screen is not a log line (CLAUDE.md section 4).
        public static void Tell(string text, LookTargets target)
        {
            Messages.Message(text, target, MessageTypeDefOf.RejectInput, false);
            Log.Message(Prefix + text);
        }

        // Neutral on-screen message that looks at a thing, and the log line.
        public static void Report(string text, Verse.Thing target)
        {
            Messages.Message(text, new LookTargets(target), MessageTypeDefOf.NeutralEvent, false);
            Log.Message(Prefix + text);
        }
    }
}
