namespace VanillaCombatOverhaul
{
    /// <summary>One named pass/fail check in a test report.</summary>
    public class AssertionResult
    {
        public string Name;
        public bool Passed;
        public string Detail;

        public override string ToString() => (Passed ? "PASS  " : "FAIL  ") + Name + "  " + Detail;
    }
}
