using BlazorApp.Shared;
using Blazored.LocalStorage;
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
        private const string StorageKey = "leaderboard_v1";
        private const int MaxEntries = 100;

        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly string _staticBase;

        public LeaderboardService(HttpClient httpClient, ILocalStorageService localStorage, IWebAssemblyHostEnvironment hostEnv)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _staticBase = hostEnv.BaseAddress;
        }

        public Task<bool> TestApiConnectionAsync() => Task.FromResult(true);

        public async Task<List<LeaderboardEntry>> GetAllScoresAsync() =>
            await GetTopScoresAsync(MaxEntries);

        public async Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10)
        {
            var entries = await LoadEntriesAsync();
            return entries.OrderByDescending(e => e.Score).ThenByDescending(e => e.GameDate).Take(count).ToList();
        }

        public async Task SaveScoreAsync(LeaderboardEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.PlayerName))
                throw new Exception("Player name cannot be empty");

            var entries = await LoadEntriesAsync();
            entries.Add(entry);

            var trimmed = entries.OrderByDescending(e => e.Score).ThenByDescending(e => e.GameDate).Take(MaxEntries).ToList();
            await _localStorage.SetItemAsync(StorageKey, trimmed);
        }

        private async Task<List<LeaderboardEntry>> LoadEntriesAsync()
        {
            try
            {
                var stored = await _localStorage.GetItemAsync<List<LeaderboardEntry>>(StorageKey);
                if (stored != null) return stored;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardService] localStorage read failed: {ex.Message}");
            }

            // Seed from the committed static file if localStorage is empty
            try
            {
                var bust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var url = new Uri(new Uri(_staticBase), $"data/leaderboard.json?v={bust}");
                var json = await _httpClient.GetStringAsync(url);
                var wrapper = JsonSerializer.Deserialize<LeaderboardFileWrapper>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return wrapper?.Entries ?? new List<LeaderboardEntry>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaderboardService] Static seed read failed: {ex.Message}");
            }

            return new List<LeaderboardEntry>();
        }

        private sealed class LeaderboardFileWrapper
        {
            public List<LeaderboardEntry> Entries { get; set; } = new();
        }
    }
}
