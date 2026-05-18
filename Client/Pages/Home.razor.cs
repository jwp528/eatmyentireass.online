using BlazorApp.Client.Components;
using BlazorApp.Client.Components.BootstrapCarousel;
using BlazorApp.Client.Services;
using BlazorApp.Shared;
using Microsoft.JSInterop;
using static BlazorApp.Shared.Assets;
using Timer = System.Timers.Timer;

namespace BlazorApp.Client.Pages
{
    public partial class Home : IDisposable
    {
        HelpModal HelpDialog;
        AboutModal AboutDialog;
        AnniversaryModal AnniversaryDialog;
        UpdateModal UpdateDialog;
        ResultsModal ResultsDialog;

        bool gamePlaying;
        bool gameJustEnded; // Add this flag to prevent immediate restart
        bool gameStartTransition = false; // Drives the hint→timer tween animation
        bool hasScoreSaved; // Track if score has been saved for current game
        bool playSounds = true;
        double volume = 1;
        Timer? GameTimer;
        Timer? StatsUpdateTimer; // Add timer for updating dynamic stats
        Timer? StatsPollTimer; // Add timer for polling stats every 10 seconds
        int GameTimeInSeconds = 60; // 1 minute
        string TimerDisplay => $"{GameTimeInSeconds / 60:D2}:{GameTimeInSeconds % 60:D2}";

        double assesEaten = 0;
        int piecesEaten = 0;

        // Frenzy Mode
        bool frenzyActive = false;
        int frenzySecondsLeft = 0;
        int frenzyCount = 0;
        bool frenzyShaking = false;
        bool _mouseHeld = false;
        bool _autoEatInProgress = false;
        Timer? FrenzyCountdownTimer;
        Timer? FrenzyAutoClickTimer;

        // Add tracking for dynamic stats and polling
        DateTime gameStartTime;
        List<(int timeStamp, double clicksPerSecond)> clicksPerSecondPolls = new(); // Store polls for chart
        double CurrentClicksPerSecond => GetClicksPerSecond();

        private double GetClicksPerSecond()
        {
            if (!gamePlaying || totalClicks == 0) return 0.0;
            var elapsed = DateTime.Now - gameStartTime;
            if (elapsed.TotalSeconds < 0.1) return 0.0; // Avoid division by very small numbers
            return Math.Round(totalClicks / elapsed.TotalSeconds, 1);
        }

        List<CarouselItem> CarouselItems = new()
        {
            new()
            {
                ImageUrl = "/images/Asses/Boney/entire_ass.png",
                AltText = "Boney Ass",
                Title = "Boney",
                Description = "0.5 point. 0 nutritional value, weirdly crunchy."
            },
            new()
            {
                ImageUrl = "/images/Asses/Cartoon/entire_ass.png",
                AltText = "Cartoon Ass",
                Title = "Cartoon",
                Description = "1 point. Hand drawn, full of life, Tastes like MS Paint because it is."
            },
            new()
            {
                ImageUrl = "/images/Asses/Flat/entire_ass.png",
                AltText = "Flat Ass",
                Title = "Flat",
                Description = "1 point. Deflated rump. Flatter and straighter than most boards at home depot."
            },
            new()
            {
                ImageUrl = "/images/Asses/Hairy/entire_ass.png",
                AltText = "Hairy Ass",
                Title = "Hairy",
                Description = "1 point. Never shaven, letting it all hang out. The way god intended"
            },
            new()
            {
                ImageUrl = "/images/Asses/GYAT/entire_ass.png",
                AltText = "GYAT Ass",
                Title = "GYAT",
                Description = "2 point. GYAT damn that's one thicc juicy thang."
            },
            new()
            {
                ImageUrl = "/images/Asses/Golden/entire_ass.png",
                AltText = "Golden Ass",
                Title = "Golden",
                Description = "10 point. The holy grail of asses. Forces you to savour each sweet metallic bite."
            },
        };

