using System.Collections.Generic;
using System.Xml;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// A PatchOperation that only applies when a named feature flag is active in mod settings,
    /// letting XML patches be toggled from the settings menu like C# features are.
    ///
    /// Adapted from the same idea in Vanilla Combat Reloaded by Donald (DonaldKar), which is
    /// the neatest solution to settings-gated XML patching in the RimWorld ecosystem.
    ///
    /// Def patching runs before [StaticConstructorOnStartup], so the flag snapshot is taken
    /// lazily on first use -- Mod instances are constructed before def loading, so settings
    /// are already available by then.
    /// </summary>
    public class PatchOperationSettingGated : PatchOperation
    {
        // Populated by RimWorld's XML deserializer via reflection, never assigned in code.
#pragma warning disable CS0649
        private string setting;
        private PatchOperation match;
        private PatchOperation nomatch;
#pragma warning restore CS0649

        private static HashSet<string> activeFlags;

        private static HashSet<string> ActiveFlags
        {
            get
            {
                if (activeFlags == null)
                {
                    var settings = VCOMod.Settings;
                    activeFlags = settings == null
                        ? new HashSet<string>()
                        : new HashSet<string>(settings.ActiveXmlFlags());
                }
                return activeFlags;
            }
        }

        /// <summary>Drops the snapshot so a settings change is picked up on next restart's load.</summary>
        public static void InvalidateSnapshot() => activeFlags = null;

        protected override bool ApplyWorker(XmlDocument xml)
        {
            if (setting.NullOrEmpty())
            {
                Log.Error("[VCO] PatchOperationSettingGated used with no <setting> name.");
                return false;
            }

            // A branch with nothing to run is a no-op, and a no-op succeeded. Returning false
            // here instead makes RimWorld report a failed patch operation every time a feature
            // is simply switched off, which is the overwhelmingly common case for this mod.
            return ActiveFlags.Contains(setting)
                ? match == null || match.Apply(xml)
                : nomatch == null || nomatch.Apply(xml);
        }

        public override string ToString() => $"{base.ToString()}({setting})";
    }
}
