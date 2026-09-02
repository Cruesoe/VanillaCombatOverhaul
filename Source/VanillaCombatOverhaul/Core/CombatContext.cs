using System;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// Several vanilla combat types we need to extend are structs (ShotReport), so we cannot
    /// attach fields to them and cannot use a ConditionalWeakTable. The information still has
    /// to travel from the method that has it to the method that needs it.
    ///
    /// Rather than parking it in static fields that outlive the call and go stale -- which is
    /// how a tooltip ends up showing another pawn's numbers -- context is pushed for an
    /// explicit scope and popped on dispose. Reads outside a scope return false rather than
    /// a leftover value, so a missing scope surfaces as "no data" instead of wrong data.
    /// </summary>
    public readonly struct ShotContext
    {
        public readonly Thing Caster;
        public readonly Verb Verb;

        public ShotContext(Thing caster, Verb verb)
        {
            Caster = caster;
            Verb = verb;
        }

        public bool IsValid => Caster != null;
    }

    public static class CombatContext
    {
        // RimWorld's simulation and UI are single-threaded, but shot reports do nest
        // (a tooltip can be built while AI evaluates targets), so this is a stack.
        private const int MaxDepth = 8;
        private static readonly ShotContext[] Stack = new ShotContext[MaxDepth];
        private static int depth;

        public static bool TryGetShot(out ShotContext context)
        {
            if (depth > 0)
            {
                context = Stack[depth - 1];
                return context.IsValid;
            }
            context = default;
            return false;
        }

        /// <summary>Pushes shot context for the lifetime of the returned scope.</summary>
        public static Scope PushShot(Thing caster, Verb verb)
        {
            if (depth >= MaxDepth)
            {
                // Refuse to grow without bound; a runaway push is a bug worth seeing.
                Log.ErrorOnce(
                    "[VCO] Shot context stack overflow; context will be unavailable for this call.",
                    0x5C09E01);
                return new Scope(false);
            }
            Stack[depth++] = new ShotContext(caster, verb);
            return new Scope(true);
        }

        /// <summary>Clears all context. Called on map load and game start.</summary>
        public static void Reset() => depth = 0;

        public readonly struct Scope : IDisposable
        {
            private readonly bool pushed;
            internal Scope(bool pushed) => this.pushed = pushed;

            public void Dispose()
            {
                if (pushed && depth > 0)
                {
                    Stack[--depth] = default;
                }
            }
        }
    }
}