        Dictionary<AssTypeEnum, int> Breakdown = new()
        {
            { AssTypeEnum.Boney, 0 },
            { AssTypeEnum.Cartoon, 0 },
            { AssTypeEnum.Flat, 0 },
            { AssTypeEnum.Golden, 0 },
            { AssTypeEnum.GYAT, 0 },
            { AssTypeEnum.Hairy, 0 }
        };

        AssTypeEnum CurrentAssType;
        List<string> AssFrames = new();

        string scoreText => assesEaten switch
        {
            0 => "Come on, don't knock it till you try it.",
            >= 100 => $"Legendary! You've reached the pinnacle of ass-eating with {Math.Truncate(assesEaten)} or more! You're a true champion!",
            >= 75 => $"Incredible! {Math.Truncate(assesEaten)} asses devoured! You're in a league of your own!",
            >= 50 => $"Fantastic! You've eaten {Math.Truncate(assesEaten)} asses! That's a monumental achievement!",
            >= 40 => $"Amazing! {Math.Truncate(assesEaten)} asses down the hatch! You're unstoppable!",
            >= 30 => $"{Math.Truncate(assesEaten)} rumps! did you even taste them?",
            >= 20 => $"{Math.Truncate(assesEaten)} asses. A hearty meal.",
            >= 10 => $"{Math.Truncate(assesEaten)} asses eaten! Maybe you're just not hungry right now",
            >= 5 => $"Only {Math.Truncate(assesEaten)} asses? It's not for everyone, at least you tried.",
            _ => "How did you even get this? I default the number to 0..."
        };

        protected override void OnInitialized()
        {
            base.OnInitialized();
            GetNewAss(); // Populate frames synchronously before first render to avoid loading spinner
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await LoadSettings();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;
            await AnniversaryDialog.ShowIfEligibleAsync();
            await CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            var hasUpdate = await VersionCheckService.IsUpdateAvailableAsync();
            if (hasUpdate && VersionCheckService.ServerVersion != null)
            {
                await UpdateDialog.ShowIfNeededAsync(VersionCheckService.ServerVersion);
            }
        }

        private async Task LoadSettings()
        {
            playSounds = await SettingsService.GetSoundEnabledAsync();
            volume = playSounds ? 1 : 0;
        }

        async Task ToggleSound()
        {
            playSounds = !playSounds;
            volume = playSounds ? 1 : 0;
            await SettingsService.SetSoundEnabledAsync(playSounds);
        }

        void GetNewAss()
        {
            try
            {
                CurrentAssType = BlazorApp.Shared.Assets.GetRandomAssType();
                AssFrames = BlazorApp.Shared.Assets.GetAssFrames(CurrentAssType);

                // Ensure piecesEaten is within bounds
                if (piecesEaten >= AssFrames.Count)
                {
                    piecesEaten = 0;
                }
            }
            catch (Exception)
            {
                // Fallback to a safe default if something goes wrong
                CurrentAssType = AssTypeEnum.Flat;
                AssFrames = BlazorApp.Shared.Assets.GetAssFrames(CurrentAssType);
                piecesEaten = 0;
            }
        }

        async Task LoadDataAsync(string username)
        {
            assesEaten = await SettingsService.GetSettingAsync($"{username}_assesEaten", 0);
        }

        void ResetGame()
        {
            assesEaten = 0;
            piecesEaten = 0;
            totalClicks = 0;
            frenzyActive = false;
            frenzySecondsLeft = 0;
            frenzyCount = 0;
            _mouseHeld = false;
            _autoEatInProgress = false;
            StopFrenzy();
            GameTimeInSeconds = 60;
            gameJustEnded = false; // Reset the flag when starting a fresh game
            hasScoreSaved = false; // Reset score saved flag for new game
            clicksPerSecondPolls.Clear(); // Clear previous game's polling data
            Breakdown = new()
            {
                { AssTypeEnum.Boney, 0 },
                { AssTypeEnum.Cartoon, 0 },
                { AssTypeEnum.Flat, 0 },
                { AssTypeEnum.Golden, 0 },
                { AssTypeEnum.GYAT, 0 },
                { AssTypeEnum.Hairy, 0 }
            };
        }

