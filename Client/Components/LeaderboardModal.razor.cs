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
            await Modal?.Hide();
        }
    }
}