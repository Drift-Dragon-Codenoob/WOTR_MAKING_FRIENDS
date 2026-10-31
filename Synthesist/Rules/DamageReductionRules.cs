namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal static class DamageReductionRules
    {
        internal static bool OpposesAlignment(string bypass, bool good, bool evil, bool lawful, bool chaotic)
            => bypass switch
            {
                "Good" => evil,
                "Evil" => good,
                "Lawful" => chaotic,
                "Chaotic" => lawful,
                _ => false
            };
    }
}
