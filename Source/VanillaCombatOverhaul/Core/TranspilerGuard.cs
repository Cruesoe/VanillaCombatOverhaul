namespace VanillaCombatOverhaul
{
    /// <summary>Expected splice count for a transpiler; checked after patching so a transpiler that finds no target logs an error.</summary>
    public sealed class TranspilerGuard
    {
        public readonly string Id;
        public readonly int Expected;
        public int Actual { get; private set; }

        internal TranspilerGuard(string id, int expected)
        {
            Id = id;
            Expected = expected;
        }

        /// <summary>Call once per successful splice inside the transpiler body.</summary>
        public void Spliced() => Actual++;

        public bool Satisfied => Actual == Expected;
    }
}
