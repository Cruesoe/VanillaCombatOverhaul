using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Rules of Engagement has its own walk-firing (for NPCs too) and loadouts, so run and gun and loadouts stay off while
    /// it is active. Its loadouts only fetch named weapons, so choosing the primary weapon stays available.
    /// </summary>
    public static class RulesOfEngagementCompat
    {
        public const string PackageId = "asunib.RulesOfEngagement";

        public static readonly bool Active = ModsConfig.IsActive(PackageId);

        private static bool resolved;
        private static Func<Pawn, bool> handlesWeapons;

        /// <summary>
        /// True while Rules of Engagement is managing this pawn's weapons (a named weapon, off-hand, melee switch and so on),
        /// using the check it applies to Auto Arm. Also true if that check can't be found, so the two never compete.
        /// </summary>
        public static bool HandlesWeapons(Pawn pawn)
        {
            if (!Active)
            {
                return false;
            }
            if (!resolved)
            {
                resolved = true;
                var type = FindType("AutoArmCompat");
                var method = type == null ? null : AccessTools.Method(type, "RoeHandlesWeapons", new[] { typeof(Pawn) });
                if (method != null && method.ReturnType == typeof(bool))
                {
                    handlesWeapons = AccessTools.MethodDelegate<Func<Pawn, bool>>(method);
                }
                else
                {
                    Log.Warning("[VCO] Rules of Engagement's weapon check was not found; colonists won't choose their primary weapon.");
                }
            }
            return handlesWeapons == null || handlesWeapons(pawn);
        }

        /// <summary>A type in Rules of Engagement's assemblies, found by class name.</summary>
        private static Type FindType(string name)
        {
            var mod = LoadedModManager.RunningModsListForReading.Find(m => string.Equals(m.PackageId, PackageId, StringComparison.OrdinalIgnoreCase));
            if (mod == null)
            {
                return null;
            }
            foreach (var assembly in mod.assemblies.loadedAssemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }
                foreach (var type in types)
                {
                    if (type != null && type.Name == name)
                    {
                        return type;
                    }
                }
            }
            return null;
        }
    }
}
