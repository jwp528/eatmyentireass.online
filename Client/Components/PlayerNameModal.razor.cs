using BlazorApp.Client.Services;
using BlazorApp.Shared;
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
        private bool _modalVisible = false;
        private string _errorMessage = string.Empty;
        private PlayerStats? _stats = null;

        private enum NameState { Unknown, Checking, Free, ClaimedOwned, ClaimedNotOwned }
        private NameState _nameState = NameState.Unknown;
        private CancellationTokenSource? _checkCts;

        // Block all re-renders once the modal is closed. Without this, Save() being
        // an async Task event handler causes Blazor to fire StateHasChanged 2-3 times
        // concurrently (once mid-execution, once on completion, once via the parent
        // cascade from OnNameSaved). Those concurrent renders fight over the @if blocks
        // in the template, causing "r.parentNode is null".
        protected override bool ShouldRender() => _modalVisible;

        public async Task Show()
        {
            _modalVisible = true;
            _name = await SettingsService.GetLastPlayerNameAsync() ?? string.Empty;
            _password = string.Empty;
            _confirmPassword = string.Empty;
            _showClaimForm = false;
            _showPasswordField = false;
            _isBusy = false;
            _errorMessage = string.Empty;
            _nameState = NameState.Unknown;
            _stats = null;

            Modal?.Show();
            await InvokeAsync(StateHasChanged);

            if (_modalVisible && !string.IsNullOrWhiteSpace(_name))
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
                if (_modalVisible) await InvokeAsync(StateHasChanged);
                return;
            }

            _nameState = NameState.Checking;
            if (_modalVisible) await InvokeAsync(StateHasChanged);

            var isClaimed = await PlayerService.IsNameClaimedAsync(name);
            if (!_modalVisible) return;

            if (!isClaimed)
            {
                _nameState = NameState.Free;
            }
            else
            {
                var storedToken = await PlayerService.GetStoredTokenAsync(name);
                if (!_modalVisible) return;

                _nameState = string.IsNullOrWhiteSpace(storedToken)
                    ? NameState.ClaimedNotOwned
                    : NameState.ClaimedOwned;

                if (_nameState == NameState.ClaimedOwned)
                    _ = LoadStatsAsync(name);
            }

            if (_modalVisible) await InvokeAsync(StateHasChanged);
        }

        // Called when the user clicks Save.
        // This method is a multi-step state machine — it may return early several times
        // before it finally saves. Each early return leaves the modal open so the user
        // can complete the next required step.
        //
        // Flow:
        //   1. Name is free          → just save immediately
        //   2. Name is claimed by someone else
        //        a. First Save click  → show the password field, stop (return)
        //        b. Second Save click → verify the password via API, then save
        //   3. User chose to claim a free name
        //        → create the name + password via API, then save
        //
        // ⚠️  DOUBLE-RENDER HAZARD: Save() is an async Task event handler.
        //     Blazor calls StateHasChanged() on this component both at the first
        //     await AND again when the task completes. OnNameSaved.InvokeAsync()
        //     also triggers a re-render cascade from the parent. That means this
        //     component can re-render 2-3 times concurrently, which causes the
        //     @if blocks in the template to fight over DOM nodes → r.parentNode crash.
        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(_name)) return;

            // Cancel any in-flight name-check debounce so it can't fire mid-save.
            _checkCts?.Cancel();

            // ── Step 2: Name is already claimed by someone else ──────────────────
            if (_nameState == NameState.ClaimedNotOwned)
            {
                if (!_showPasswordField)
                {
                    // First Save click: reveal the password field and stop here.
                    // The user must enter the password and click Save again.
                    _showPasswordField = true;
                    StateHasChanged();
                    return;
                }

                if (string.IsNullOrWhiteSpace(_password))
                {
                    _errorMessage = "Enter the password for this name.";
                    return;
                }

                // Second Save click: verify the entered password against the API.
                // If it fails, RunVerifyAsync sets _errorMessage and leaves
                // _nameState as ClaimedNotOwned, so we bail out below.
                await RunVerifyAsync(skipFinalRender: true);
                if (_nameState != NameState.ClaimedOwned) return;
            }

            // ── Step 3: User is claiming a free name (creating a new account) ───
            if (_showClaimForm)
            {
                // Sends the name + new password to the API to register ownership.
                // If it fails, RunClaimAsync sets _errorMessage and we bail out.
                await RunClaimAsync(skipFinalRender: true);
                if (_nameState != NameState.ClaimedOwned) return;
            }

            // ── All checks passed — commit the name and close ─────────────────
            await SettingsService.SetLastPlayerNameAsync(_name.Trim());

            // Guard flag: stops any async CheckNameStateAsync continuations from
            // calling InvokeAsync(StateHasChanged) after we close.
            _modalVisible = false;

            // Hide the modal (sets display:none via CSS toggle).
            Modal?.Hide();

            // Notify the parent (Home) so it can update the player name in the UI.
            // ⚠️  This triggers Home's StateHasChanged → cascades back down here,
            //     causing the double-render that crashes. This is the problem line.
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
                // Don't fire LoadStatsAsync here — the caller may be about to close the modal.
                // Stats will load the next time the user opens the modal (CheckNameStateAsync).
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
            _stats = null;
            _nameState = NameState.ClaimedNotOwned;
            StateHasChanged();
        }

        private async Task LoadStatsAsync(string name)
        {
            try
            {
                _stats = await PlayerService.GetPlayerStatsAsync(name);
                if (_modalVisible)
                    await InvokeAsync(StateHasChanged);
            }
            catch { }
        }

        private void Cancel()
        {
            _checkCts?.Cancel();
            _modalVisible = false;
            Modal?.Hide();
        }
    }
}
