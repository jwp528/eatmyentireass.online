# "Eat My Entire Ass" Game Development Instructions

Always follow these instructions FIRST and only fallback to additional search and context gathering if the information here is incomplete or found to be in error.

## Quick Setup and Requirements

- **.NET 10.0 SDK** — required. Download from https://dotnet.microsoft.com/download
- **Azure Functions Core Tools v4** — required for the API
- **Azure SWA CLI** (optional): `npm install -g @azure/static-web-apps-cli`

## Modal Design Standards

### Modal Structure
- Use only `modal-body`, avoid `modal-header` and `modal-footer` for cleaner design
- All content, including titles and actions, should be contained within the modal body
- Use custom styling within the body for headers and action buttons

### Modal CSS Pattern
- Global `.modal-content` background is defined once in `Client/wwwroot/css/app.css` (`background: #1a1a2e`).
- **Do NOT** add `background`, `background-image`, or gradient rules to individual `.razor.css` modal files. Let the global rule apply.
- The only exception: inner content cards may use `rgba(255,255,255,0.05)` frosted-glass styling.

### Modal Behavior
- Action buttons should be integrated into the modal body design
- Close functionality should be handled through custom buttons within the body

## Frontend Design System

The frontend uses **Bootstrap for structural primitives** (grid, modal shell, utilities) and **Material Design for visual language**. Do not fight Bootstrap's box model — layer Material patterns on top.

### Design Philosophy
- **Flat, minimalistic, enterprise-grade**: No heavy drop shadows, no gradients on surfaces, no decorative borders. Use whitespace and typography weight for hierarchy.
- Reserve the accent color (`#5c6bc0` / `#7986cb`) for interactive and active states only.
- Surfaces layer through opacity overlays, not shadows.

### Dark Theme Color Tokens
| Role | Value |
|------|-------|
| Page background | `#0d0d1a` |
| Surface / modal | `#1a1a2e` |
| Inner card (frosted) | `rgba(255,255,255,0.05)` |
| Text primary | `#e8eaf6` |
| Text secondary | `rgba(255,255,255,0.5)` |
| Text muted | `rgba(255,255,255,0.35)` |
| Primary accent | `#5c6bc0` (Material indigo-600) |
| Primary accent light | `#7986cb` (Material indigo-400) |
| Warning / gold | `#f59e0b` / `#fcd34d` |
| Danger / red | `#ef5350` |
| Border subtle | `rgba(255,255,255,0.08)` |
| Border visible | `rgba(255,255,255,0.12)` |

### Elevation (Dark Theme)
Use background opacity overlays — not box shadows — to distinguish surface layers:
- `rgba(255,255,255,0.03)` — app bar, nav (barely lifted)
- `rgba(255,255,255,0.05)` — cards, inner panels
- `rgba(255,255,255,0.08)` — hover states, active chips

### Icon Button Pattern
Always use `.icon-btn` for toolbar/action icon buttons:
```css
.icon-btn {
    width: 40px; height: 40px;
    border-radius: 50%;
    background: transparent;
    color: rgba(255,255,255,0.6);
    transition: background 0.15s, color 0.15s;
}
.icon-btn:hover { background: rgba(255,255,255,0.1); color: #fff; }
.icon-btn:active { background: rgba(255,255,255,0.16); }
```

### Typography Scale
| Usage | Size | Weight | Style |
|-------|------|--------|-------|
| Display / game title | clamp / large | 900 | — |
| Heading | 1.4–1.5rem | 300–700 | — |
| Body | 0.82–0.92rem | 400–500 | — |
| Label / overline | 0.68–0.75rem | 700 | uppercase, letter-spacing 0.1–0.14em |

### Component Patterns
- **Chip**: `border-radius: 20px`, `rgba` background + `1px rgba` border, small text. Use for tags, player names, badges.
- **Tab indicator**: Bottom `2px solid` on active tab (Material tab bar). See `.hp-tab--on`.
- **Action buttons**: `border-radius: 4px`, uppercase label, weight 700, `0.6rem 1.75rem` padding. Avoid Bootstrap's default pill `.btn-rounded`.

