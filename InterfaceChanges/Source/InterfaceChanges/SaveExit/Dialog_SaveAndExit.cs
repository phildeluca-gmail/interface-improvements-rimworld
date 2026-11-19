using System;
using System.IO;
using RimWorld;
using Verse;
using Logger = InterfaceChanges.Utility.Logger;

namespace InterfaceChanges.SaveExit
{
    // Vanilla's own Save dialog (file name box included); after the save has finished the game
    // exits to the OS. Cancel (closing without saving) exits nothing. A save that leaves the file
    // missing or unchanged does not exit. ii-architecture.md section 35.
    public class Dialog_SaveAndExit : Dialog_SaveFileList_Save
    {
        private bool saveChosen;

        public Dialog_SaveAndExit()
        {
            Logger.Info("Save and exit: dialog opened, suggested file name \"" + typingName + "\".");
        }

        // Called by the base class for both the typed name and a row's Overwrite button.
        // base.DoFileInteraction only QUEUES the save as a synchronous long event. The check and
        // the exit are queued as a second long event, which LongEventHandler runs strictly after
        // the first. Calling ExecuteWhenFinished here instead would run Root.Shutdown at once,
        // because no long event is current yet (LongEventHandler.ExecuteWhenFinished runs its
        // action immediately when currentEvent is null).
        protected override void DoFileInteraction(string mapName)
        {
            saveChosen = true;
            string name = GenFile.SanitizedFileName(mapName);
            string path = GenFilePaths.FilePathForSavedGame(name);
            bool existedBefore = File.Exists(path);
            DateTime writtenBefore = existedBefore ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            Logger.Info("Save and exit: saving as \"" + name + "\"" + (existedBefore ? ", overwriting the existing file." : ", a new file."));
            base.DoFileInteraction(mapName);
            LongEventHandler.QueueLongEvent(delegate
            {
                bool exists = File.Exists(path);
                bool written = exists && (!existedBefore || File.GetLastWriteTimeUtc(path) != writtenBefore);
                if (!written)
                {
                    string why = exists ? "the file was not changed" : "the file is missing";
                    Logger.Warning("Save and exit: save of \"" + name + "\" failed, " + why + ". Exit cancelled.");
                    Messages.Message("Save and exit: the save failed (" + why + "). The game was not closed.", MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
                Logger.Info("Save and exit: save of \"" + name + "\" written. Exiting to the OS.");
                // Same call vanilla's permadeath "Save and quit to OS" makes from inside its long event.
                LongEventHandler.ExecuteWhenFinished(Root.Shutdown);
            }, "SavingLongEvent", doAsynchronously: false, null, showExtraUIInfo: false);
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!saveChosen) Logger.Info("Save and exit: cancelled, nothing saved, game not exited.");
        }
    }
}
