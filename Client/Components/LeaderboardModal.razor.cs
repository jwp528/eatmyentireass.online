using BlazorApp.Shared;
using BlazorApp.Client.Services;
using Microsoft.AspNetCore.Components;

namespace BlazorApp.Client.Components
{
    public partial class LeaderboardModal : ComponentBase, IDisposable
    {
        [Inject] public ILeaderboardService LeaderboardService { get; set; } = default!;
        
        public BSModal? Modal { get; set; }

        private List<LeaderboardEntry> topScores = new();
        private bool isLoading = false;

        protected override async Task OnInitializedAsync()
        {
            // Subscribe to real-time updates
            LeaderboardService.LeaderboardUpdated += OnLeaderboardUpdated;
            
            await LoadScores();
        }

        private void OnLeaderboardUpdated(object? sender, List<LeaderboardEntry> scores)
        {
            InvokeAsync(() =>
            {
                topScores = scores;
                StateHasChanged();
            });
        }

        public async Task Show()
        {
            await LoadScores();
            // Start polling when modal is shown
            LeaderboardService.StartPolling();
            Modal?.Show();
        }

        private async Task LoadScores()
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                topScores = await LeaderboardService.GetTopScoresAsync(10);
            }
            catch (Exception)
            {
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
            await LoadScores();
        }

        private async Task CloseModal()
        {
            // Stop polling when modal is closed
            LeaderboardService.StopPolling();
            await Modal?.Hide();
        }

        public void Dispose()
        {
            LeaderboardService.LeaderboardUpdated -= OnLeaderboardUpdated;
            LeaderboardService.StopPolling();
        }
    }
}