### Motion
- Hover / focus transitions: `0.15s ease`
- Entrance animations: `0.25–0.42s ease-out`
- Spring / pop-in: `cubic-bezier(0.175, 0.885, 0.32, 1.275)`
- Fade pulse (breathing): `ease-in-out infinite alternate`

### CSS Naming Convention
- Component-scoped styles live in `.razor.css` files using a **2–3 letter kebab prefix** matching the component (e.g. `hp-` for HelpModal, `lb-` for LeaderboardModal, `sm-` for SaveScoreModal).
- Global utilities, resets, and cross-component overrides live in `Client/wwwroot/css/app.css` only.
- Never add `background` or gradient rules to individual modal `.razor.css` files — let the global `.modal-content` rule in `app.css` apply.

## Blazor DOM Stability Rules

**NEVER use `@if` to conditionally add or remove DOM elements inside async event handlers or components that receive parent-cascade re-renders.**

### Why It Crashes
Blazor's `IHandleEvent.HandleEventAsync` fires `StateHasChanged()` on the event component:
1. Once at the **first `await`** (before the awaited task completes)
2. Once on **async completion**

Plus any `EventCallback.InvokeAsync` triggers a **third cascade from the parent**. This means 2–3 concurrent render cycles fire almost simultaneously. If an `@if` block changes between renders, the second render calls `removeChild` on nodes the first already removed → `TypeError: can't access property "removeChild", r.parentNode is null`.

### The Fix: CSS Display Toggle
Always render elements; toggle visibility with `style="display:none"`:

```razor
@* ❌ BAD — crashes under concurrent renders *@
@if (isNamed)
{
    <span>@name</span>
}
else
{
    <i class="fas fa-user"></i>
}

@* ✅ GOOD — always rendered, Blazor only updates attribute *@
<span style="@(!isNamed ? "display:none" : "")">@name</span>
<i class="fas fa-user" style="@(isNamed ? "display:none" : "")"></i>
```

### ShouldRender() Guard for Modals
In modal components, block phantom re-renders after close:

```csharp
protected override bool ShouldRender() => _modalVisible;
```

Set `_modalVisible = false` **before** any `await` that triggers parent cascades (e.g., before `OnNameSaved.InvokeAsync`). Once `false`, all subsequent render requests from any source are no-ops.

### BSModal Reference
`BSModal.razor` already uses the CSS display toggle pattern — the modal DOM is always rendered, `_isVisible` only toggles `display:` style and `.show` class. **Do not regress this back to `@if (_isVisible)`.**

## Bootstrap and Build Process

NEVER CANCEL builds or long-running commands. Set timeout to 120+ minutes for all operations.

### Step 1: Restore Packages
```bash
cd /path/to/repo
export PATH="$HOME/.dotnet:$PATH"
dotnet restore EMEAOnline.slnx
```
Expected time: **1 second** (when packages already restored). **FIRST RESTORE**: 45+ seconds. NEVER CANCEL - wait for completion.

### Step 2: Build Solution
```bash
dotnet build EMEAOnline.slnx
```
Expected time: **6 seconds** (after initial setup). **FIRST BUILD**: 60+ seconds. NEVER CANCEL - set timeout to 120+ minutes.

Note: Build will succeed with warnings (nullable reference type warnings). This is normal.

**DLL Locking on Windows**: If the build fails with "file is being used by another process", stop any running `dotnet run` or `func start` processes first, then retry.

### Step 3: Set Up API Local Settings
```bash
cd Api
cp local.settings.example.json local.settings.json
```

## Development Approaches (Choose One)

### Method 1: Aspire Orchestration (Recommended for .NET 9)

**IMPORTANT**: Due to Azure Functions compatibility with Aspire, use hybrid approach:

1. **Start API manually**:
   ```bash
   cd Api
   func start
   # Runs on http://localhost:7071
   ```

2. **Start Aspire for Client**:
   ```bash
   cd Aspire
   dotnet run
   # Check terminal output for dashboard URL and client URL
   ```

3. Navigate to the client URL shown in Aspire output (typically `https://localhost:5001`)

**Why Hybrid?** Azure Functions don't integrate well with Aspire's service discovery model. The error "service-producer annotation is invalid" occurs because Functions use their own hosting model that doesn't support Aspire's orchestration protocols.

