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
        [Inject] public IPlayerService PlayerService { get; set; } = default!;

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
        private int _frenzyCount = 0;
        private int _peakFrenzyChain = 0;

        // Name claiming state
        private enum NameState { Unknown, Checking, Free, ClaimedOwned, ClaimedNotOwned }
        private NameState _nameState = NameState.Unknown;
        private CancellationTokenSource? _checkCts;

        // Password flow
        private string password = string.Empty;
        private string confirmPassword = string.Empty;
        private bool _showClaimForm = false;
        private bool _showPasswordField = false;

        public async Task Show(double score, int totalClicks, Dictionary<AssTypeEnum, int> breakdown, int frenzyCount = 0, int peakFrenzyChain = 0)
        {
            Score = score;
            TotalClicks = totalClicks;
            AssBreakdown = breakdown;
            _frenzyCount = frenzyCount;
            _peakFrenzyChain = peakFrenzyChain;
            errorMessage = string.Empty;
            isSaving = false;
            password = string.Empty;
            confirmPassword = string.Empty;
            _showClaimForm = false;
            _showPasswordField = false;
            _nameState = NameState.Unknown;

            playerName = await SettingsService.GetLastPlayerNameAsync() ?? string.Empty;

            try
            {
                var topScores = await SharedLeaderboardService.GetTopScoresAsync(count: 10);
                isHighScore = topScores.Count < 10 || score > (topScores.LastOrDefault()?.Score ?? 0);
                playerRank = topScores.Count(x => x.Score > score) + 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveScoreModal] Error checking high score status: {ex.Message}");
                isHighScore = true;
                playerRank = 1;
            }

            Modal?.Show();

            await Task.Delay(100);
            await InvokeAsync(StateHasChanged);

            if (!string.IsNullOrWhiteSpace(playerName))
                await CheckNameStateAsync(playerName);
        }

        private async Task OnNameChanged(ChangeEventArgs e)
        {
            playerName = e.Value?.ToString() ?? string.Empty;
            errorMessage = string.Empty;
            password = string.Empty;
            _showClaimForm = false;
            _showPasswordField = false;
            _nameState = NameState.Unknown;

            _checkCts?.Cancel();
            _checkCts = new CancellationTokenSource();
            var token = _checkCts.Token;

            await Task.Delay(500);
            if (token.IsCancellationRequested) return;

            await CheckNameStateAsync(playerName);
        }

        private async Task CheckNameStateAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _nameState = NameState.Unknown;
                await InvokeAsync(StateHasChanged);
                return;
            }

            _nameState = NameState.Checking;
            await InvokeAsync(StateHasChanged);

            var isClaimed = await PlayerService.IsNameClaimedAsync(name);
            if (!isClaimed)
            {
                _nameState = NameState.Free;
            }
            else
            {
                var storedToken = await PlayerService.GetStoredTokenAsync(name);
                _nameState = string.IsNullOrWhiteSpace(storedToken)
                    ? NameState.ClaimedNotOwned
                    : NameState.ClaimedOwned;
            }

            await InvokeAsync(StateHasChanged);
        }

        private async Task SaveScore()
        {
            if (string.IsNullOrWhiteSpace(playerName)) return;

            // If claimed and needs password — show the field on first Save click, then verify on second
            if (_nameState == NameState.ClaimedNotOwned)
            {
                if (!_showPasswordField)
                {
                    _showPasswordField = true;
                    StateHasChanged();
                    return;
                }
                if (string.IsNullOrWhiteSpace(password))
                {
                    errorMessage = "Enter the password for this name.";
                    return;
                }
                await RunVerifyAsync(skipFinalRender: true);
                if (_nameState != NameState.ClaimedOwned) return;
            }

            // If showing claim form, run claim first
            if (_showClaimForm)
            {
                await RunClaimAsync(skipFinalRender: true);
                if (_nameState != NameState.ClaimedOwned) return;
            }

            await DoSaveAsync();
        }

        private async Task RunVerifyAsync(bool skipFinalRender = false)
        {
            isSaving = true;
            errorMessage = string.Empty;
            StateHasChanged();
            bool hadError = false;
            try
            {
                var token = await PlayerService.VerifyNameAsync(playerName, password);
                await PlayerService.StoreTokenAsync(playerName, token);
                _nameState = NameState.ClaimedOwned;
                password = string.Empty;
            }
            catch (Exception ex)
            {
                hadError = true;
                errorMessage = ex.Message.Contains("429") ? "Too many attempts. Try again later."
                    : "Incorrect password.";
            }
            finally
            {
                isSaving = false;
                if (hadError || !skipFinalRender) StateHasChanged();
            }
        }

        private async Task RunClaimAsync(bool skipFinalRender = false)
        {
            if (password != confirmPassword)
            {
                errorMessage = "Passwords do not match.";
                return;
            }
            if (password.Length < 4)
            {
                errorMessage = "Password must be at least 4 characters.";
                return;
            }
            isSaving = true;
            errorMessage = string.Empty;
            StateHasChanged();
            bool hadError = false;
            try
            {
                var token = await PlayerService.ClaimNameAsync(playerName, password);
                await PlayerService.StoreTokenAsync(playerName, token);
                _nameState = NameState.ClaimedOwned;
                _showClaimForm = false;
                password = string.Empty;
                confirmPassword = string.Empty;
            }
            catch (Exception ex)
            {
                hadError = true;
                errorMessage = ex.Message.Contains("already claimed")
                    ? "Name was just claimed by someone else."
                    : ex.Message;
            }
            finally
            {
                isSaving = false;
                if (hadError || !skipFinalRender) StateHasChanged();
            }
        }

        private async Task DoSaveAsync()
        {
            isSaving = true;
            errorMessage = string.Empty;
            StateHasChanged();
            bool saved = false;
            try
            {
                var authToken = _nameState == NameState.ClaimedOwned
                    ? await PlayerService.GetStoredTokenAsync(playerName)
                    : null;

                var entry = new LeaderboardEntry
                {
                    PlayerName = playerName.Trim(),
                    Score = Score,
                    TotalClicks = TotalClicks,
                    GameDate = DateTime.UtcNow,
                    AssTypeBreakdown = AssBreakdown.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value),
                    GameDurationSeconds = 60,
                    FrenzyCount = _frenzyCount,
                    PeakFrenzyChain = _peakFrenzyChain
                };

                await SharedLeaderboardService.SaveScoreAsync(entry, authToken);
                await LocalLeaderboardService.SaveScoreAsync(entry);
                await SettingsService.SetLastPlayerNameAsync(playerName.Trim());
                saved = true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message.Contains("Verify identity")
                    ? "Token expired. Re-enter your password."
                    : $"Save failed: {ex.Message}";

                if (errorMessage.Contains("Token expired"))
                {
                    await PlayerService.ClearTokenAsync(playerName);
                    _nameState = NameState.ClaimedNotOwned;
                }

                try
                {
                    var entry = new LeaderboardEntry
                    {
                        PlayerName = playerName.Trim(),
                        Score = Score,
                        TotalClicks = TotalClicks,
                        GameDate = DateTime.UtcNow,
                        AssTypeBreakdown = AssBreakdown.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value),
                        GameDurationSeconds = 60,
                        FrenzyCount = _frenzyCount,
                        PeakFrenzyChain = _peakFrenzyChain
                    };
                    await LocalLeaderboardService.SaveScoreAsync(entry);
                    errorMessage += " Score saved locally as backup.";
                }
                catch { }
            }
            finally
            {
                isSaving = false;
                // Only re-render here if we're NOT closing — if saved=true we hide the modal
                // next and re-rendering after DOM removal causes "r.parentNode is null".
                if (!saved) StateHasChanged();
            }

            if (saved)
            {
                Modal?.Hide();
                if (OnScoreSaved.HasDelegate)
                    await OnScoreSaved.InvokeAsync();
            }
        }

        private async Task SkipSave()
        {
            Modal?.Hide();
            if (OnScoreSaved.HasDelegate)
                await OnScoreSaved.InvokeAsync();
        }

        private async Task HandleKeyPress(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(playerName) && !isSaving)
                await SaveScore();
        }
    }
}