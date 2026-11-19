using InterfaceChanges.Core;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.Roofs
{
    // ii-architecture.md sections 27 and 33. Flips the saved setting showRoofSupport;
    // SupportedRoofOverlay draws the outline while it is on. Called by the play-settings icon.
    public static class RoofSupportToggle
    {
        public static void SetShown(bool on)
        {
            InterfaceChangesMod.Settings.showRoofSupport = on;
            InterfaceChangesMod.SaveSettings();
            Logger.Info("Roof support overlay switched " + (on ? "on." : "off."));
        }
    }
}
