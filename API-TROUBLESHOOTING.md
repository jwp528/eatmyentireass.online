# API Connection Troubleshooting Guide

## Issue: NS_BINDING_ABORTED Error

The "NS_BINDING_ABORTED" error in the browser's network tab indicates that the connection to the API server was terminated or could not be established.

## Quick Diagnosis Steps

### 1. Run the API Status Check
```bash
# Windows
test-api-connection.bat

# The script will test:
# - WeatherForecast endpoint
# - Leaderboard GET endpoint  
# - CORS preflight requests
# - POST functionality
# - Port availability
```

### 2. Check Console Output
After starting your application, look for these diagnostic messages:

**? Success indicators:**
```
[StartupDiagnostics] ? API CONNECTION SUCCESSFUL - Azure Functions API is running and accessible
[StartupDiagnostics] ? LEADERBOARD ENDPOINT WORKING - Retrieved X scores
[LeaderboardService] WeatherForecast test - Status: OK
```

**? Failure indicators:**
```
[StartupDiagnostics] ? API CONNECTION FAILED
[LeaderboardService] API Connection Test Failed - HTTP Error: ...
[Program] Development mode - using standard Azure Functions port: http://localhost:7071/
```

## Common Solutions

### Solution 1: Azure Functions Not Running

**Symptoms:**
- No processes on port 7071
- Connection refused errors
- NS_BINDING_ABORTED in network tab

**Fix:**
```bash
# Navigate to Api directory
cd Api

# Start Azure Functions
func start

# Should see output like:
# Host started (...)
# Http Functions:
#   GetLeaderboard: [GET] http://localhost:7071/api/leaderboard
#   SaveScore: [POST] http://localhost:7071/api/leaderboard
#   WeatherForecast: [GET] http://localhost:7071/api/WeatherForecast
```

### Solution 2: Port Conflicts

**Symptoms:**
- "Port 7071 is unavailable" error
- API starts but can't bind to port

**Fix:**
```bash
# Kill processes using port 7071
kill-port-7071.bat

# Then restart your application
```

### Solution 3: Aspire Configuration Issues

**Symptoms:**
- Using Aspire but API not starting
- Port mismatches between client and API

**Fix:**
```bash
# Make sure to start from Aspire orchestrator
cd Aspire
dotnet run

# Check Aspire dashboard for service status
# Both API and Client should show as running
```

### Solution 4: CORS Configuration

**Symptoms:**
- API starts but CORS errors in browser
- OPTIONS requests failing

**Fix:**
Verify `Api/local.settings.json` contains:
```json
{
    "Host": {
        "CORS": "*"
    }
}
```

### Solution 5: Firewall/Antivirus Blocking

**Symptoms:**
- API appears to start but connections fail
- Inconsistent connection behavior

**Fix:**
1. Temporarily disable firewall/antivirus
2. Add exception for port 7071
3. Add exception for `func.exe` process

## Development Workflow

### Using Aspire (Recommended)
```bash
# 1. Clean up any stuck processes
kill-port-7071.bat

# 2. Start from Aspire orchestrator  
cd Aspire
dotnet run

# 3. Check both services are running in Aspire dashboard
# 4. Client should auto-detect API at http://localhost:7071/
```

### Manual Startup (Alternative)
```bash
# Terminal 1: Start API
cd Api
func start

# Terminal 2: Start Client  
cd Client
dotnet run

# 3. Client runs on http://localhost:5000
# 4. API runs on http://localhost:7071
```

## Testing API Manually

### 1. Test WeatherForecast (Simple endpoint)
```bash
curl http://localhost:7071/api/WeatherForecast
# Should return JSON weather data array
```

### 2. Test Leaderboard GET
```bash
curl http://localhost:7071/api/leaderboard  
# Should return empty array [] or existing scores
```

### 3. Test Leaderboard POST
```bash
curl -X POST -H "Content-Type: application/json" \
  -d '{"playerName":"TestUser","score":1000,"totalClicks":50,"gameDate":"2024-01-01T00:00:00Z","gameDurationSeconds":60,"assTypeBreakdown":{}}' \
  http://localhost:7071/api/leaderboard
# Should return "Score saved successfully"
```

## Enhanced Logging

The updated LeaderboardService now provides detailed diagnostics:

1. **Connectivity Testing** - Tests WeatherForecast endpoint first
2. **Request/Response Logging** - Shows full HTTP details
3. **Error Classification** - Specific error messages by HTTP status
4. **Startup Diagnostics** - Automatic API health check on app start

## Next Steps

1. **Run the diagnostic script** (`test-api-connection.bat`)
2. **Check console output** for detailed error messages
3. **Ensure API is running** on port 7071
4. **Verify CORS configuration** in local.settings.json
5. **Test manual API calls** using curl or browser

If issues persist after following these steps, the enhanced logging should provide specific error details to help identify the root cause.