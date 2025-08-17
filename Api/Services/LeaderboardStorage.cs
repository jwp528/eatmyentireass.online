using BlazorApp.Shared;
using System.Collections.Concurrent;

namespace Api.Services
{
    public interface ILeaderboardStorage
    {
        Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10);
        Task<List<LeaderboardEntry>> GetAllScoresAsync();
        Task SaveScoreAsync(LeaderboardEntry entry);
    }

    public class InMemoryLeaderboardStorage : ILeaderboardStorage
    {
        private readonly ConcurrentBag<LeaderboardEntry> _scores = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task<List<LeaderboardEntry>> GetAllScoresAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                return _scores.ToList();
            }
            finally
            {
                _semaphore.Release();
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
            await _semaphore.WaitAsync();
            try
            {
                _scores.Add(entry);
                
                // Keep only top 100 scores to prevent unlimited growth
                var allScores = _scores.ToList();
                var topScores = allScores
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.GameDate)
                    .Take(100)
                    .ToList();

                // Clear and re-add top scores
                _scores.Clear();
                foreach (var score in topScores)
                {
                    _scores.Add(score);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}