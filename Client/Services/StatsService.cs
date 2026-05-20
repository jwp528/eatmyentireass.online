using BlazorApp.Shared;
using Blazored.LocalStorage;
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
        private const string StorageKey = "gamestats_v1";

        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly string _staticBase;

        public StatsService(HttpClient httpClient, ILocalStorageService localStorage, IWebAssemblyHostEnvironment hostEnv)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _staticBase = hostEnv.BaseAddress;
        }

        public async Task<GameStats?> GetStatsAsync()
        {
            try
            {
                var stored = await _localStorage.GetItemAsync<GameStats>(StorageKey);
                if (stored != null) return stored;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatsService] localStorage read failed: {ex.Message}");
            }

            // Seed from the committed static file if localStorage is empty
            try
            {
                var bust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var url = new Uri(new Uri(_staticBase), $"data/stats.json?v={bust}");
                var json = await _httpClient.GetStringAsync(url);
                return JsonSerializer.Deserialize<GameStats>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatsService] Static seed read failed: {ex.Message}");
            }

            return null;
        }

        public async Task UpdateStatsAsync(GameStatsUpdate update)
        {
            try
            {
                var stats = await GetStatsAsync() ?? CreateEmptyStats();

                stats.TotalClicks += update.Clicks;
                stats.TotalTimePlayedSeconds += update.DurationSeconds;

                if (update.AssTypeBreakdown != null)
                {
                    foreach (var kvp in update.AssTypeBreakdown)
                    {
                        if (stats.AssTypeStats.ContainsKey(kvp.Key))
                            stats.AssTypeStats[kvp.Key] += kvp.Value;
                        else
                            stats.AssTypeStats[kvp.Key] = kvp.Value;
                    }
                }

                await _localStorage.SetItemAsync(StorageKey, stats);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatsService] UpdateStats failed: {ex.Message}");
            }
        }

        private static GameStats CreateEmptyStats() => new GameStats
        {
            AssTypeStats = new Dictionary<string, long>
            {
                ["Boney"] = 0,
                ["Cartoon"] = 0,
                ["Flat"] = 0,
                ["Golden"] = 0,
                ["GYAT"] = 0,
                ["Hairy"] = 0,
                ["Regular"] = 0,
            }
        };
    }
}
