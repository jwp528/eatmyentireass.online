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

        // Progress / perks cache (loaded in OnInitializedAsync, safe defaults until then)
        Dictionary<AssTypeEnum, AssTypeProgress> _progressCache =
            Enum.GetValues<AssTypeEnum>().ToDictionary(t => t, _ => new AssTypeProgress());
        int _effectiveCompleteThreshold = 0;

        // Frenzy Mode
        bool frenzyActive = false;
        int frenzySecondsLeft = 0;
        int frenzyCount = 0;
        int frenzyChainLevel = 0;  // 1 = base frenzy, 2+ = chained (applies ×n score multiplier)
        int _peakChainLevel = 0;   // highest chain level reached this game
        bool _mouseHeld = false;
        CancellationTokenSource? _autoEatCts;
        Timer? FrenzyCountdownTimer;

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
            _progressCache = await ProgressService.LoadAsync();
            _currentPlayerName = await SettingsService.GetLastPlayerNameAsync() ?? string.Empty;
            await LoadSettings();
            ParseChallengeFromUrl();
        }

        private void ParseChallengeFromUrl()
        {
            var uri = new Uri(NavigationManager.Uri);
            var query = uri.Query.TrimStart('?');
            if (string.IsNullOrEmpty(query)) return;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2)
                    dict[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
            }

            if (dict.TryGetValue("challenge", out var scoreStr) &&
                double.TryParse(scoreStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var score))
            {
                _challengeScore = score;
                _challengerName = dict.TryGetValue("challenger", out var n) && !string.IsNullOrWhiteSpace(n) ? n : "Someone";
                _showChallengeBanner = true;
            }
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
                CurrentAssType = PickNextAssType();
                AssFrames = BlazorApp.Shared.Assets.GetAssFrames(CurrentAssType);
                _effectiveCompleteThreshold = ProgressService.GetEffectiveCompleteThreshold(CurrentAssType, _progressCache[CurrentAssType]);

                if (piecesEaten >= AssFrames.Count)
                {
                    piecesEaten = 0;
                }
            }
            catch (Exception)
            {
                CurrentAssType = AssTypeEnum.Flat;
                AssFrames = BlazorApp.Shared.Assets.GetAssFrames(CurrentAssType);
                _effectiveCompleteThreshold = ProgressService.GetEffectiveCompleteThreshold(CurrentAssType, _progressCache[CurrentAssType]);
                piecesEaten = 0;
            }
        }

        AssTypeEnum PickNextAssType()
        {
            // ~2% chance of Golden
            if (Random.Shared.Next(50) == 0)
                return AssTypeEnum.Golden;

            // Build weighted pool for non-golden types
            var pool = new List<AssTypeEnum>();
            foreach (var type in Enum.GetValues<AssTypeEnum>())
            {
                if (type == AssTypeEnum.Golden) continue;
                var count = _progressCache.TryGetValue(type, out var p) && p.HasPerk25 ? 2 : 1;
                for (int i = 0; i < count; i++) pool.Add(type);
            }
            return pool[Random.Shared.Next(pool.Count)];
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
            frenzyChainLevel = 0;
            _peakChainLevel = 0;
            _mouseHeld = false;
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

            // Pause main countdown while frenzy is active
            if (frenzyActive)
                return;

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

                // Write stats to file before showing results so Global Stats tab is current
                await SubmitGameStatsAsync();

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
            if (piecesEaten >= _effectiveCompleteThreshold)
            {
                var completedType = CurrentAssType;
                var clicksUsed = piecesEaten + 1;
                piecesEaten = 0;
                var basePoints = ProgressService.GetEffectivePoints(completedType, _progressCache[completedType]);
                var chainMultiplier = frenzyActive && frenzyChainLevel >= 2 ? frenzyChainLevel : 1;
                assesEaten += basePoints * chainMultiplier;
                Breakdown[completedType]++;

                // Update progress cache synchronously before getting next ass
                _progressCache[completedType].Eaten++;
                _progressCache[completedType].ClicksUsed += clicksUsed;

                // Assdex: track new unlocks; get next ass (uses updated cache for threshold)
                GetNewAss();
                _ = CollectionService.MarkUnlockedAsync(completedType);
                _ = ProgressService.SaveAsync(_progressCache);

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

        async Task OpenAssDexFromHelp()
        {
            await AssdexDialog?.Show();
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
            try
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
            catch (Exception ex)
            {
                Console.WriteLine($"[Home] Stats submission failed (non-critical): {ex.Message}");
            }
        }

        async Task ShowResultsDialog()
        {
            await ResultsDialog?.Modal?.Show();
        }

        async Task CheckAndPromptScoreSave()
        {
            _isPersonalBest = false;
            _personalBestScore = null;
            await ShowResultsDialog();

            var savedName = await SettingsService.GetLastPlayerNameAsync();
            if (string.IsNullOrWhiteSpace(savedName)) return;

            try
            {
                var playerBest = await LeaderboardService.GetPlayerBestScoreAsync(savedName);
                bool isNewBest = playerBest == null || assesEaten > playerBest.Score;

                _personalBestScore = playerBest?.Score;
                _isPersonalBest = isNewBest;
                await InvokeAsync(StateHasChanged);

                if (isNewBest)
                {
                    await Task.Delay(300);
                    await SaveScoreDialog?.Show((double)assesEaten, totalClicks, Breakdown);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Home] Auto-show save score check failed (non-critical): {ex.Message}");
            }
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
            _isPersonalBest = false;
            _personalBestScore = null;
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
            frenzyCount++;

            if (!frenzyActive)
            {
                frenzyActive = true;
                frenzyChainLevel = 1;
                _ = js.InvokeVoidAsync("toggleBodyShake", true);

                FrenzyCountdownTimer = new Timer(1000);
                FrenzyCountdownTimer.Elapsed += OnFrenzyCountdownTick;
                FrenzyCountdownTimer.AutoReset = true;
                FrenzyCountdownTimer.Enabled = true;

                // If the mouse is already held, kick off auto-eat immediately
                if (_mouseHeld)
                    _ = StartFrenzyAutoClickLoop();
            }
            else
            {
                // Frenzy within a frenzy — escalate the chain!
                frenzyChainLevel++;
            }

            _peakChainLevel = Math.Max(_peakChainLevel, frenzyChainLevel);
            StateHasChanged();
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
            frenzyChainLevel = 0;
            _ = js.InvokeVoidAsync("toggleBodyShake", false);
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
                _ = StartFrenzyAutoClickLoop();
        }

        void OnFrenzyHoldEnd()
        {
            _mouseHeld = false;
            StopFrenzyAutoClick();
        }

        async Task StartFrenzyAutoClickLoop()
        {
            // Cancel any existing loop before starting a new one
            _autoEatCts?.Cancel();
            var cts = new CancellationTokenSource();
            _autoEatCts = cts;

            try
            {
                while (!cts.IsCancellationRequested && _mouseHeld && frenzyActive && gamePlaying)
                {
                    await EatPiece();
                    await Task.Delay(100, cts.Token); // 10 clicks/sec
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[FrenzyAutoClick] Error: {ex.Message}");
            }
        }

        void StopFrenzyAutoClick()
        {
            _autoEatCts?.Cancel();
            _autoEatCts = null;
        }

        public void Dispose()
        {
            // Cancel async operations immediately (no JS interop or StateHasChanged during disposal)
            _autoEatCts?.Cancel();
            _autoEatCts = null;

            FrenzyCountdownTimer?.Stop();
            FrenzyCountdownTimer?.Dispose();
            FrenzyCountdownTimer = null;

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
