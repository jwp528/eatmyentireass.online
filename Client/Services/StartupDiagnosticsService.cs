using BlazorApp.Client.Services;

namespace BlazorApp.Client.Services
{
    public interface IStartupDiagnosticsService
    {
        Task RunDiagnosticsAsync();
    }

    public class StartupDiagnosticsService : IStartupDiagnosticsService
    {
        private readonly ILeaderboardService _leaderboardService;

        public StartupDiagnosticsService(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        public async Task RunDiagnosticsAsync()
        {
            Console.WriteLine("[StartupDiagnostics] ===== API CONNECTIVITY DIAGNOSTICS =====");

            try
            {
                var isConnected = await _leaderboardService.TestApiConnectionAsync();

                if (isConnected)
                {
                    Console.WriteLine("[StartupDiagnostics] ? API CONNECTION SUCCESSFUL - Azure Functions API is running and accessible");

                    // Test leaderboard endpoint
                    try
                    {
                        var scores = await _leaderboardService.GetTopScoresAsync(count: 1);
                        Console.WriteLine($"[StartupDiagnostics] ? LEADERBOARD ENDPOINT WORKING - Retrieved {scores.Count} scores");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StartupDiagnostics] ??  LEADERBOARD ENDPOINT ISSUE: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("[StartupDiagnostics] ? API CONNECTION FAILED");
                    Console.WriteLine("[StartupDiagnostics] TROUBLESHOOTING STEPS:");
                    Console.WriteLine("[StartupDiagnostics] 1. Make sure Azure Functions API is running:");
                    Console.WriteLine("[StartupDiagnostics]    - Open terminal in 'Api' directory");
                    Console.WriteLine("[StartupDiagnostics]    - Run 'func start'");
                    Console.WriteLine("[StartupDiagnostics]    - Should see 'Host started' message");
                    Console.WriteLine("[StartupDiagnostics] 2. Check if using Aspire:");
                    Console.WriteLine("[StartupDiagnostics]    - Make sure Aspire orchestrator is running");
                    Console.WriteLine("[StartupDiagnostics]    - Both API and Client should be started together");
                    Console.WriteLine("[StartupDiagnostics] 3. Test API directly:");
                    Console.WriteLine("[StartupDiagnostics]    - Open browser to: http://localhost:7071/api/WeatherForecast");
                    Console.WriteLine("[StartupDiagnostics]    - Should return JSON weather data");
                    Console.WriteLine("[StartupDiagnostics] 4. Check firewall/antivirus:");
                    Console.WriteLine("[StartupDiagnostics]    - Make sure port 7071 is not blocked");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StartupDiagnostics] ? DIAGNOSTICS FAILED: {ex.Message}");
            }

            Console.WriteLine("[StartupDiagnostics] ===== END DIAGNOSTICS =====");
        }
    }
}