### Method 2: Visual Studio 2022 (Recommended for Windows)
1. Open `EMEAOnline.slnx` in Visual Studio 2022
2. Configure multiple startup projects:
   - Right-click solution → Configure Startup Projects
   - Select Multiple startup projects
   - Set both **Api** and **Client** to **Start**
3. Press F5 to launch
4. Navigate to displayed URL (typically `https://localhost:5001`)

### Method 3: Separate Terminals (Cross-platform)

**Terminal 1** - Start Blazor Client:
```bash
cd Client
export PATH="$HOME/.dotnet:$PATH"
dotnet run
```
Expected startup time: **3 seconds**. Runs on `http://localhost:5000`

**Terminal 2** - Start Azure Functions API:
```bash
cd Api
export PATH="$HOME/.dotnet:$PATH"
func start
```
Expected startup time: **5 seconds**. Runs on `http://localhost:7071`

### Method 4: Azure SWA CLI (Best for Azure Static Web Apps development)

**Terminal 1** - Start Client:
```bash
cd Client
export PATH="$HOME/.dotnet:$PATH"
dotnet run
```

**Terminal 2** - Start API:
```bash
cd Api
export PATH="$HOME/.dotnet:$PATH"
func start
```

**Terminal 3** - Start SWA Proxy:
```bash
swa start http://localhost:5000 --api-location http://localhost:7071
```

Access application at: `http://localhost:4280`

## Testing and Validation

### No Unit Tests Available
The repository currently contains no test projects. Run:
```bash
dotnet test EMEAOnline.slnx
```
This will complete quickly with 0 tests found.

### Manual Validation Scenarios

ALWAYS perform these validation steps after making code changes:

1. **Build Validation**:
   ```bash
   dotnet build EMEAOnline.slnx
   # Must succeed (warnings OK, errors not OK)
   ```

2. **API Validation**:
   ```bash
   curl http://localhost:7071/api/leaderboard
   # Should return JSON array of scores
   ```

3. **Leaderboard API Validation**:
   ```bash
   curl http://localhost:7071/api/leaderboard
   # Should return JSON array of scores
   ```

4. **Client Validation**:
   ```bash
   curl -I http://localhost:5000
   # Should return HTTP 200 OK with text/html content-type
   ```

5. **SWA Proxy Validation** (if using Method 4):
   ```bash
   curl -I http://localhost:4280
   # Should return HTTP 200 OK with text/html content-type
   ```

6. **Game Functionality Testing**:
   - Navigate to the running application in browser
   - Verify the game loads and displays assets
   - Test clicking on assets to "eat" them
   - Verify timer functionality works
   - Test modal dialogs (Help, About, Results)
   - **Test shared leaderboard**: Save a score and verify it appears in the leaderboard

## Code Quality and CI Requirements

ALWAYS run these before committing changes:

### Format Code:
```bash
dotnet format
```
Expected time: **12 seconds**. NEVER CANCEL - set timeout to 60+ minutes.

### Verify Formatting:
```bash
dotnet format --verify-no-changes
```
This command will EXIT CODE 2 if formatting issues exist. Fix them with `dotnet format`.

### Build for CI:
```bash
dotnet build EMEAOnline.slnx --configuration Release
```
Expected time: **6 seconds**. NEVER CANCEL - set timeout to 120+ minutes.

## Project Structure

