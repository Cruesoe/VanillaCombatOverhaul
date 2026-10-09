using System.Collections.Generic;
using System.Xml;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Runs <c>match</c> when the named settings flag is on, otherwise <c>nomatch</c>. Adapted from
    /// Vanilla Combat Reloaded by Donald (DonaldKar). Flags are snapshotted on first use during def load.
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

            // A missing branch succeeds, so a feature switched off is not reported as a failed patch.
            return ActiveFlags.Contains(setting)
                ? match == null || match.Apply(xml)
                : nomatch == null || nomatch.Apply(xml);
        }

        public override string ToString() => $"{base.ToString()}({setting})";
    }
}
