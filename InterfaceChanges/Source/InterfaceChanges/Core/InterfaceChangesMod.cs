using HarmonyLib;
using InterfaceChanges.Patches;
using Logger = InterfaceChanges.Utility.Logger;
using UnityEngine;
using Verse;

namespace InterfaceChanges.Core
{
    // Entry point. RimWorld builds one of these per load and hands it the
    // ModContentPack. See ic-architecture.md.
    public class InterfaceChangesMod : Mod
    {
        public static InterfaceChangesSettings Settings;

        public InterfaceChangesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<InterfaceChangesSettings>();
            Harmony harmony = new Harmony("phildeluca.interfacechanges");
            harmony.PatchAll();
            SelectionNamesPatch.Apply(harmony);
            TabLabelPatch.Apply(harmony);
            ColonistBarPatches.Apply(harmony);
            Utility.DropButtonCompat.Evaluate();
            Logger.Info("Loaded.");
        }

        // Writes ModSettings to disk now; called when a standard bill set
        // is saved, renamed or deleted so it survives a crash.
        public static void SaveSettings()
        {
            LoadedModManager.GetMod<InterfaceChangesMod>().WriteSettings();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        // Must match About.xml's <name> exactly - the Mod options list is
        // drawn from this string, not from About.xml.
        public override string SettingsCategory()
        {
            return "Ketjak's Interface Improvements";
        }
    }
}