        void StartGame()
        {
            // Stop any existing timers first to prevent multiple timers
            if (GameTimer != null)
            {
                GameTimer.Stop();
                GameTimer.Dispose();
                GameTimer = null;
            }

            if (StatsUpdateTimer != null)
            {
                StatsUpdateTimer.Stop();
                StatsUpdateTimer.Dispose();
                StatsUpdateTimer = null;
            }

            if (StatsPollTimer != null)
            {
                StatsPollTimer.Stop();
                StatsPollTimer.Dispose();
                StatsPollTimer = null;
            }

            ResetGame();
            GetNewAss();

            // Capture game start time for dynamic stats
            gameStartTime = DateTime.Now;

            // Add initial poll at game start (0 seconds, 0 clicks/sec)
            clicksPerSecondPolls.Add((0, 0.0));

            // Main game timer (1 second intervals)
            GameTimer = new Timer(1000);
            GameTimer.Elapsed += OnTimerTick;
            GameTimer.AutoReset = true;
            GameTimer.Enabled = true;

            // Stats update timer (faster updates for smoother display)
            StatsUpdateTimer = new Timer(500); // Update every 500ms for smooth stats
            StatsUpdateTimer.Elapsed += OnStatsUpdateTick;
            StatsUpdateTimer.AutoReset = true;
            StatsUpdateTimer.Enabled = true;

            // Stats polling timer (every 10 seconds for chart data)
            StatsPollTimer = new Timer(10000); // Poll every 10 seconds
            StatsPollTimer.Elapsed += OnStatsPollTick;
            StatsPollTimer.AutoReset = true;
            StatsPollTimer.Enabled = true;

            gamePlaying = true;
            gameStartTransition = true;
            _ = Task.Run(async () =>
            {
                await Task.Delay(420);
                await InvokeAsync(() => { gameStartTransition = false; StateHasChanged(); });
            });

            // Force UI update
            StateHasChanged();
        }

        void OnStatsUpdateTick(object sender, EventArgs e)
        {
            if (!gamePlaying || StatsUpdateTimer == null)
            {
                return;
            }

            // Update UI for dynamic stats display
            InvokeAsync(() => StateHasChanged());
        }

        void OnStatsPollTick(object sender, EventArgs e)
        {
            if (!gamePlaying || StatsPollTimer == null)
            {
                return;
            }

            // Poll clicks per second for chart data
            var elapsed = DateTime.Now - gameStartTime;
            var timeStamp = (int)Math.Round(elapsed.TotalSeconds);
            var cps = GetClicksPerSecond();

            InvokeAsync(() =>
            {
                clicksPerSecondPolls.Add((timeStamp, cps));
                StateHasChanged();
            });
        }

        void OnTimerTick(object sender, EventArgs e)
        {
            // Double-check we're still supposed to be playing
            if (!gamePlaying || GameTimer == null)
            {
                return;
            }

            if (GameTimeInSeconds > 0)
            {
                GameTimeInSeconds--;
                InvokeAsync(() => StateHasChanged());
                return;
            }

            // Time's up! Stop the game immediately
            StopGame();

            InvokeAsync(async () =>
            {
                // Show results first, then check for leaderboard qualification
                StateHasChanged(); // Update UI to show final score

                string gameOverSound = BlazorApp.Shared.Assets.GetRandomGameOverSound();
                if (playSounds)
                {
                    await js.InvokeVoidAsync("playSound", gameOverSound, volume);
                }

                // Submit aggregate stats (fire-and-forget, don't block results)
                _ = SubmitGameStatsAsync();

                // Check daily challenge progress
                _ = CheckDailyChallengesAsync();

                // Show results immediately - no delay for API calls
                await CheckAndPromptScoreSave();
            });
        }

