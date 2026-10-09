using System;
using Verse;

namespace VanillaCombatOverhaul
{
    /// <summary>
    /// The caster and verb of the shot being worked out, for code that only receives a ShotReport
    /// or stat request. Pushed for a scope and popped on dispose; reads outside a scope return false.
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
        // A stack, because shot reports nest (a tooltip built while AI evaluates targets).
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
                // Bounded: a runaway push logs once and gets no context.
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
