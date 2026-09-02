namespace VanillaCombatOverhaul
{
    /// <summary>
    /// One named claim a test run makes, shared by every suite -- melee, ranged, armor,
    /// wound and height. A harness that only prints numbers cannot regress, so each of
    /// these is something that can fail rather than something that merely gets reported.
    /// </summary>
    public class AssertionResult
    {
        public string Name;
        public bool Passed;
        public string Detail;

        public override string ToString() => (Passed ? "PASS  " : "FAIL  ") + Name + "  " + Detail;
    }
}