        async Task SaveDataAsync(string username)
        {
            await SettingsService.SetSettingAsync($"{username}_assesEaten", assesEaten);
        }

        async Task EatPiece()
        {
            // If the game just ended, don't allow immediate restart
            if (gameJustEnded)
            {
                return; // Ignore clicks immediately after game ends
            }

            if (!gamePlaying)
            {
                StartGame();
                return; // Exit early to ensure the first click doesn't count as eating
            }

            totalClicks++; // Track every click

            // Ensure we have valid frames before proceeding
            if (AssFrames?.Any() != true)
            {
                GetNewAss();
                return;
            }

            // Check if we're about to complete the current ass
            if (piecesEaten >= AssFrames.Count - 1)
            {
                var completedType = CurrentAssType;
                piecesEaten = 0;
                assesEaten += BlazorApp.Shared.Assets.GetPointsForAssType(completedType);
                Breakdown[completedType]++;

                // Assdex: track new unlocks
                GetNewAss();
                _ = CollectionService.MarkUnlockedAsync(completedType);

                if (completedType == AssTypeEnum.Golden)
                    TriggerFrenzy();
            }
            else
            {
                // Still eating this ass - advance to next frame
                piecesEaten++;
                var sound = BlazorApp.Shared.Assets.GetBiteSoundForAssType(CurrentAssType);

                if (playSounds)
                {
                    await js.InvokeVoidAsync("playSound", sound, volume);
                }
            }

            // Force UI update to show current score
            StateHasChanged();
        }

        async Task ShowHelpDialog()
        {
            await HelpDialog?.Modal?.Show();
        }

        async Task ShowAboutDialog()
        {
            await AboutDialog?.Modal?.Show();
        }

        async Task ShowStatsDialog()
        {
            if (LeaderboardStatsDialog != null) await LeaderboardStatsDialog.ShowStats();
        }

        async Task SubmitGameStatsAsync()
        {
            var update = new GameStatsUpdate
            {
                Clicks = totalClicks,
                DurationSeconds = 60,
                AssTypeBreakdown = Breakdown.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value)
            };
            await StatsService.UpdateStatsAsync(update);
        }

        async Task ShowResultsDialog()
        {
            await ResultsDialog?.Modal?.Show();
        }

        async Task CheckAndPromptScoreSave()
        {
            // Always show results dialog immediately
            await ShowResultsDialog();

            // Don't check for leaderboard qualification - let user decide if they want to save
            // This eliminates the delay from API calls
        }

        async Task TryAgain()
        {
            StopGame();
            gameJustEnded = false; // Clear the flag for intentional restart
            ResetGame();
            await ResultsDialog?.Modal?.Hide();
        }

        async Task StartNewGame()
        {
            gameJustEnded = false; // Clear the flag for intentional_restart
            StartGame();
        }

