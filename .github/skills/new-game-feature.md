---
name: new-game-feature
description: Pattern and checklist for adding a new game mechanic, mode, stat, or UI feature to the Eat My Entire Ass game. Use this skill when adding something new to the game loop, scoring, frenzy system, or game UI.
---

# New Game Feature Skill

This skill covers the patterns, file locations, and safety rules for adding new features to the game.

## File Map

| What | Where |
|------|-------|
| Game state (fields, flags, timers) | `Client/Pages/Home.razor.cs` — `partial class Home` |
| Game UI template | `Client/Pages/Home.razor` |
| Game UI styles | `Client/Pages/Home.razor.css` |
| Shared data / asset definitions | `Shared/Assets.cs` |
| API endpoints (leaderboard, stats) | `Api/LeaderboardFunction.cs` |
| Shared models | `Shared/` |

---

## Adding State (Home.razor.cs)

State fields go directly in `partial class Home`. Group related fields with a comment header:

```csharp
// ── My New Feature ────────────────────────────────────────────────
bool _myFeatureActive = false;
int _myFeatureValue = 0;
Timer? MyFeatureTimer;
CancellationTokenSource? _myFeatureCts;
```

### Timer Safety
Timers fire on a background thread. Always marshal UI updates through `InvokeAsync`:

```csharp
MyFeatureTimer = new Timer(1000);
MyFeatureTimer.Elapsed += async (_, _) =>
{
    // Update state
    _myFeatureValue++;

    // Always use InvokeAsync to touch UI from timer callback
    await InvokeAsync(StateHasChanged);
};
MyFeatureTimer.Start();
```

Dispose timers in `IDisposable.Dispose()`:
```csharp
public void Dispose()
{
    MyFeatureTimer?.Dispose();
    _myFeatureCts?.Cancel();
}
```

### Cancellation Tokens
For async operations that may be cancelled (e.g., held-click auto-eat), use `CancellationTokenSource`:
```csharp
_myFeatureCts = new CancellationTokenSource();
try
{
    while (!_myFeatureCts.Token.IsCancellationRequested)
    {
        await Task.Delay(100, _myFeatureCts.Token);
        // do work
    }
}
catch (TaskCanceledException) { }
```

---

## Adding UI (Home.razor)

### DOM Stability Rule — CRITICAL
**Never use `@if` to add/remove DOM elements** in components that receive async re-renders. Use `style="display:none"` toggling instead.

See `blazor-dom-safety` skill for full explanation.

```razor
@* ❌ BAD — will crash under concurrent renders *@
@if (_myFeatureActive) { <div class="feature-banner">...</div> }

@* ✅ GOOD — always rendered, toggled via CSS *@
<div class="feature-banner" style="@(!_myFeatureActive ? "display:none" : "")">
    ...
</div>
```

### Connecting to the Frenzy System

The frenzy system state lives in `Home.razor.cs`:

```csharp
bool frenzyActive = false;
int frenzySecondsLeft = 0;
int frenzyChainLevel = 0;   // 1 = base frenzy, 2+ = chain (×N score multiplier)
int _peakChainLevel = 0;    // highest chain reached this game
```

To hook into frenzy:
- Check `frenzyActive` for frenzy-specific behavior
- Check `frenzyChainLevel` for chain multiplier (score × chain level)
- Frenzy begins in `StartFrenzy()` / ends in `EndFrenzy()`
- Chain increments when frenzy triggers during an active frenzy

### Scoring Pattern

Points are accumulated in `assesEaten`. Apply the frenzy chain multiplier:

```csharp
var basePoints = Assets.GetPointsForAssType(currentAssType);
var multiplier = frenzyActive ? Math.Max(1, frenzyChainLevel) : 1;
assesEaten += basePoints * multiplier;
```

---

## Adding Styles (Home.razor.css)

Use the existing CSS variable/pattern system. Group new styles with a comment header:

```css
/* ── My New Feature ─────────────────────────────────────────────── */
.my-feature-banner {
    position: absolute;
    /* ... */
    animation: my-feature-in 0.35s cubic-bezier(0.175, 0.885, 0.32, 1.275) both;
}

@keyframes my-feature-in {
    from { opacity: 0; transform: translateY(-12px); }
    to   { opacity: 1; transform: translateY(0); }
}
```

Follow the Bootstrap + Material Design rules from `bootstrap-material-ui` skill.

---

## Connecting Services

These services are injected in `Home.razor` and available in `Home.razor.cs`:

| Service | Use for |
|---------|---------|
| `IStatsService` | Reading/writing per-session game stats |
| `IProgressService` | Ass type unlock progress, perks |
| `ICollectionService` | Player's collected ass types |
| `ISettingsService` | Player name, sound preferences |
| `IDailyChallengeService` | Daily challenge state and scores |
| `ILeaderboardService` | Reading/submitting leaderboard scores |

---

## Checklist

- [ ] State fields added to `Home.razor.cs` with descriptive comment header
- [ ] Timers use `InvokeAsync(StateHasChanged)` — never direct `StateHasChanged()` from background threads
- [ ] Timers disposed in `Dispose()`
- [ ] UI elements use `display:none` toggle — no `@if` adding/removing elements
- [ ] Scoring uses the frenzy chain multiplier where appropriate
- [ ] CSS grouped under a clear comment header in `Home.razor.css`
- [ ] New CSS follows Bootstrap + Material Design patterns (`bootstrap-material-ui` skill)
- [ ] Run `dotnet build EMEAOnline.slnx` — no errors
- [ ] Run `dotnet format` before committing
