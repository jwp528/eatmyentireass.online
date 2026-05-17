# Running EMEA with Aspire

## Overview
The EMEA game can be run with .NET Aspire for orchestration, but requires a specific setup due to Azure Functions compatibility limitations.

## The Issue
The error message:
```
could not create Endpoint object {"Executable": {"name":"api-ewpkyqbn"}, "Reconciliation": 7, "ServiceName": "api", "Workload": "/api-ewpkyqbn", "error": "information about the port to expose the service is missing; service-producer annotation is invalid"}
```

This occurs because Azure Functions don't integrate well with Aspire's service discovery and orchestration model.

## Solution: Hybrid Approach

### Method 1: Aspire + Manual API (Recommended)

1. **Start the Azure Functions API manually**:
   ```bash
   cd Api
   func start
   ```
   - This runs the API on `http://localhost:7071`
   - Azure Functions work best when started directly

2. **Start Aspire for the Client**:
   ```bash
   cd Aspire
   dotnet run
   ```
   - This will start the Blazor client via Aspire
   - The client is configured to connect to `http://localhost:7071`

### Method 2: Traditional Separate Terminals

If you prefer not to use Aspire for now:

1. **Terminal 1 - API**:
   ```bash
   cd Api
   func start
   ```

2. **Terminal 2 - Client**:
   ```bash
   cd Client
   dotnet run
   ```

## Why This Approach?

### Azure Functions + Aspire Challenges
- Azure Functions use their own hosting model (`func.exe`)
- They don't implement standard ASP.NET Core service discovery
- Aspire expects services to support specific service discovery protocols
- The `AddExecutable` approach doesn't work well with Functions' lifecycle

### Benefits of Hybrid Approach
- ? Aspire manages the Blazor client with full dashboard support
- ? Azure Functions run in their optimal environment
- ? No port conflicts or service discovery issues
- ? Full development experience with logging and monitoring

## Aspire Dashboard Access

When running Aspire, you can access the dashboard at:
- **Dashboard URL**: Displayed in terminal when running `dotnet run` from Aspire directory
- **Client**: Shows up as managed service with logs and metrics
- **API**: Accessible externally but not managed by Aspire

## Expected Behavior

### Successful Startup
1. **API Terminal**: Should show "Host started" and function registrations
2. **Aspire Terminal**: Should show client started and dashboard URL
3. **Browser**: Navigate to client URL (usually `https://localhost:5001`)

### Testing the Integration
1. Play the game and save a score
2. Check both terminals for logs
3. Verify score appears in leaderboard
4. Check `Client/wwwroot/data/leaderboard.json` for file updates

## Alternative: Pure Aspire Solution

If you want everything managed by Aspire, you would need to:
1. Convert the API from Azure Functions to ASP.NET Core Web API
2. Update the function code to controller-based endpoints
3. This would require significant refactoring but would work seamlessly with Aspire

For now, the hybrid approach provides the best developer experience while maintaining the Azure Functions architecture.