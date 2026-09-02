using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VanillaCombatOverhaul
{
    [HarmonyPatch(typeof(ShootLine), nameof(ShootLine.ChangeDestToMissWild))]
    public static class Patch_ShootLine_ChangeDestToMissWild
    {
        private static readonly TranspilerGuard Guard =
            PatchGuard.Declare("ShootLine.ChangeDestToMissWild.firingArc", 1);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var evaluate = AccessTools.Method(typeof(SimpleSurface), nameof(SimpleSurface.Evaluate));
            var adjust = AccessTools.Method(typeof(Patch_ShootLine_ChangeDestToMissWild), nameof(AdjustMissRadius));

            var destField = AccessTools.Field(typeof(ShootLine), "dest");
            var sourceField = AccessTools.Field(typeof(ShootLine), "source");

            foreach (var instruction in instructions)
            {
                yield return instruction;

                if (instruction.operand as MethodBase != evaluate)
                {
                    continue;
                }

                Guard.Spliced();
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, destField);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, sourceField);
                yield return new CodeInstruction(OpCodes.Call, adjust);
            }
        }

        public static float AdjustMissRadius(float vanillaRadius, IntVec3 dest, IntVec3 source)
        {
            var settings = VCOMod.Settings;
            if (settings == null || !settings.enableFiringArc)
            {
                return vanillaRadius;
            }

            return FiringArcUtility.AdjustMissRadius(
                vanillaRadius, dest, source, settings.firingArcDegrees);
        }
    }
}
