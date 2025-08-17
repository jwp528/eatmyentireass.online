using BlazorApp.Shared;
using BlazorApp.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static BlazorApp.Shared.Assets;

namespace BlazorApp.Client.Components
{
    public partial class SaveScoreModal : ComponentBase
    {
        [Inject] public ILeaderboardService LeaderboardService { get; set; } = default!;
        
        [Parameter] public double Score { get; set; }
        [Parameter] public int TotalClicks { get; set; }
        [Parameter] public Dictionary<AssTypeEnum, int> AssBreakdown { get; set; } = new();
        [Parameter] public EventCallback OnScoreSaved { get; set; }
        
        public BSModal? Modal { get; set; }

        private string playerName = string.Empty;
        private bool isSaving = false;
        private string errorMessage = string.Empty;

        public async Task Show(double score, int totalClicks, Dictionary<AssTypeEnum, int> breakdown)
        {
            Score = score;
            TotalClicks = totalClicks;
            AssBreakdown = breakdown;
            playerName = string.Empty;
            errorMessage = string.Empty;
            isSaving = false;
            
            Modal?.Show();
            
            // Focus the input after a short delay
            await Task.Delay(100);
            await InvokeAsync(() =>
            {
                StateHasChanged();
            });
        }

        private async Task SaveScore()
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                errorMessage = "Please enter your name.";
                return;
            }

            errorMessage = string.Empty;
            isSaving = true;
            StateHasChanged();

            try
            {
                var entry = new LeaderboardEntry
                {
                    PlayerName = playerName.Trim(),
                    Score = Score,
                    TotalClicks = TotalClicks,
                    GameDate = DateTime.Now,
                    AssTypeBreakdown = AssBreakdown.ToDictionary(
                        kvp => kvp.Key.ToString(), 
                        kvp => kvp.Value
                    ),
                    GameDurationSeconds = 60
                };

                await LeaderboardService.SaveScoreAsync(entry);
                
                Modal?.Hide();
                
                if (OnScoreSaved.HasDelegate)
                {
                    await OnScoreSaved.InvokeAsync();
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to save score. Please try again.";
            }
            finally
            {
                isSaving = false;
                StateHasChanged();
            }
        }

        private async Task HandleKeyPress(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(playerName) && !isSaving)
            {
                await SaveScore();
            }
        }
    }
} 