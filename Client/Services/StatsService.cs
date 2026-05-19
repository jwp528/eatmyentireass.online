using BlazorApp.Shared;
using System.Text.Json;

namespace BlazorApp.Client.Services
{
    public interface IStatsService
    {
        Task<GameStats?> GetStatsAsync();
        Task UpdateStatsAsync(GameStatsUpdate update);
    }

    public class StatsService : IStatsService
    {
        private readonly HttpClient _httpClient;

        public StatsService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<GameStats?> GetStatsAsync()
        {
            try
            {
                // Read directly from the static file — fast, no Azure Function cold-start
                var bust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var json = await _httpClient.GetStringAsync($"data/stats.json?v={bust}");
                return JsonSerializer.Deserialize<GameStats>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatsService] GetStats failed: {ex.Message}");
                return null;
            }
        }

        public async Task UpdateStatsAsync(GameStatsUpdate update)
        {
            try
            {
                var json = JsonSerializer.Serialize(update, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                await _httpClient.PostAsync("api/stats/update", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatsService] UpdateStats failed: {ex.Message}");
            }
        }
    }
}
