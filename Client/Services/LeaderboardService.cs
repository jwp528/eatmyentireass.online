using BlazorApp.Shared;
using System.Text.Json;

namespace BlazorApp.Client.Services
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardEntry>> GetTopScoresAsync(string period = "alltime", int count = 10);
        Task SaveScoreAsync(LeaderboardEntry entry, string? authToken = null);
        Task<LeaderboardEntry?> GetPlayerBestScoreAsync(string playerName);
        Task<bool> TestApiConnectionAsync();
    }

    public class LeaderboardService : ILeaderboardService
    {
        private readonly HttpClient _httpClient;

        public LeaderboardService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Task<bool> TestApiConnectionAsync() => Task.FromResult(true);

        public async Task<List<LeaderboardEntry>> GetTopScoresAsync(string period = "alltime", int count = 100)
        {
            try
            {
                var json = await _httpClient.GetStringAsync($"api/leaderboard?period={period}");
                var entries = JsonSerializer.Deserialize<List<LeaderboardEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<LeaderboardEntry>();
                return entries.Take(count).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardService] GetTopScores({period}) failed: {ex.Message}");
                return new List<LeaderboardEntry>();
            }
        }

        public async Task<LeaderboardEntry?> GetPlayerBestScoreAsync(string playerName)
        {
            try
            {
                var json = await _httpClient.GetStringAsync($"api/leaderboard/player?name={Uri.EscapeDataString(playerName)}");
                if (string.IsNullOrWhiteSpace(json) || json == "null") return null;
                return JsonSerializer.Deserialize<LeaderboardEntry>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardService] GetPlayerBestScoreAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task SaveScoreAsync(LeaderboardEntry entry, string? authToken = null)
        {
            if (string.IsNullOrWhiteSpace(entry.PlayerName))
                throw new Exception("Player name cannot be empty");

            var request = new SaveScoreRequest { Entry = entry, AuthToken = authToken };
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/leaderboard/save", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to save score: {response.StatusCode} — {error}");
            }
        }
    }
}