- **Client/** - Blazor WebAssembly frontend (.NET 10.0)
  - Main game UI and logic
  - Services: `StatsService`, `ProgressService`, `CollectionService`, `VersionCheckService`, `DailyChallengeService`, `LeaderboardService`, `SettingsService`
  - Runs on port 5000 in development (or 5001 with Aspire)
  - Builds to `Client/bin/Debug/net10.0/wwwroot`

- **Api/** - Azure Functions backend (.NET 10.0)
  - Leaderboard, stats, and daily challenge endpoints
  - Runs on port 7071 in development
  - Shared leaderboard system writes to `Client/wwwroot/data/leaderboard.json`

- **Shared/** - Common models and utilities (.NET 10.0)
  - `Assets.cs` — game asset definitions (`ClicksRequired`, `HasHole`, `AssTypeEnum`, point values)
  - Shared between Client and Api

- **Aspire/** - .NET Aspire orchestrator (.NET 10.0)
  - Manages client project with dashboard
  - API should be started manually due to Azure Functions limitations

## Key Assets and Configuration

### Important Files to Monitor:
- `Shared/Assets.cs` - Game asset definitions, `ClicksRequired` dict, point values
- `Client/Pages/Home.razor.cs` - Main game logic
- `Client/Components/AssCanvas.razor` - Canvas-based eating animation component
- `Client/wwwroot/js/assCanvas.js` - Canvas eating JS module
- `Client/wwwroot/` - Static assets (images, sounds)
- `Client/wwwroot/data/leaderboard.json` - Shared leaderboard data
- `Client/staticwebapp.config.json` - SWA deployment config
- `Api/local.settings.json` - Local API configuration
- `Api/LeaderboardFunction.cs` - Shared leaderboard API endpoints

### Asset Types and Points:
The game includes multiple "ass types". Each has a `ClicksRequired` value (number of canvas bites to complete):
- **Boney**: 4 clicks — no hole.png (stays erased)
- **Cartoon**: 13 clicks
- **Flat**: 9 clicks
- **Golden**: 29 clicks — ~2% spawn rate
- **GYAT**: 14 clicks
- **Hairy**: 8 clicks

### Canvas Eating System:
Asses are eaten by erasing chunks from a single `entire_ass.png` using an HTML Canvas with `destination-out` compositing. No frame PNG files are used. The `AssCanvas.razor` component handles the display; `assCanvas.js` handles all canvas state.

## Shared Leaderboard System

The game uses a **shared leaderboard** where all players see the same scores:
- **Read**: Direct file access to `Client/wwwroot/data/leaderboard.json`
- **Write**: API calls to Azure Functions which update the shared file
- **Format**: JSON with `entries` array containing score data
- **Capacity**: Maintains top 100 scores, displays top 10

### Testing Shared Leaderboard:
1. Play game and save a score
2. Check that score appears in leaderboard modal
3. Verify file `Client/wwwroot/data/leaderboard.json` is updated
4. Test from different browser tabs/windows to confirm sharing

## Common Operations

### Adding New Game Assets:
1. Add image files to `Client/wwwroot/images/[AssetType]/`
2. Update `Shared/Assets.cs` asset dictionaries
3. Update point values in asset configuration
4. Test with manual validation scenarios

### Deployment:
The project uses Azure Static Web Apps. CI/CD is configured in:
`.github/workflows/azure-static-web-apps-brave-sea-0dfd8ff10.yml`

The workflow builds and deploys automatically on pushes to main branch.

## Troubleshooting

### Aspire + Azure Functions Issues:
If you see "service-producer annotation is invalid" error:
1. This is expected with Azure Functions + Aspire
2. Use the hybrid approach (manual API + Aspire client)
3. See `ASPIRE-SETUP.md` for detailed explanation

### Build Fails:
1. Ensure .NET 10.0 SDK is installed and in PATH
2. Run `dotnet restore` first
3. Check for actual errors (warnings are OK)
4. On Windows: stop any running `dotnet run`/`func start` processes if you see DLL lock errors

### API Won't Start:
1. Ensure Azure Functions Core Tools v4.1.2+ installed
2. Verify `Api/local.settings.json` exists
3. Check port 7071 is available

### Client Won't Start:
1. Ensure port 5000/5001 is available
2. Verify build succeeded first
3. Check `Client/Properties/launchSettings.json` for port config

### Shared Leaderboard Issues:
1. Verify API is running and accessible
2. Check `Api` terminal for file write logs
3. Ensure `Client/wwwroot/data/` directory exists
4. Test API directly: `curl http://localhost:7071/api/leaderboard`

### SWA CLI Issues:
1. Ensure both Client and API are running first
2. Verify SWA CLI v2.0.6+ installed
3. Check ports 5000 and 7071 are accessible

Remember: This project has a humorous theme but maintain professional development practices. Always run formatting and validation steps before committing changes.
