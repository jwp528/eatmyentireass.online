using BlazorApp.Shared;
using System.Text.Json;

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

        public LeaderboardService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            Console.WriteLine($"[LeaderboardService] Initialized with BaseAddress: {_httpClient.BaseAddress}");

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
                // Test API connection first
                var isApiConnected = await TestApiConnectionAsync();
                if (!isApiConnected)
                {
                    Console.WriteLine("[LeaderboardService] API connection test failed, returning empty list");
                    return new List<LeaderboardEntry>();
                }

                var requestUrl = "api/leaderboard";
                var fullUrl = new Uri(_httpClient.BaseAddress!, requestUrl);
                Console.WriteLine($"[LeaderboardService] Attempting to fetch scores from: {fullUrl}");

                var response = await _httpClient.GetAsync(requestUrl);
                Console.WriteLine($"[LeaderboardService] GET response status: {response.StatusCode}");
                Console.WriteLine($"[LeaderboardService] GET response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[LeaderboardService] Response content: {json}");

                    var scores = JsonSerializer.Deserialize<List<LeaderboardEntry>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    Console.WriteLine($"[LeaderboardService] Deserialized {scores?.Count ?? 0} scores");
                    return scores ?? new List<LeaderboardEntry>();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[LeaderboardService] Error response: {errorContent}");
                    return new List<LeaderboardEntry>();
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"[LeaderboardService] HTTP Error in GetTopScoresAsync: {httpEx.Message}");
                Console.WriteLine($"[LeaderboardService] Base Address: {_httpClient.BaseAddress}");
                Console.WriteLine("[LeaderboardService] This usually means the API server is not running or not accessible.");
                return new List<LeaderboardEntry>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardService] Exception in GetTopScoresAsync: {ex.Message}");
                Console.WriteLine($"[LeaderboardService] Stack trace: {ex.StackTrace}");
                return new List<LeaderboardEntry>();
            }
        }

        public async Task SaveScoreAsync(LeaderboardEntry entry)
        {
            try
            {
                Console.WriteLine($"[LeaderboardService] Attempting to save score for {entry.PlayerName}: {entry.Score}");

                // Enhanced connectivity test before attempting to save
                var isApiConnected = await TestApiConnectionAsync();
                if (!isApiConnected)
                {
                    throw new Exception("Cannot connect to the API server. The API may be down or not accessible. Please make sure the Azure Functions API is running on http://localhost:7071");
                }

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