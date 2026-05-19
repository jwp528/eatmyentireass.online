using BlazorApp.Shared;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace BlazorApp.Client.Services
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10);
        Task SaveScoreAsync(LeaderboardEntry entry);
        Task<List<LeaderboardEntry>> GetAllScoresAsync();
        Task<bool> TestApiConnectionAsync();
    }

    public class LeaderboardService : ILeaderboardService
    {
        private readonly HttpClient _httpClient;
        private readonly string _staticBase;

        public LeaderboardService(HttpClient httpClient, IWebAssemblyHostEnvironment hostEnv)
        {
            _httpClient = httpClient;
            _staticBase = hostEnv.BaseAddress;
            Console.WriteLine($"[LeaderboardService] Initialized. API: {_httpClient.BaseAddress}, Static: {_staticBase}");

            // Set a reasonable timeout to prevent hanging requests
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<bool> TestApiConnectionAsync()
        {
            try
            {
                Console.WriteLine($"[LeaderboardService] Testing API connection to: {_httpClient.BaseAddress}");

                // Test with WeatherForecast endpoint first (simpler endpoint)
                var response = await _httpClient.GetAsync("api/WeatherForecast");
                Console.WriteLine($"[LeaderboardService] WeatherForecast test - Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[LeaderboardService] WeatherForecast response length: {content.Length} chars");
                    return true;
                }
                return false;
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"[LeaderboardService] API Connection Test Failed - HTTP Error: {httpEx.Message}");
                return false;
            }
            catch (TaskCanceledException tcEx)
            {
                Console.WriteLine($"[LeaderboardService] API Connection Test Failed - Timeout: {tcEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardService] API Connection Test Failed - General Error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<LeaderboardEntry>> GetAllScoresAsync()
        {
            return await GetTopScoresAsync(10); // Server only maintains top 10
        }

        public async Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10)
        {
            try
            {
                // Read directly from the static file — use the app's own origin, not API base.
                var bust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var url = new Uri(new Uri(_staticBase), $"data/leaderboard.json?v={bust}");
                var json = await _httpClient.GetStringAsync(url);
                var wrapper = JsonSerializer.Deserialize<LeaderboardFileWrapper>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                var entries = wrapper?.Entries ?? new List<LeaderboardEntry>();
                return entries.OrderByDescending(e => e.Score).Take(count).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardService] GetTopScores failed: {ex.Message}");
                return new List<LeaderboardEntry>();
            }
        }

        private sealed class LeaderboardFileWrapper
        {
            public List<LeaderboardEntry> Entries { get; set; } = new();
        }

        public async Task SaveScoreAsync(LeaderboardEntry entry)
        {
            try
            {
                Console.WriteLine($"[LeaderboardService] Attempting to save score for {entry.PlayerName}: {entry.Score}");

                var requestUrl = "api/leaderboard/save";
                var fullUrl = new Uri(_httpClient.BaseAddress!, requestUrl);
                Console.WriteLine($"[LeaderboardService] POST URL: {fullUrl}");

                // Validate entry before serialization
                if (string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    throw new Exception("Player name cannot be empty");
                }

                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
                Console.WriteLine($"[LeaderboardService] Serialized entry: {json}");

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Add additional headers for better compatibility
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var response = await _httpClient.PostAsync(requestUrl, content);
                Console.WriteLine($"[LeaderboardService] POST response status: {response.StatusCode}");
                Console.WriteLine($"[LeaderboardService] POST response reason: {response.ReasonPhrase}");
                Console.WriteLine($"[LeaderboardService] POST response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[LeaderboardService] Save successful: {responseContent}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[LeaderboardService] Save failed: {errorContent}");

                    // Provide more specific error messages based on status code
                    var errorMessage = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.MethodNotAllowed => "The API endpoint does not support POST requests. This usually means the API is not configured correctly or the wrong endpoint is being called.",
                        System.Net.HttpStatusCode.NotFound => "The API endpoint was not found. Make sure the API is running and accessible at http://localhost:7071",
                        System.Net.HttpStatusCode.BadRequest => $"The request data is invalid. Error details: {errorContent}",
                        System.Net.HttpStatusCode.InternalServerError => $"Server error occurred while saving the score. Error details: {errorContent}",
                        _ => $"Failed to save score: {response.StatusCode} - {errorContent}"
                    };

                    throw new Exception(errorMessage);
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"[LeaderboardService] HTTP Error in SaveScoreAsync: {httpEx.Message}");
                Console.WriteLine($"[LeaderboardService] Base Address: {_httpClient.BaseAddress}");

                // Provide different error messages based on the likely environment
                var isProduction = _httpClient.BaseAddress?.Host != "localhost";
                string errorMessage;

                if (isProduction)
                {
                    errorMessage = "Cannot connect to the API server. The API may be down or experiencing issues. Please try again later.";
                }
                else
                {
                    errorMessage = "Cannot connect to the API server. Make sure the API is running:\n" +
                                 "1. Open a terminal in the 'Api' directory\n" +
                                 "2. Run 'func start' (or make sure Aspire is running)\n" +
                                 "3. Ensure the API is running on http://localhost:7071\n" +
                                 "4. Test the API directly: curl http://localhost:7071/api/WeatherForecast\n" +
                                 "5. Check if port 7071 is available and not blocked by firewall";
                }

                Console.WriteLine($"[LeaderboardService] {errorMessage}");
                throw new Exception(errorMessage);
            }
            catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
            {
                Console.WriteLine($"[LeaderboardService] Timeout in SaveScoreAsync: {tcEx.Message}");
                throw new Exception("Request timed out after 30 seconds. The API server may be overloaded or not responding.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardService] Exception in SaveScoreAsync: {ex.Message}");
                Console.WriteLine($"[LeaderboardService] Stack trace: {ex.StackTrace}");
                throw; // Re-throw so the UI can handle the error
            }
        }
    }
}