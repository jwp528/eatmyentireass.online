# "Eat My Entire Ass" Game Development Instructions

Always follow these instructions FIRST and only fallback to additional search and context gathering if the information here is incomplete or found to be in error.

## Quick Setup and Requirements

Install .NET 9.0 SDK:
```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version 9.0.101
export PATH="$HOME/.dotnet:$PATH"
```

Install Azure Functions Core Tools:
```bash
curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg
sudo mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg
sudo sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-$(lsb_release -cs)-prod $(lsb_release -cs) main" > /etc/apt/sources.list.d/dotnetdev.list'
sudo apt update && sudo apt install -y azure-functions-core-tools-4
```

Install Azure SWA CLI (optional):
```bash
npm install -g @azure/static-web-apps-cli
```

## Modal Design Standards

### Modal Structure
- Use only `modal-body`, avoid `modal-header` and `modal-footer` for cleaner design
- All content, including titles and actions, should be contained within the modal body
- Use custom styling within the body for headers and action buttons

### Modal Behavior
- Action buttons should be integrated into the modal body design
- Close functionality should be handled through custom buttons within the body

## Bootstrap and Build Process

NEVER CANCEL builds or long-running commands. Set timeout to 120+ minutes for all operations.

### Step 1: Restore Packages
```bash
cd /path/to/repo
export PATH="$HOME/.dotnet:$PATH"
dotnet restore EMEAOnline.sln
```
Expected time: **1 second** (when packages already restored). **FIRST RESTORE**: 45+ seconds. NEVER CANCEL - wait for completion.

### Step 2: Build Solution
```bash
dotnet build EMEAOnline.sln
```
Expected time: **6 seconds** (after initial setup). **FIRST BUILD**: 60+ seconds. NEVER CANCEL - set timeout to 120+ minutes.

Note: Build will succeed with warnings (nullable reference type warnings). This is normal.

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
1. Open `EMEAOnline.sln` in Visual Studio 2022
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
dotnet test EMEAOnline.sln
```
This will complete quickly with 0 tests found.

### Manual Validation Scenarios

ALWAYS perform these validation steps after making code changes:

1. **Build Validation**:
   ```bash
   dotnet build EMEAOnline.sln
   # Must succeed (warnings OK, errors not OK)
   ```

2. **API Validation**:
   ```bash
   curl http://localhost:7071/api/WeatherForecast
   # Should return JSON weather data array
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
dotnet build EMEAOnline.sln --configuration Release
```
Expected time: **6 seconds**. NEVER CANCEL - set timeout to 120+ minutes.

## Project Structure

- **Client/** - Blazor WebAssembly frontend (.NET 9.0)
  - Main game UI and logic
  - Runs on port 5000 in development (or 5001 with Aspire)
  - Builds to `Client/bin/Debug/net9.0/wwwroot`

- **Api/** - Azure Functions backend (.NET 9.0)
  - Weather forecast and leaderboard endpoints
  - Runs on port 7071 in development
  - Shared leaderboard system writes to `Client/wwwroot/data/leaderboard.json`

- **Shared/** - Common models and utilities (.NET 9.0)
  - Assets.cs contains game asset definitions
  - Shared between Client and Api

- **Aspire/** - .NET Aspire orchestrator (.NET 9.0)
  - Manages client project with dashboard
  - API should be started manually due to Azure Functions limitations

## Key Assets and Configuration

### Important Files to Monitor:
- `Shared/Assets.cs` - Game asset definitions and point values
- `Client/Pages/Home.razor.cs` - Main game logic
- `Client/wwwroot/` - Static assets (images, sounds)
- `Client/wwwroot/data/leaderboard.json` - Shared leaderboard data
- `Client/staticwebapp.config.json` - SWA deployment config
- `Api/local.settings.json` - Local API configuration
- `Api/LeaderboardFunction.cs` - Shared leaderboard API endpoints

### Asset Types and Points:
The game includes multiple "ass types" with different point values and asset counts:
- Regular: Standard points
- Flat: Lower point value
- Golden: High point value (25 assets)
- GYAT: Special type (15 assets)
- Hairy: Another variant

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
1. Ensure .NET 9.0 SDK is installed and in PATH
2. Run `dotnet restore` first
3. Check for actual errors (warnings are OK)

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