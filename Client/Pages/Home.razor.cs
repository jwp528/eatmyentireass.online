using BlazorApp.Shared;
using BlazorBootstrap;
using Microsoft.JSInterop;
using static BlazorApp.Shared.Assets;
using Timer = System.Timers.Timer;

namespace BlazorApp.Client.Pages;

public partial class Home
{
    Modal HelpDialog;
    Modal AboutDialog;
    Modal ResultsDialog;

    bool gamePlaying;
    Timer GameTimer;
    int GameTimeInSeconds = 60; // 1 minute
    string TimerDisplay => $"{GameTimeInSeconds / 60:D2}:{GameTimeInSeconds % 60:D2}";

    double assesEaten = 0;
    int piecesEaten = 0;

    // breakdown
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

    void GetNewAss()
    {
        CurrentAssType = Assets.GetRandomAssType();
        AssFrames = Assets.GetAssFrames(CurrentAssType);
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
            string gameOverSound = Assets.GetRandomGameOverSound();
            await js.InvokeVoidAsync("playSound", gameOverSound);
            StateHasChanged();
        });
    }

    async Task SaveDataAsync(string username)
    {
        _localstorage.SetItemAsync($"{username}_assesEaten", assesEaten);
    }

    async Task EatPiece()
    {
        if (!gamePlaying)
        {
            StartGame();
        }

        if (piecesEaten == AssFrames.Count()-1)
        {
            piecesEaten = 0;
            assesEaten += Assets.GetPointsForAssType(CurrentAssType);

            Breakdown[CurrentAssType]++;
            GetNewAss();
        }
        else
        {
            ++piecesEaten;
            var sound = Assets.GetBiteSoundForAssType(CurrentAssType);
            await js.InvokeVoidAsync("playSound", sound);
        }
    }

    async Task ShowHelpDialog()
    {
        await HelpDialog?.ShowAsync();
    }

    async Task HideHelpDialog()
    {
        await HelpDialog?.HideAsync();
    }


    async Task ShowAboutDialog()
    {
        await AboutDialog?.ShowAsync();
    }

    async Task HideAboutDialog()
    {
        await AboutDialog?.HideAsync();
    }

    async Task ShowResultsDialog()
    {
        await ResultsDialog?.ShowAsync();
    }

    async Task TryAgain()
    {
        ResetGame();
        await ResultsDialog?.HideAsync();
    }

    async Task HideResultsDialog()
    {
        await ResultsDialog?.HideAsync();
    }
}