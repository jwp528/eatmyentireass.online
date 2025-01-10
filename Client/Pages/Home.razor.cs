using BlazorApp.Client.Components;
using BlazorApp.Client.Components.BootstrapCarousel;
using Microsoft.JSInterop;
using static BlazorApp.Shared.Assets;
using Timer = System.Timers.Timer;

namespace BlazorApp.Client.Pages
{
    public partial class Home
    {
        HelpModal HelpDialog;
        AboutModal AboutDialog;
        ResultsModal ResultsDialog;

        bool gamePlaying;
        Timer GameTimer;
        int GameTimeInSeconds = 60; // 1 minute
        string TimerDisplay => $"{GameTimeInSeconds / 60:D2}:{GameTimeInSeconds % 60:D2}";

        double assesEaten = 0;
        int piecesEaten = 0;

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
            GetNewAss();
        }

        void ToggleSound()
        {
            playSounds = !playSounds;
            volume = playSounds ? 1 : 0;
        }

        void GetNewAss()
        {
            CurrentAssType = BlazorApp.Shared.Assets.GetRandomAssType();
            AssFrames = BlazorApp.Shared.Assets.GetAssFrames(CurrentAssType);
        }

        async Task LoadDataAsync(string username)
        {
            assesEaten = await _localstorage.GetItemAsync<int>($"{username}_assesEaten");
        }

        void ResetGame()
        {
            assesEaten = 0;
            piecesEaten = 0;
            GameTimeInSeconds = 60;
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
            ResetGame();
            GetNewAss();
            GameTimer = new Timer(1000);
            GameTimer.Elapsed += OnTimerTick;
            GameTimer.AutoReset = true;
            GameTimer.Enabled = true;
            gamePlaying = true;
        }

        void OnTimerTick(object sender, EventArgs e)
        {
            if (GameTimeInSeconds > 0)
            {
                GameTimeInSeconds--;
                InvokeAsync(() => StateHasChanged());
                return;
            }

            GameTimer.Stop();
            gamePlaying = false;

            InvokeAsync(async () =>
            {
                await ShowResultsDialog();
                string gameOverSound = BlazorApp.Shared.Assets.GetRandomGameOverSound();

                if (playSounds)
                {
                    await js.InvokeVoidAsync("playSound", gameOverSound, volume);
                }

                StateHasChanged();
            });
        }

        async Task SaveDataAsync(string username)
        {
            await _localstorage.SetItemAsync($"{username}_assesEaten", assesEaten);
        }

        async Task EatPiece()
        {
            if (!gamePlaying)
            {
                StartGame();
            }

            if (piecesEaten == AssFrames.Count() - 1)
            {
                piecesEaten = 0;
                assesEaten += BlazorApp.Shared.Assets.GetPointsForAssType(CurrentAssType);

                Breakdown[CurrentAssType]++;
                GetNewAss();
            }
            else
            {
                ++piecesEaten;
                var sound = BlazorApp.Shared.Assets.GetBiteSoundForAssType(CurrentAssType);

                if (playSounds)
                {
                    await js.InvokeVoidAsync("playSound", sound, volume);
                }
            }
        }

        async Task ShowHelpDialog()
        {
            await HelpDialog?.Modal?.Show();
        }

        async Task ShowAboutDialog()
        {
            await AboutDialog?.Modal?.Show();
        }

        async Task ShowResultsDialog()
        {
            await ResultsDialog?.Modal?.Show();
        }

        async Task TryAgain()
        {
            ResetGame();
            await ResultsDialog?.Modal?.Hide();
        }
    }
}
