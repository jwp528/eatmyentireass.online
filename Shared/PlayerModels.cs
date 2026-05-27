using System;
using System.Collections.Generic;

namespace BlazorApp.Shared
{
    public static class PlayerNameHelper
    {
        public static string Normalize(string name) =>
            name.Trim().ToLowerInvariant();
    }

    public class PlayerStatusResponse
    {
        public bool IsClaimed { get; set; }
    }

    public class PlayerClaimRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class PlayerAuthResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    public class SaveScoreRequest
    {
        public LeaderboardEntry Entry { get; set; } = new();
        public string? AuthToken { get; set; }
    }

    public class PlayerStats
    {
        public string PlayerName { get; set; } = string.Empty;
        public int GamesPlayed { get; set; }
        public long TotalClicks { get; set; }
        public long TotalTimeSeconds { get; set; }
        public double BestScore { get; set; }
        public DateTime LastPlayed { get; set; }
        public Dictionary<string, int> AssTypeBreakdown { get; set; } = new();
    }

    /// <summary>Per-type progress stored in Azure Tables (and used client-side for perk logic).</summary>
    public class AssTypeProgress
    {
        public int Eaten { get; set; }
        public long ClicksUsed { get; set; }

        public bool HasPerk10 => Eaten >= 10;
        public bool HasPerk25 => Eaten >= 25;
        public bool HasPerk50 => Eaten >= 50;
        public bool HasPerk100 => Eaten >= 100;

        public int NextMilestone => Eaten switch
        {
            < 10 => 10,
            < 25 => 25,
            < 50 => 50,
            < 100 => 100,
            _ => 0
        };
    }

    /// <summary>Request body for saving player progress to Azure Tables.</summary>
    public class PlayerProgressRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? AuthToken { get; set; }
        /// <summary>Keys are AssTypeEnum names; values are progress. Server merges with MAX semantics.</summary>
        public Dictionary<string, AssTypeProgress> Progress { get; set; } = new();
    }

    /// <summary>Request body for saving Assdex collection to Azure Tables.</summary>
    public class PlayerCollectionRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? AuthToken { get; set; }
        /// <summary>AssTypeEnum names that are unlocked. Server unions with existing set.</summary>
        public List<string> UnlockedTypes { get; set; } = new();
    }
}
