using BlazorApp.Shared;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text;

namespace BlazorApp.Client.Services
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10);
        Task SaveScoreAsync(LeaderboardEntry entry);
        Task<List<LeaderboardEntry>> GetAllScoresAsync();
        event EventHandler<List<LeaderboardEntry>>? LeaderboardUpdated;
        Task StartConnectionAsync();
        Task StopConnectionAsync();
    }

    public class LeaderboardService : ILeaderboardService, IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<LeaderboardService> _logger;
        private HubConnection? _hubConnection;
        private const string LEADERBOARD_KEY = "leaderboard_scores";

        public event EventHandler<List<LeaderboardEntry>>? LeaderboardUpdated;

        public LeaderboardService(HttpClient httpClient, ILocalStorageService localStorage, ILogger<LeaderboardService> logger)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _logger = logger;
        }

        public async Task StartConnectionAsync()
        {
            try
            {
                if (_hubConnection == null)
                {
                    _hubConnection = new HubConnectionBuilder()
                        .WithUrl(_httpClient.BaseAddress + "leaderboardHub")
                        .Build();

                    _hubConnection.On<List<LeaderboardEntry>>("LeaderboardUpdated", (scores) =>
                    {
                        LeaderboardUpdated?.Invoke(this, scores);
                    });

                    await _hubConnection.StartAsync();
                    await _hubConnection.InvokeAsync("JoinLeaderboardGroup");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start SignalR connection, falling back to HTTP polling");
                // Continue without real-time updates if SignalR fails
            }
        }

        public async Task StopConnectionAsync()
        {
            if (_hubConnection != null)
            {
                try
                {
                    await _hubConnection.InvokeAsync("LeaveLeaderboardGroup");
                    await _hubConnection.StopAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping SignalR connection");
                }
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
                    // Successfully saved to API, no need to save locally
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

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await StopConnectionAsync();
                await _hubConnection.DisposeAsync();
            }
        }
    }
} 