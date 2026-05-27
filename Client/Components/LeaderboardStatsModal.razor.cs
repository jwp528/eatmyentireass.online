using BlazorApp.Client.Services;
using BlazorApp.Shared;
using Microsoft.AspNetCore.Components;

namespace BlazorApp.Client.Components
{
    public partial class LeaderboardStatsModal : ComponentBase
    {
        [Inject] public ILeaderboardService LeaderboardService { get; set; } = default!;
        [Parameter] public EventCallback OnShowStats { get; set; }

        public BSModal? Modal;
        public BSModal? DetailsModal;

        private string activeTab = "alltime";
        private bool isLoadingLeaderboard;
        private string _searchText = string.Empty;
        private LeaderboardEntry? _selectedEntry;
        private int _selectedRank;

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

        private IReadOnlyList<KeyValuePair<string, int>> SelectedBreakdown =>
            (_selectedEntry?.AssTypeBreakdown ?? new Dictionary<string, int>())
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key)
                .ToList();

        public async Task ShowLeaderboard()
        {
            activeTab = "alltime";
            await Modal!.Show();
            _ = LoadPeriodAsync("alltime");
        }

        public async Task ShowStats()
        {
            await Modal!.Hide();
            await OnShowStats.InvokeAsync();
        }

        private async Task SwitchTab(string tab)
        {
            activeTab = tab;
            _searchText = string.Empty;

            if (!_loadedPeriods.Contains(tab))
            {
                await LoadPeriodAsync(tab);
            }
        }

        private async Task Refresh()
        {
            _loadedPeriods.Remove(activeTab);
            await LoadPeriodAsync(activeTab);
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

        private async Task CloseModal()
        {
            if (Modal != null) await Modal.Hide();
        }

        private async Task ShowEntryDetails(LeaderboardEntry entry, int rank)
        {
            _selectedEntry = entry;
            _selectedRank = rank;
            if (DetailsModal != null) await DetailsModal.Show();
        }

        private async Task CloseDetailsModal()
        {
            if (DetailsModal != null) await DetailsModal.Hide();
        }
    }
}
