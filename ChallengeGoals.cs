using System;
using System.Collections.Generic;
// challenge data
public static class ChallengeGoals
{
    public sealed class HeightGoals
    {
        public readonly float Bronze;
        public readonly float Silver;
        public readonly float Gold;
        public readonly float Platinum;

        public HeightGoals(float bronze, float silver, float gold, float platinum)
        {
            Bronze = bronze;
            Silver = silver;
            Gold = gold;
            Platinum = platinum;
        }
    }

    public static readonly Dictionary<string, HeightGoals> ByName =
        new Dictionary<string, HeightGoals>(StringComparer.OrdinalIgnoreCase)
        {
            { "Shortcuts", new HeightGoals(5000f, 10000f, 15000f, 20000f) }
        };
}
