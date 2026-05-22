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
            if (Modal != null) await Modal.Show();
            await LoadScores();
        }

        private async Task LoadScores()
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                Console.WriteLine("[LeaderboardModal] Loading shared scores from API...");
                topScores = await LeaderboardService.GetTopScoresAsync(count: 10);
                Console.WriteLine($"[LeaderboardModal] ? Loaded {topScores.Count} scores from shared leaderboard");
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
            Console.WriteLine("[LeaderboardModal] Manually refreshing shared scores...");
            await LoadScores();
        }

        private async Task ClearLeaderboard()
        {
            try
            {
                Console.WriteLine("[LeaderboardModal] Clearing shared leaderboard...");
                // Note: ClearLeaderboardAsync method doesn't exist on ILeaderboardService
                // For now, just refresh the scores
                await LoadScores();
                Console.WriteLine("[LeaderboardModal] ? Leaderboard refreshed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardModal] Error refreshing leaderboard: {ex.Message}");
            }
        }

        private async Task CloseModal()
        {
            if (Modal != null) await Modal.Hide();
        }
    }
}