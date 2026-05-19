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
            try
            {
                // Read directly from the static file — bypass API cold-start.
                // Use the app's own origin (not the API base address).
                var bust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var url = new Uri(new Uri(_staticBase), $"data/stats.json?v={bust}");
                var json = await _httpClient.GetStringAsync(url);
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
