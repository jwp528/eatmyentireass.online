using BlazorApp.Shared;
using System.Text.Json;
using Blazored.LocalStorage;
using System.Text;

namespace BlazorApp.Client.Services
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10);
        Task SaveScoreAsync(LeaderboardEntry entry);
        Task<List<LeaderboardEntry>> GetAllScoresAsync();
        event EventHandler<List<LeaderboardEntry>>? LeaderboardUpdated;
        void StartPolling();
        void StopPolling();
    }

    public class LeaderboardService : ILeaderboardService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<LeaderboardService> _logger;
        private Timer? _pollingTimer;
        private const string LEADERBOARD_KEY = "leaderboard_scores";
        private const int POLLING_INTERVAL_MS = 5000; // Poll every 5 seconds

        public event EventHandler<List<LeaderboardEntry>>? LeaderboardUpdated;

        public LeaderboardService(HttpClient httpClient, ILocalStorageService localStorage, ILogger<LeaderboardService> logger)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _logger = logger;
        }

        public void StartPolling()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = new Timer(PollLeaderboard, null, 0, POLLING_INTERVAL_MS);
        }

        public void StopPolling()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }

        private async void PollLeaderboard(object? state)
        {
            try
            {
                var scores = await GetTopScoresAsync(10);
                LeaderboardUpdated?.Invoke(this, scores);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error polling leaderboard");
            }
        }

        public async Task<List<LeaderboardEntry>> GetAllScoresAsync()
        {
            try
            {
                // Try to get from API first
                var response = await _httpClient.GetAsync("api/GetLeaderboard?count=100");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<LeaderboardEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<LeaderboardEntry>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get scores from API, falling back to local storage");
            }

            // Fallback to local storage
            try
            {
                var leaderboard = await _localStorage.GetItemAsync<Leaderboard>(LEADERBOARD_KEY);
                return leaderboard?.Entries ?? new List<LeaderboardEntry>();
            }
            catch
            {
                return new List<LeaderboardEntry>();
            }
        }

        public async Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10)
        {
            try
            {
                // Try to get from API first
                var response = await _httpClient.GetAsync($"api/GetLeaderboard?count={count}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<LeaderboardEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<LeaderboardEntry>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get scores from API, falling back to local storage");
            }

            // Fallback to local storage
            var allScores = await GetAllScoresAsync();
            return allScores
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.GameDate)
                .Take(count)
                .ToList();
        }

        public async Task SaveScoreAsync(LeaderboardEntry entry)
        {
            try
            {
                // Try to save to API first
                var json = JsonSerializer.Serialize(entry);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/SaveScore", content);
                
                if (response.IsSuccessStatusCode)
                {
                    // Successfully saved to API, trigger immediate update
                    var updatedScores = await GetTopScoresAsync(10);
                    LeaderboardUpdated?.Invoke(this, updatedScores);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save score to API, falling back to local storage");
            }

            // Fallback to local storage
            var allScores = await GetAllScoresAsync();
            allScores.Add(entry);
            
            // Keep only top 100 scores to prevent unlimited growth
            var topScores = allScores
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.GameDate)
                .Take(100)
                .ToList();

            var leaderboard = new Leaderboard { Entries = topScores };
            await _localStorage.SetItemAsync(LEADERBOARD_KEY, leaderboard);

            // Manually trigger the event for fallback
            LeaderboardUpdated?.Invoke(this, topScores.Take(10).ToList());
        }

        public void Dispose()
        {
            StopPolling();
        }
    }
} 