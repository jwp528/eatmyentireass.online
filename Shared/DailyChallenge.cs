using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorApp.Shared
{
    public enum DailyChallengeType
    {
        EatXAsses,       // eat X total asses in one game
        EatXTypeAsses,   // eat X of [Type] in one game
        CpsOver10,       // average CPS >= 10
        TriggerFrenzy,   // trigger frenzy mode >= X times
        EatAllTypes,     // eat all 6 types in one game
    }

    public class DailyTask
    {
        public DailyChallengeType Type { get; set; }
        public int TargetValue { get; set; }
        public string? AssTypeName { get; set; }
        public bool Completed { get; set; }

        public string GetDescription() => Type switch
        {
            DailyChallengeType.EatXAsses => $"Eat {TargetValue} asses in one game",
            DailyChallengeType.EatXTypeAsses => $"Eat {TargetValue} {AssTypeName} asses in one game",
            DailyChallengeType.CpsOver10 => "Average 10+ clicks per second in one game",
            DailyChallengeType.TriggerFrenzy => $"Trigger Frenzy {TargetValue} time{(TargetValue > 1 ? "s" : "")} in one game",
            DailyChallengeType.EatAllTypes => "Eat at least one of every ass type",
            _ => "Complete this challenge"
        };

        public string GetIcon() => Type switch
        {
            DailyChallengeType.EatXAsses => "🍑",
            DailyChallengeType.EatXTypeAsses => "🎯",
            DailyChallengeType.CpsOver10 => "⚡",
            DailyChallengeType.TriggerFrenzy => "🔥",
            DailyChallengeType.EatAllTypes => "🌈",
            _ => "✅"
        };
    }

    public class DailyChallengeProgress
    {
        public string Date { get; set; } = "";
        public List<DailyTask> Tasks { get; set; } = new();
        public int CompletedCount => Tasks.Count(t => t.Completed);
    }

    public static class DailyChallenge
    {
        public static string GetChallengeDateKey(DateOnly date) =>
            $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}";

        public static List<DailyTask> GenerateDailyTasks(DateOnly date)
        {
            int seed = date.Year * 10000 + date.Month * 100 + date.Day;
            var rng = new Random(seed);

            var allTypes = (Assets.AssTypeEnum[])Enum.GetValues(typeof(Assets.AssTypeEnum));
            var frenzyTargets = new[] { 1, 2, 3 };

            // One task from each category pool, shuffled
            var pool = new List<DailyTask>
            {
                // Eat X asses: 50-500 steps of 50
                new() { Type = DailyChallengeType.EatXAsses, TargetValue = (rng.Next(1, 11)) * 50 },
                // Eat X of a type: 1-25
                new()
                {
                    Type = DailyChallengeType.EatXTypeAsses,
                    TargetValue = rng.Next(1, 26),
                    AssTypeName = allTypes[rng.Next(allTypes.Length)].ToString()
                },
                // CPS > 10
                new() { Type = DailyChallengeType.CpsOver10 },
                // Trigger frenzy N times
                new() { Type = DailyChallengeType.TriggerFrenzy, TargetValue = frenzyTargets[rng.Next(frenzyTargets.Length)] },
                // Eat all types
                new() { Type = DailyChallengeType.EatAllTypes },
            };

            // Fisher-Yates shuffle
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            return pool.Take(3).ToList();
        }
    }
}
