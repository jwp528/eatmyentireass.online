using BlazorApp.Client.Services;
using BlazorApp.Shared;
using Microsoft.AspNetCore.Components;

namespace BlazorApp.Client.Components
{
    public partial class LeaderboardStatsModal : ComponentBase
    {
        [Inject] public ILeaderboardService LeaderboardService { get; set; } = default!;
        [Inject] public IStatsService StatsService { get; set; } = default!;

        public BSModal? Modal;

        private string activeTab = "alltime";
        private bool isLoadingLeaderboard;
        private bool isLoadingStats;
        private GameStats? stats;
        private string _searchText = string.Empty;

        // Per-period leaderboard data
        private readonly Dictionary<string, List<LeaderboardEntry>> _periodScores = new();
        private readonly HashSet<string> _loadedPeriods = new();

        private static readonly string[] LeaderboardPeriods = ["alltime", "daily", "monthly", "yearly"];

        private List<LeaderboardEntry> CurrentScores =>
            _periodScores.TryGetValue(activeTab, out var list) ? list : new();

        private List<LeaderboardEntry> CurrentScoresFiltered =>
            string.IsNullOrWhiteSpace(_searchText)
                ? CurrentScores
                : CurrentScores.Where(e => e.PlayerName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        public async Task ShowLeaderboard()
        {
            activeTab = "alltime";
            await Modal!.Show();
            _ = LoadPeriodAsync("alltime");
        }

        public async Task ShowStats()
        {
            activeTab = "stats";
            await Modal!.Show();
            _ = LoadStatsAsync();
        }

        private async Task SwitchTab(string tab)
        {
            activeTab = tab;
            _searchText = string.Empty;

            if (tab == "stats")
            {
                await LoadStatsAsync();
            }
            else if (!_loadedPeriods.Contains(tab))
            {
                await LoadPeriodAsync(tab);
            }
        }

        private async Task Refresh()
        {
            if (activeTab == "stats")
            {
                await LoadStatsAsync();
            }
            else
            {
                _loadedPeriods.Remove(activeTab);
                await LoadPeriodAsync(activeTab);
            }
        }

        private async Task LoadPeriodAsync(string period)
        {
            isLoadingLeaderboard = true;
            StateHasChanged();
            try
            {
                var entries = await LeaderboardService.GetTopScoresAsync(period, count: 100);
                _periodScores[period] = entries;
                _loadedPeriods.Add(period);
            }
            catch
            {
                _periodScores[period] = new();
            }
            isLoadingLeaderboard = false;
            StateHasChanged();
        }

        private async Task LoadStatsAsync()
        {
            isLoadingStats = true;
            StateHasChanged();
            try
            {
                stats = await StatsService.GetStatsAsync();
            }
            catch
            {
                stats = null;
            }
            isLoadingStats = false;
            StateHasChanged();
        }

        private async Task CloseModal()
        {
            if (Modal != null) await Modal.Hide();
        }

        private string FormatTime(long seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalDays >= 1)
                return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }

        private long GetAssTypeCount(string assType)
            => stats?.AssTypeStats.TryGetValue(assType, out var v) == true ? v : 0;

        private long TotalAssesEaten()
            => stats?.AssTypeStats.Values.Sum() ?? 0;
    }
}
