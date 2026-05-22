using BlazorApp.Shared;
using Blazored.LocalStorage;
using System.Text.Json;

namespace BlazorApp.Client.Services
{
    public interface ILocalLeaderboardService
    {
        Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10);
        Task SaveScoreAsync(LeaderboardEntry entry);
        Task<List<LeaderboardEntry>> GetAllScoresAsync();
        Task ClearLeaderboardAsync();
        Task<int> GetPlayerRankAsync(string playerName, double score);
        Task<bool> IsHighScoreAsync(double score);
    }

    public class LocalLeaderboardService : ILocalLeaderboardService
    {
        private readonly ILocalStorageService _localStorage;
        private const string LEADERBOARD_KEY = "emea-leaderboard";
        private const int MAX_ENTRIES = 10;

        public LocalLeaderboardService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
            Console.WriteLine("[LocalLeaderboardService] Initialized - using browser LocalStorage");
        }

        public async Task<List<LeaderboardEntry>> GetAllScoresAsync()
        {
            return await GetTopScoresAsync(MAX_ENTRIES);
        }

        public async Task<List<LeaderboardEntry>> GetTopScoresAsync(int count = 10)
        {
            try
            {
                Console.WriteLine($"[LocalLeaderboardService] Getting top {count} scores from LocalStorage");

                var leaderboardJson = await _localStorage.GetItemAsync<string>(LEADERBOARD_KEY);

                if (string.IsNullOrEmpty(leaderboardJson))
                {
                    Console.WriteLine("[LocalLeaderboardService] No leaderboard found in LocalStorage, returning empty list");
                    return new List<LeaderboardEntry>();
                }

                var entries = JsonSerializer.Deserialize<List<LeaderboardEntry>>(leaderboardJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (entries == null)
                {
                    Console.WriteLine("[LocalLeaderboardService] Failed to deserialize leaderboard, returning empty list");
                    return new List<LeaderboardEntry>();
                }

                // Sort by score (descending) then by date (descending) and take the requested count
                var topScores = entries
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.GameDate)
                    .Take(count)
                    .ToList();

                Console.WriteLine($"[LocalLeaderboardService] Retrieved {topScores.Count} scores from LocalStorage");
                return topScores;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalLeaderboardService] Error retrieving leaderboard: {ex.Message}");
                return new List<LeaderboardEntry>();
            }
        }

        public async Task SaveScoreAsync(LeaderboardEntry entry)
        {
            try
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    throw new ArgumentException("Invalid score entry: entry is null or player name is empty");
                }

                Console.WriteLine($"[LocalLeaderboardService] Saving score for {entry.PlayerName}: {entry.Score}");

                // Get current entries
                var currentEntries = await GetTopScoresAsync(MAX_ENTRIES * 2); // Get extra to handle duplicates

                // Add the new entry
                currentEntries.Add(entry);

                // Sort and keep only top entries
                var topEntries = currentEntries
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.GameDate)
                    .Take(MAX_ENTRIES)
                    .ToList();

                // Serialize and save
                var leaderboardJson = JsonSerializer.Serialize(topEntries, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await _localStorage.SetItemAsync(LEADERBOARD_KEY, leaderboardJson);

                Console.WriteLine($"[LocalLeaderboardService] Successfully saved score. Leaderboard now has {topEntries.Count} entries");
                Console.WriteLine($"[LocalLeaderboardService] Player {entry.PlayerName} ranked #{topEntries.FindIndex(x => x.PlayerName == entry.PlayerName && x.Score == entry.Score && x.GameDate == entry.GameDate) + 1}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalLeaderboardService] Error saving score: {ex.Message}");
                throw new Exception($"Failed to save score to local leaderboard: {ex.Message}");
            }
        }

        public async Task ClearLeaderboardAsync()
        {
            try
            {
                Console.WriteLine("[LocalLeaderboardService] Clearing leaderboard");
                await _localStorage.RemoveItemAsync(LEADERBOARD_KEY);
                Console.WriteLine("[LocalLeaderboardService] Leaderboard cleared successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalLeaderboardService] Error clearing leaderboard: {ex.Message}");
                throw new Exception($"Failed to clear leaderboard: {ex.Message}");
            }
        }

        public async Task<int> GetPlayerRankAsync(string playerName, double score)
        {
            try
            {
                var allScores = await GetTopScoresAsync(MAX_ENTRIES);

                // Find the rank of this score
                for (int i = 0; i < allScores.Count; i++)
                {
                    if (allScores[i].Score <= score)
                    {
                        return i + 1; // Rank is 1-based
                    }
                }

                // If score is lower than all existing scores
                return allScores.Count + 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalLeaderboardService] Error getting player rank: {ex.Message}");
                return -1;
            }
        }

        public async Task<bool> IsHighScoreAsync(double score)
        {
            try
            {
                var topScores = await GetTopScoresAsync(MAX_ENTRIES);

                // If leaderboard isn't full, it's definitely a high score
                if (topScores.Count < MAX_ENTRIES)
                {
                    return true;
                }

                // Check if score is higher than the lowest score in top 10
                var lowestScore = topScores.LastOrDefault()?.Score ?? 0;
                return score > lowestScore;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalLeaderboardService] Error checking high score: {ex.Message}");
                return false;
            }
        }
    }
}