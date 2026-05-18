using System.Net.Http.Json;

namespace BlazorApp.Client.Services
{
    public record DailyEntry(string PlayerName, double Score, string ChallengeDate, DateTime GameDate);

    public record DailyLeaderboardResult(string Date, List<DailyEntry> Entries);

    public interface IDailyChallengeService
    {
        Task<DailyLeaderboardResult?> GetTodayLeaderboardAsync();
        Task SaveScoreAsync(string playerName, double score);
    }

    public class DailyChallengeService : IDailyChallengeService
    {
        private readonly HttpClient _http;

        public DailyChallengeService(HttpClient http)
        {
            _http = http;
        }

        public async Task<DailyLeaderboardResult?> GetTodayLeaderboardAsync()
        {
            try
            {
                return await _http.GetFromJsonAsync<DailyLeaderboardResult>("api/daily");
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveScoreAsync(string playerName, double score)
        {
            try
            {
                var entry = new
                {
                    playerName,
                    score,
                    challengeDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    gameDate = DateTime.UtcNow
                };
                await _http.PostAsJsonAsync("api/daily/save", entry);
            }
            catch { /* fire and forget */ }
        }
    }
}
