using BlazorApp.Shared;
using BlazorApp.Client.Services;
using Microsoft.AspNetCore.Components;

namespace BlazorApp.Client.Components
{
    public partial class LeaderboardModal : ComponentBase
    {
        [Inject] public ILeaderboardService LeaderboardService { get; set; } = default!;

        public BSModal? Modal { get; set; }

        private List<LeaderboardEntry> topScores = new();
        private bool isLoading = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadScores();
        }

        public async Task Show()
        {
            await LoadScores();
            Modal?.Show();
        }

        private async Task LoadScores()
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                Console.WriteLine("[LeaderboardModal] Loading scores...");
                topScores = await LeaderboardService.GetTopScoresAsync(10);
                Console.WriteLine($"[LeaderboardModal] Loaded {topScores.Count} scores");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardModal] Error loading scores: {ex.Message}");
                topScores = new List<LeaderboardEntry>();
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task RefreshScores()
        {
            Console.WriteLine("[LeaderboardModal] Manually refreshing scores...");
            await LoadScores();
        }

        private async Task CloseModal()
        {
            await Modal?.Hide();
        }
    }
}