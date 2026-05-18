using System;
using System.Collections.Generic;

namespace BlazorApp.Shared
{
    public class GameStats
    {
        public long TotalClicks { get; set; }
        public long RoundsPlayed { get; set; }
        public long TotalTimePlayedSeconds { get; set; }
        public Dictionary<string, long> AssTypeStats { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class GameStatsUpdate
    {
        public int Clicks { get; set; }
        public int DurationSeconds { get; set; }
        public Dictionary<string, int> AssTypeBreakdown { get; set; } = new();
    }
}
