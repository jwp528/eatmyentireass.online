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
            StateHasChanged();
            stats = await StatsService.GetStatsAsync();
            isLoading = false;
            StateHasChanged();
        }
    }
}

