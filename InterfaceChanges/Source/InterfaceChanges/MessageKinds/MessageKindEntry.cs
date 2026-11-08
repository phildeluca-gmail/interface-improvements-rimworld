using Verse;

namespace InterfaceChanges.MessageKinds
{
    // One kind of on-screen message: one call site, ii-architecture.md section 17.
    public class MessageKindEntry : IExposable
    {
        public string key;      // assembly|Type.Method
        public string assembly; // sending mod's assembly name
        public string label;    // Type.Method
        public string sample;   // text of the first message seen
        public bool shown = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref assembly, "assembly");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref sample, "sample");
            Scribe_Values.Look(ref shown, "shown", true);
        }
    }
}
