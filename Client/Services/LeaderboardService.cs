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
                var response = await _httpClient.GetAsync("api/leaderboard");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var scores = JsonSerializer.Deserialize<List<LeaderboardEntry>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return scores ?? new List<LeaderboardEntry>();
                }
                else
                {
                    return new List<LeaderboardEntry>();
                }
            }
            catch
            {
                return new List<LeaderboardEntry>();
            }
        }

        public async Task SaveScoreAsync(LeaderboardEntry entry)
        {
            try
            {
                var json = JsonSerializer.Serialize(entry);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("api/leaderboard", content);
                // Don't throw on failure, just log it silently (matches original behavior)
            }
            catch
            {
                // Silently handle errors (matches original localStorage behavior)
            }
        }
    }
} 