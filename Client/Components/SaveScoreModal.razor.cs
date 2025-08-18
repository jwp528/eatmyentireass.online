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
        [Inject] public ISettingsService SettingsService { get; set; } = default!;

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
            
            // Load the last used player name
            playerName = await SettingsService.GetLastPlayerNameAsync() ?? string.Empty;

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
                return;

            isSaving = true;
            errorMessage = string.Empty;
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

                // Save the player name for next time
                await SettingsService.SetLastPlayerNameAsync(playerName.Trim());

                // Close modal immediately after successful save
                Modal?.Hide();

                // Trigger callback to update parent component
                if (OnScoreSaved.HasDelegate)
                {
                    await OnScoreSaved.InvokeAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveScoreModal] Error saving score: {ex.Message}");
                errorMessage = $"Failed to save score: {ex.Message}";
            }
            finally
            {
                isSaving = false;
                StateHasChanged();
            }
        }

        private async Task SkipSave()
        {
            Modal?.Hide();

            if (OnScoreSaved.HasDelegate)
            {
                await OnScoreSaved.InvokeAsync();
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