        void StopGame()
        {
            if (GameTimer != null)
            {
                GameTimer.Stop();
                GameTimer.Dispose();
                GameTimer = null;
            }

            if (StatsUpdateTimer != null)
            {
                StatsUpdateTimer.Stop();
                StatsUpdateTimer.Dispose();
                StatsUpdateTimer = null;
            }

            if (StatsPollTimer != null)
            {
                StatsPollTimer.Stop();
                StatsPollTimer.Dispose();
                StatsPollTimer = null;
            }

            // Capture final poll when game ends
            if (gamePlaying && clicksPerSecondPolls.Count > 0)
            {
                var elapsed = DateTime.Now - gameStartTime;
                var timeStamp = (int)Math.Round(elapsed.TotalSeconds);
                var cps = GetClicksPerSecond();
                clicksPerSecondPolls.Add((timeStamp, cps));
            }

            gamePlaying = false;
            gameJustEnded = true; // Set flag to prevent immediate restart
            StopFrenzy();

            // Auto-clear the flag after 5 seconds to allow restart if modals are closed
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000); // 5 second delay
                gameJustEnded = false;
                await InvokeAsync(() => StateHasChanged());
            });

            StateHasChanged();
        }

        async Task CheckDailyChallengesAsync()
        {
            var totalCount = Breakdown.Values.Sum();
            var breakdownStrings = Breakdown.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
            var avgCps = GetClicksPerSecond();
            var hasAllTypes = Breakdown.Values.All(v => v > 0);
            await DailyChallengeService.CheckGameResultAsync(totalCount, breakdownStrings, avgCps, frenzyCount, hasAllTypes);
        }

        async Task PromptAndSaveDailyScoreAsync()
        {
            await InvokeAsync(async () => await DailyDialog?.Show());
        }

        void TriggerFrenzy()
        {
            // Reset countdown (always extend to 10s on re-trigger)
            frenzySecondsLeft = 10;

            if (!frenzyActive)
            {
                frenzyActive = true;
                frenzyCount++;

                FrenzyCountdownTimer = new Timer(1000);
                FrenzyCountdownTimer.Elapsed += OnFrenzyCountdownTick;
                FrenzyCountdownTimer.AutoReset = true;
                FrenzyCountdownTimer.Enabled = true;

                // Screen shake for 0.5s, fire effect is CSS-driven while frenzy is active
                frenzyShaking = true;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    await InvokeAsync(() => { frenzyShaking = false; StateHasChanged(); });
                });

                // If the mouse is already held, kick off auto-eat immediately
                if (_mouseHeld)
                    StartFrenzyAutoClick();
            }
        }

        void OnFrenzyCountdownTick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            frenzySecondsLeft--;
            if (frenzySecondsLeft <= 0)
                InvokeAsync(StopFrenzy);
            else
                InvokeAsync(() => StateHasChanged());
        }

        void StopFrenzy()
        {
            frenzyActive = false;
            frenzySecondsLeft = 0;
            frenzyShaking = false;
            FrenzyCountdownTimer?.Stop();
            FrenzyCountdownTimer?.Dispose();
            FrenzyCountdownTimer = null;
            StopFrenzyAutoClick();
            StateHasChanged();
        }

        void OnFrenzyHoldStart()
        {
            _mouseHeld = true;
            if (frenzyActive && gamePlaying)
                StartFrenzyAutoClick();
        }

        void OnFrenzyHoldEnd()
        {
            _mouseHeld = false;
            StopFrenzyAutoClick();
        }

        void StartFrenzyAutoClick()
        {
            if (FrenzyAutoClickTimer != null) return;
            FrenzyAutoClickTimer = new Timer(100); // 10 clicks/sec
            FrenzyAutoClickTimer.Elapsed += OnFrenzyAutoClickTick;
            FrenzyAutoClickTimer.AutoReset = true;
            FrenzyAutoClickTimer.Enabled = true;
        }

        void StopFrenzyAutoClick()
        {
            FrenzyAutoClickTimer?.Stop();
            FrenzyAutoClickTimer?.Dispose();
            FrenzyAutoClickTimer = null;
        }

        async void OnFrenzyAutoClickTick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_autoEatInProgress || !frenzyActive || !gamePlaying || !_mouseHeld) return;
            _autoEatInProgress = true;
            try
            {
                await InvokeAsync(async () =>
                {
                    if (frenzyActive && gamePlaying && _mouseHeld)
                        await EatPiece();
                });
            }
            catch { }
            finally
            {
                _autoEatInProgress = false;
            }
        }

        public void Dispose()
        {
            StopFrenzy();

            if (GameTimer != null)
            {
                GameTimer.Stop();
                GameTimer.Dispose();
                GameTimer = null;
            }

            if (StatsUpdateTimer != null)
            {
                StatsUpdateTimer.Stop();
                StatsUpdateTimer.Dispose();
                StatsUpdateTimer = null;
            }

            if (StatsPollTimer != null)
            {
                StatsPollTimer.Stop();
                StatsPollTimer.Dispose();
                StatsPollTimer = null;
            }
        }
    }
}
