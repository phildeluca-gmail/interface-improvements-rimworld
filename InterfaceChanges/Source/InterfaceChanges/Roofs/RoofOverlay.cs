using RimWorld.Planet;
using UnityEngine;
using Verse;
using InterfaceChanges.Core;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Roofs
{
    // ii-architecture.md sections 18 and 27. The bottom-left "Roof support" toggle. It
    // only flips the saved setting showRoofSupport; SupportedRoofOverlay draws the
    // outline while it is on and roof-holding buildings are selected. No patches.
    public class RoofOverlay : GameComponent
    {
        public RoofOverlay(Game game) { }

        public override void GameComponentOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || WorldRendererUtility.WorldRenderedNow) return;
            var settings = InterfaceChangesMod.Settings;
            bool on = settings.showRoofSupport;
            var rect = new Rect(10f, UI.screenHeight - 35f - 8f - 30f, 190f, 30f);
            if (Widgets.ButtonText(rect, on ? "Roof support: on" : "Roof support: off"))
            {
                settings.showRoofSupport = !on;
                InterfaceChangesMod.SaveSettings();
                Logger.Info("Roof support overlay switched " + (on ? "off." : "on."));
            }
        }
    }
}
