using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using InterfaceChanges.SaveExit;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Patches
{
    // Prefix on Verse.OptionListingUtility.DrawOptionListing(Rect, List<ListableOption>), which draws
    // the Esc menu list (MainMenuDrawer.DoMainMenuControls builds the list as a local, so it cannot be
    // patched there). Inserts "Save and exit" after the vanilla "Save" option. The Esc menu is
    // recognised by holding both the "Save" and "Quit to OS" options while a game is running.
    // Applied by hand so a missing target is logged. ii-architecture.md section 35.
    public static class SaveAndExitButtonPatch
    {
        private const string ButtonLabel = "Save and exit";

        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.DeclaredMethod(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing));
            if (target == null)
            {
                Logger.Warning("SaveAndExitButtonPatch: OptionListingUtility.DrawOptionListing not found, the Save and exit button will not be added.");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(SaveAndExitButtonPatch), nameof(Prefix))));
            Logger.Info("SaveAndExitButtonPatch applied to OptionListingUtility.DrawOptionListing.");
        }

        public static void Prefix(List<ListableOption> optList)
        {
            if (optList == null || Current.ProgramState != ProgramState.Playing) return;
            string saveLabel = "Save".Translate().ToString();
            string quitLabel = "QuitToOS".Translate().ToString();
            int saveIndex = -1;
            bool hasQuit = false;
            for (int i = 0; i < optList.Count; i++)
            {
                string label = optList[i].label;
                if (label == ButtonLabel) return;
                if (label == saveLabel) saveIndex = i;
                else if (label == quitLabel) hasQuit = true;
            }
            if (saveIndex < 0 || !hasQuit) return;

            optList.Insert(saveIndex + 1, new ListableOption(ButtonLabel, delegate
            {
                Logger.Info("Save and exit: button pressed.");
                // Same hide-the-menu step as vanilla's Save option.
                Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
                Find.WindowStack.Add(new Dialog_SaveAndExit());
            }));
        }
    }
}
