using BlazorApp.Shared;
using BlazorApp.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static BlazorApp.Shared.Assets;

namespace BlazorApp.Client.Components
{
    public partial class SaveScoreModal : ComponentBase
    {
        [Inject] public ILeaderboardService SharedLeaderboardService { get; set; } = default!;
        [Inject] public ILocalLeaderboardService LocalLeaderboardService { get; set; } = default!;
        [Inject] public ISettingsService SettingsService { get; set; } = default!;

        [Parameter] public double Score { get; set; }
        [Parameter] public int TotalClicks { get; set; }
        [Parameter] public Dictionary<AssTypeEnum, int> AssBreakdown { get; set; } = new();
        [Parameter] public EventCallback OnScoreSaved { get; set; }

        public BSModal? Modal { get; set; }

        private string playerName = string.Empty;
        private bool isSaving = false;
        private string errorMessage = string.Empty;
        private bool isHighScore = false;
        private int playerRank = 0;

        public async Task Show(double score, int totalClicks, Dictionary<AssTypeEnum, int> breakdown)
        {
            Score = score;
            TotalClicks = totalClicks;
            AssBreakdown = breakdown;

            // Load the last used player name
            playerName = await SettingsService.GetLastPlayerNameAsync() ?? string.Empty;

            // Check if this is a high score using shared leaderboard via API
            try
            {
                var topScores = await SharedLeaderboardService.GetTopScoresAsync(count: 10);
                isHighScore = topScores.Count < 10 || score > (topScores.LastOrDefault()?.Score ?? 0);
                playerRank = topScores.Count(x => x.Score > score) + 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveScoreModal] Error checking high score status: {ex.Message}");
                isHighScore = true; // Default to true if we can't check
                playerRank = 1;
            }

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

                Console.WriteLine($"[SaveScoreModal] Saving score to shared leaderboard via API for {entry.PlayerName}: {entry.Score}");

                // Save to shared leaderboard via API (this will write to Client/wwwroot/data/leaderboard.json)
                await SharedLeaderboardService.SaveScoreAsync(entry);

                // Also save to local leaderboard as backup
                await LocalLeaderboardService.SaveScoreAsync(entry);

                // Save the player name for next time
                await SettingsService.SetLastPlayerNameAsync(playerName.Trim());

                Console.WriteLine($"[SaveScoreModal] ? Score saved successfully to shared leaderboard and local backup!");

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
                errorMessage = $"Failed to save score to shared leaderboard: {ex.Message}";

                // Try to save to local storage as fallback
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

                    await LocalLeaderboardService.SaveScoreAsync(entry);
                    errorMessage += "\n\nScore saved to local storage as backup.";
                }
                catch (Exception localEx)
                {
                    errorMessage += $"\n\nLocal backup also failed: {localEx.Message}";
                }
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