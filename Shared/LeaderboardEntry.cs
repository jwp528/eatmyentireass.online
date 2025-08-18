using System;
using System.Collections.Generic;

namespace BlazorApp.Shared
{
    public class LeaderboardEntry
    {
        public string PlayerName { get; set; } = string.Empty;
        public double Score { get; set; }
        public int TotalClicks { get; set; }
        public DateTime GameDate { get; set; }
        public Dictionary<string, int> AssTypeBreakdown { get; set; } = new();
        public int GameDurationSeconds { get; set; } = 60;
        public double ClickEfficiency => TotalClicks > 0 ? Math.Round(Score / TotalClicks, 3) : 0;
        public double CPS => GameDurationSeconds > 0 ? Math.Round((double)TotalClicks / GameDurationSeconds, 2) : 0;
    }

    public class Leaderboard
    {
        public List<LeaderboardEntry> Entries { get; set; } = new();
    }
}