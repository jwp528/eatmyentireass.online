using BlazorApp.Shared;
using System.Text.Json;

namespace BlazorApp.Client.Services
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10);
        Task SaveScoreAsync(LeaderboardEntry entry);
        Task<List<LeaderboardEntry>> GetAllScoresAsync();
    }

    public class LeaderboardService : ILeaderboardService
    {
        private readonly HttpClient _httpClient;

        public LeaderboardService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<LeaderboardEntry>> GetAllScoresAsync()
        {
            return await GetTopScoresAsync(10); // Server only maintains top 10
        }

        public async Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10)
        {
            try
            {
                Console.WriteLine($"[LeaderboardService] Attempting to fetch scores from: {_httpClient.BaseAddress}api/leaderboard");
                var response = await _httpClient.GetAsync("api/leaderboard");
                Console.WriteLine($"[LeaderboardService] GET response status: {response.StatusCode}");
                
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
                Console.WriteLine("[LeaderboardService] This usually means the API server is not running. Start the API with 'func start' in the Api directory.");
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
                Console.WriteLine($"[LeaderboardService] API Base Address: {_httpClient.BaseAddress}");
                
                var json = JsonSerializer.Serialize(entry);
                Console.WriteLine($"[LeaderboardService] Serialized entry: {json}");
                
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/leaderboard", content);
                Console.WriteLine($"[LeaderboardService] POST response status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[LeaderboardService] Save successful: {responseContent}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[LeaderboardService] Save failed: {errorContent}");
                    throw new Exception($"Failed to save score: {response.StatusCode} - {errorContent}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"[LeaderboardService] HTTP Error in SaveScoreAsync: {httpEx.Message}");
                var errorMessage = "Cannot connect to the API server. Make sure the API is running:\n" +
                                 "1. Open a terminal in the 'Api' directory\n" +
                                 "2. Run 'func start'\n" +
                                 "3. Ensure the API is running on http://localhost:7071";
                Console.WriteLine($"[LeaderboardService] {errorMessage}");
                throw new Exception(errorMessage);
            }
            catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
            {
                Console.WriteLine($"[LeaderboardService] Timeout in SaveScoreAsync: {tcEx.Message}");
                throw new Exception("Request timed out. The API server may be overloaded or not responding.");
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