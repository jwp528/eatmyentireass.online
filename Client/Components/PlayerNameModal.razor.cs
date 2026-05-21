using BlazorApp.Client.Services;
using Microsoft.AspNetCore.Components;

namespace BlazorApp.Client.Components
{
    public partial class PlayerNameModal : ComponentBase
    {
        [Inject] public IPlayerService PlayerService { get; set; } = default!;
        [Inject] public ISettingsService SettingsService { get; set; } = default!;

        [Parameter] public EventCallback<string> OnNameSaved { get; set; }

        public BSModal? Modal { get; set; }

        private string _name = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private bool _showClaimForm = false;
        private bool _showPasswordField = false;
        private bool _isBusy = false;
        private string _errorMessage = string.Empty;

        private enum NameState { Unknown, Checking, Free, ClaimedOwned, ClaimedNotOwned }
        private NameState _nameState = NameState.Unknown;
        private CancellationTokenSource? _checkCts;

        public async Task Show()
        {
            _name = await SettingsService.GetLastPlayerNameAsync() ?? string.Empty;
            _password = string.Empty;
            _confirmPassword = string.Empty;
            _showClaimForm = false;
            _showPasswordField = false;
            _isBusy = false;
            _errorMessage = string.Empty;
            _nameState = NameState.Unknown;

            Modal?.Show();

            await Task.Delay(100);
            await InvokeAsync(StateHasChanged);

            if (!string.IsNullOrWhiteSpace(_name))
                await CheckNameStateAsync(_name);
        }

        private async Task OnNameChanged(ChangeEventArgs e)
        {
            _name = e.Value?.ToString() ?? string.Empty;
            _errorMessage = string.Empty;
            _password = string.Empty;
            _showClaimForm = false;
            _showPasswordField = false;
            _nameState = NameState.Unknown;

            _checkCts?.Cancel();
            _checkCts = new CancellationTokenSource();
            var token = _checkCts.Token;

            await Task.Delay(500);
            if (token.IsCancellationRequested) return;

            await CheckNameStateAsync(_name);
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

        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(_name)) return;

            if (_nameState == NameState.ClaimedNotOwned)
            {
                if (!_showPasswordField)
                {
                    _showPasswordField = true;
                    StateHasChanged();
                    return;
                }
                if (string.IsNullOrWhiteSpace(_password))
                {
                    _errorMessage = "Enter the password for this name.";
                    return;
                }
                await RunVerifyAsync(skipFinalRender: true);
                if (_nameState != NameState.ClaimedOwned) return;
            }

            if (_showClaimForm)
            {
                await RunClaimAsync(skipFinalRender: true);
                if (_nameState != NameState.ClaimedOwned) return;
            }

            await SettingsService.SetLastPlayerNameAsync(_name.Trim());
            Modal?.Hide();
            if (OnNameSaved.HasDelegate)
                await OnNameSaved.InvokeAsync(_name.Trim());
        }

        private async Task RunVerifyAsync(bool skipFinalRender = false)
        {
            _isBusy = true;
            _errorMessage = string.Empty;
            StateHasChanged();
            bool hadError = false;
            try
            {
                var token = await PlayerService.VerifyNameAsync(_name, _password);
                await PlayerService.StoreTokenAsync(_name, token);
                _nameState = NameState.ClaimedOwned;
                _password = string.Empty;
            }
            catch (Exception ex)
            {
                hadError = true;
                _errorMessage = ex.Message.Contains("429") ? "Too many attempts. Try again later."
                    : "Incorrect password.";
            }
            finally
            {
                _isBusy = false;
                // Only re-render here if there was an error (so error message shows).
                // On success the caller is about to close the modal — re-rendering after
                // the modal is removed from the DOM causes "r.parentNode is null".
                if (hadError || !skipFinalRender) StateHasChanged();
            }
        }

        private async Task RunClaimAsync(bool skipFinalRender = false)
        {
            if (_password != _confirmPassword)
            {
                _errorMessage = "Passwords do not match.";
                return;
            }
            if (_password.Length < 4)
            {
                _errorMessage = "Password must be at least 4 characters.";
                return;
            }
            _isBusy = true;
            _errorMessage = string.Empty;
            StateHasChanged();
            bool hadError = false;
            try
            {
                var token = await PlayerService.ClaimNameAsync(_name, _password);
                await PlayerService.StoreTokenAsync(_name, token);
                _nameState = NameState.ClaimedOwned;
                _showClaimForm = false;
                _password = string.Empty;
                _confirmPassword = string.Empty;
            }
            catch (Exception ex)
            {
                hadError = true;
                _errorMessage = ex.Message.Contains("already claimed")
                    ? "Name was just claimed by someone else."
                    : ex.Message;
            }
            finally
            {
                _isBusy = false;
                if (hadError || !skipFinalRender) StateHasChanged();
            }
        }

        private async Task Logout()
        {
            await PlayerService.ClearTokenAsync(_name);
            _nameState = NameState.ClaimedNotOwned;
            StateHasChanged();
        }

        private void Cancel()
        {
            Modal?.Hide();
        }
    }
}
