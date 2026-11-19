using Verse;

namespace InterfaceChanges.Roofs
{
    // ii-architecture.md section 33. Empty on purpose. This was the bottom-left
    // "Roof support" button, replaced by the play-settings icon (RoofSupportToggle).
    // The class stays because existing saves list it under <components>; deleting it
    // makes RimWorld log a missing-class error when such a save loads.
    public class RoofOverlay : GameComponent
    {
        public RoofOverlay(Game game) { }
    }
}
