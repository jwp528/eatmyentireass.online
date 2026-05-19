using BlazorApp.Shared;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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
        private readonly string _staticBase;

        public StatsService(HttpClient httpClient, IWebAssemblyHostEnvironment hostEnv)
        {
            _httpClient = httpClient;
            _staticBase = hostEnv.BaseAddress;
        }

        public async Task<GameStats?> GetStatsAsync()
        {
            // Try the API first — this is the source of truth since all writes go through the API.
            // The static stats.json on CDN is only the initial seed (all zeros) and does not
            // reflect accumulated data written by the API at runtime.
            try
            {
                var json = await _httpClient.GetStringAsync("api/stats");
                return JsonSerializer.Deserialize<GameStats>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatsService] API GetStats failed: {ex.Message}, falling back to static file");
            }

            // Fallback: read the static file (may be zeros if API is unreachable)
            try
            {
                var bust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var url = new Uri(new Uri(_staticBase), $"data/stats.json?v={bust}");
                var json = await _httpClient.GetStringAsync(url);
                return JsonSerializer.Deserialize<GameStats>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatsService] Static GetStats fallback failed: {ex.Message}");
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
