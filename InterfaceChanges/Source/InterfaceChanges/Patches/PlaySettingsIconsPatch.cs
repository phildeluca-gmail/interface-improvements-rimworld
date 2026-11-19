using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using InterfaceChanges.Core;
using InterfaceChanges.Roofs;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Patches
{
    // Postfix on RimWorld.PlaySettings.DoPlaySettingsGlobalControls(WidgetRow row, bool worldView),
    // the method that draws the bottom-right toggle row (verified by reflection on
    // lib\Assembly-CSharp.dll). Adds the Roof support and Show table radius icons on the
    // map view only. WidgetRow.ToggleableIcon(ref bool, Texture2D, string tooltip, SoundDef, string) draws them.
    // Applied by hand, not by attribute, so a missing target is logged instead of throwing out of PatchAll.
    // Both icons are our own textures: vanilla's row already shows TexButton.ShowRoofOverlay.
    // ii-architecture.md section 33.
    public static class PlaySettingsIconsPatch
    {
        private static Texture2D roofIcon;
        private static Texture2D tableIcon;

        public static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.DeclaredMethod(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls));
            if (target == null)
            {
                Logger.Warning("PlaySettingsIconsPatch: PlaySettings.DoPlaySettingsGlobalControls not found, the Roof support and Show table radius icons will not be drawn.");
                return;
            }
            MethodInfo postfix = AccessTools.Method(typeof(PlaySettingsIconsPatch), nameof(Postfix));
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            Logger.Info("PlaySettingsIconsPatch applied to PlaySettings.DoPlaySettingsGlobalControls.");
        }

        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView || row == null) return;
            var settings = InterfaceChangesMod.Settings;
            if (settings == null) return;

            // ContentFinder looks under each mod's Textures\ folder; false = no error if missing.
            if (roofIcon == null) roofIcon = ContentFinder<Texture2D>.Get("UI/IIRoofSupport", false) ?? BaseContent.BadTex;
            if (tableIcon == null) tableIcon = ContentFinder<Texture2D>.Get("UI/IITableRadius", false) ?? BaseContent.BadTex;

            bool roof = settings.showRoofSupport;
            row.ToggleableIcon(ref roof, roofIcon,
                "Roof support: " + (settings.showRoofSupport ? "on" : "off") + ". Outline the roof area supported by selected walls and columns.", SoundDefOf.Mouseover_ButtonToggle);
            if (roof != settings.showRoofSupport) RoofSupportToggle.SetShown(roof);

            bool table = settings.showTableRadius;
            row.ToggleableIcon(ref table, tableIcon,
                "Show table radius: " + (settings.showTableRadius ? "on" : "off") + ". Outline the radius pawns search for a table, around each table.", SoundDefOf.Mouseover_ButtonToggle);
            if (table != settings.showTableRadius) TableRadiusOverlay.SetShown(table);
        }
    }
}
