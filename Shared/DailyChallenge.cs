using System;
using System.Collections.Generic;

namespace BlazorApp.Shared
{
    public static class DailyChallenge
    {
        private const int SequenceLength = 200;

        /// <summary>
        /// Returns a seeded, deterministic sequence of ass types for the given date.
        /// All players on the same date get the same sequence.
        /// </summary>
        public static List<Assets.AssTypeEnum> GetDailySequence(DateOnly date)
        {
            // Seed from date integer so it's stable across clients
            int seed = date.Year * 10000 + date.Month * 100 + date.Day;
            var rng = new Random(seed);

            var assTypes = (Assets.AssTypeEnum[])Enum.GetValues(typeof(Assets.AssTypeEnum));
            var sequence = new List<Assets.AssTypeEnum>(SequenceLength);

            for (int i = 0; i < SequenceLength; i++)
            {
                sequence.Add(assTypes[rng.Next(assTypes.Length)]);
            }

            return sequence;
        }

        public static string GetChallengeDateKey(DateOnly date) =>
            $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}";
    }
}
