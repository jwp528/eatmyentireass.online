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
        }

        public Task<bool> TestApiConnectionAsync() => Task.FromResult(true);

        public async Task<List<LeaderboardEntry>> GetAllScoresAsync() =>
            await GetTopScoresAsync(100);

        public async Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10)
        {
            try
            {
                var bust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var url = new Uri(new Uri(_staticBase), $"data/leaderboard.json?v={bust}");
                var json = await _httpClient.GetStringAsync(url);
                var wrapper = JsonSerializer.Deserialize<LeaderboardFileWrapper>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var entries = wrapper?.Entries ?? new List<LeaderboardEntry>();
                return entries.OrderByDescending(e => e.Score).ThenByDescending(e => e.GameDate).Take(count).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardService] GetTopScores failed: {ex.Message}");
                return new List<LeaderboardEntry>();
            }
        }

        public async Task SaveScoreAsync(LeaderboardEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.PlayerName))
                throw new Exception("Player name cannot be empty");

            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/leaderboard/save", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to save score: {response.StatusCode} — {error}");
            }
        }

        private sealed class LeaderboardFileWrapper
        {
            public List<LeaderboardEntry> Entries { get; set; } = new();
        }
    }
}

