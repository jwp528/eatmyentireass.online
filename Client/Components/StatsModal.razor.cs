using BlazorApp.Client.Services;
using BlazorApp.Shared;

namespace BlazorApp.Client.Components
{
    public partial class StatsModal
    {
        [Microsoft.AspNetCore.Components.Inject]
        public IStatsService StatsService { get; set; } = default!;

        public BSModal? Modal;
        private GameStats? stats;
        private bool isLoading;

        public async Task Show()
        {
            await Modal!.Show();
            await LoadStats();
        }

        private async Task LoadStats()
        {
            isLoading = true;
            Microsoft.AspNetCore.Components.ComponentBase? _ = null;
            StateHasChanged();
            stats = await StatsService.GetStatsAsync();
            isLoading = false;
            StateHasChanged();
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

