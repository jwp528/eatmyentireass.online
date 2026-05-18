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

        private string activeTab = "leaderboard";
        private List<LeaderboardEntry> topScores = new();
        private GameStats? stats;
        private bool isLoadingLeaderboard;
        private bool isLoadingStats;

        public async Task ShowLeaderboard()
        {
            activeTab = "leaderboard";
            await Modal!.Show();
            _ = LoadLeaderboard();
            _ = LoadStats();
        }

        public async Task ShowStats()
        {
            activeTab = "stats";
            await Modal!.Show();
            _ = LoadLeaderboard();
            _ = LoadStats();
        }

        private void SwitchTab(string tab)
        {
            activeTab = tab;
        }

        private async Task Refresh()
        {
            if (activeTab == "leaderboard")
                await LoadLeaderboard();
            else
                await LoadStats();
        }

        private async Task LoadLeaderboard()
        {
            isLoadingLeaderboard = true;
            StateHasChanged();
            try
            {
                topScores = await LeaderboardService.GetTopScoresAsync(10);
            }
            catch
            {
                topScores = new();
            }
            isLoadingLeaderboard = false;
            StateHasChanged();
        }

        private async Task LoadStats()
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
