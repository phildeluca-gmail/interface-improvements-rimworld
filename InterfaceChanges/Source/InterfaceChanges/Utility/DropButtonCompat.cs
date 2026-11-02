using Verse;

namespace InterfaceChanges.Utility
{
    // Do Not Be Lazy adds its own Drop everything button; with both mods
    // active the player would see two. Checked once, then cached.
    // ModsConfig.IsActive(packageId) verified by reflection (static, one string).
    public static class DropButtonCompat
    {
        private const string DnblPackageId = "phildeluca.donotbelazy";
        private static bool? suppress;

        // Called once at startup so the log line appears there.
        public static void Evaluate()
        {
            bool ignored = SuppressDropButton;
        }

        public static bool SuppressDropButton
        {
            get
            {
                if (suppress == null)
                {
                    suppress = ModsConfig.IsActive(DnblPackageId);
                    Logger.Info(suppress.Value
                        ? "Drop everything button suppressed: Do Not Be Lazy (" + DnblPackageId + ") is active and adds its own."
                        : "Drop everything button shown: Do Not Be Lazy (" + DnblPackageId + ") is not active.");
                }
                return suppress.Value;
            }
        }
    }
}
