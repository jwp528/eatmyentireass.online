using BlazorApp.Shared;
using System.Text.Json;
using Blazored.LocalStorage;

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
        private readonly ILocalStorageService _localStorage;
        private const string LEADERBOARD_KEY = "leaderboard_scores";

        public LeaderboardService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task<List<LeaderboardEntry>> GetAllScoresAsync()
        {
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
            var allScores = await GetAllScoresAsync();
            return allScores
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.GameDate)
                .Take(count)
                .ToList();
        }

        public async Task SaveScoreAsync(LeaderboardEntry entry)
        {
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
        }
    }
} 