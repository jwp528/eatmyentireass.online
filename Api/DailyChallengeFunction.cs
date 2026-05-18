using System.Net;
using System.Text.Json;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class DailyChallengeFunction
    {
        private readonly ILogger _logger;
        private static readonly SemaphoreSlim FileSemaphore = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public DailyChallengeFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DailyChallengeFunction>();
        }

        [Function("GetDailyLeaderboard")]
        public async Task<HttpResponseData> GetDailyLeaderboard(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "daily")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            _logger.LogInformation("GetDailyLeaderboard triggered");
            try
            {
                var todayKey = DailyChallenge.GetChallengeDateKey(DateOnly.FromDateTime(DateTime.UtcNow));
                var leaderboard = await ReadDailyLeaderboardUnsafe();

                var todayEntries = leaderboard
                    .Where(e => e.ChallengeDate == todayKey)
                    .OrderByDescending(e => e.Score)
                    .ThenByDescending(e => e.GameDate)
                    .Take(10)
                    .ToList();

                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteAsJsonAsync(new { date = todayKey, entries = todayEntries });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily leaderboard");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(response);
                await response.WriteStringAsync("Error retrieving daily leaderboard");
                return response;
            }
        }

        [Function("SaveDailyScore")]
        public async Task<HttpResponseData> SaveDailyScore(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "daily/save")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            _logger.LogInformation("SaveDailyScore triggered");
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var entry = JsonSerializer.Deserialize<DailyLeaderboardEntry>(body, ReadOptions);

                if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    AddCorsHeaders(bad);
                    await bad.WriteStringAsync("Invalid entry");
                    return bad;
                }

                // Stamp the challenge date if not provided
                if (string.IsNullOrWhiteSpace(entry.ChallengeDate))
                    entry.ChallengeDate = DailyChallenge.GetChallengeDateKey(DateOnly.FromDateTime(DateTime.UtcNow));

                if (entry.GameDate == default)
                    entry.GameDate = DateTime.UtcNow;

                await SaveDailyEntry(entry);

                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteStringAsync("Daily score saved");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving daily score");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(response);
                await response.WriteStringAsync("Error saving daily score");
                return response;
            }
        }

        // ── file helpers ────────────────────────────────────────────────────────

        private async Task<List<DailyLeaderboardEntry>> ReadDailyLeaderboardUnsafe()
        {
            var path = GetDailyLeaderboardPath();
            if (!File.Exists(path))
            {
                await WriteDailyLeaderboard(new List<DailyLeaderboardEntry>(), path);
                return new List<DailyLeaderboardEntry>();
            }

            var json = await File.ReadAllTextAsync(path);
            try
            {
                var data = JsonSerializer.Deserialize<DailyLeaderboardData>(json, ReadOptions);
                return data?.Entries ?? new List<DailyLeaderboardEntry>();
            }
            catch
            {
                return new List<DailyLeaderboardEntry>();
            }
        }

        private async Task SaveDailyEntry(DailyLeaderboardEntry entry)
        {
            await FileSemaphore.WaitAsync();
            try
            {
                var path = GetDailyLeaderboardPath();
                var entries = await ReadDailyLeaderboardUnsafe();
                entries.Add(entry);

                // Retain only the last 30 days of entries, capped at 1000 total
                var cutoff = DailyChallenge.GetChallengeDateKey(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
                entries = entries
                    .Where(e => string.Compare(e.ChallengeDate, cutoff, StringComparison.Ordinal) >= 0)
                    .OrderByDescending(e => e.ChallengeDate)
                    .ThenByDescending(e => e.Score)
                    .Take(1000)
                    .ToList();

                await WriteDailyLeaderboard(entries, path);
                _logger.LogInformation($"Daily score saved: {entry.Score} for {entry.PlayerName} on {entry.ChallengeDate}");
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        private async Task WriteDailyLeaderboard(List<DailyLeaderboardEntry> entries, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(new DailyLeaderboardData { Entries = entries }, JsonOptions);
            await File.WriteAllTextAsync(path, json);
        }

        private string GetDailyLeaderboardPath()
        {
            // Walk up from current directory to find solution root then locate the data file
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../Client/wwwroot/data/daily_leaderboard.json")),
                FindSolutionBasedPath(),
                Path.GetFullPath(Path.Combine("../Client/wwwroot/data/daily_leaderboard.json")),
            };

            foreach (var path in candidates)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir)) continue;

                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); }
                    catch { continue; }
                }

                _logger.LogInformation($"Using daily leaderboard path: {path}");
                return path;
            }

            var fallback = Path.Combine(Path.GetTempPath(), "daily_leaderboard.json");
            _logger.LogWarning($"Falling back to temp path: {fallback}");
            return fallback;
        }

        private string FindSolutionBasedPath()
        {
            try
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null)
                {
                    if (dir.GetFiles("*.sln").Length > 0 || dir.GetFiles("*.slnx").Length > 0)
                        return Path.Combine(dir.FullName, "Client", "wwwroot", "data", "daily_leaderboard.json");
                    dir = dir.Parent;
                }
            }
            catch { /* ignored */ }
            return null;
        }

        // ── CORS helpers ─────────────────────────────────────────────────────────

        private static HttpResponseData CorsOptions(HttpRequestData req)
        {
            var r = req.CreateResponse(HttpStatusCode.OK);
            AddCorsHeaders(r);
            return r;
        }

        private static void AddCorsHeaders(HttpResponseData response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
        }
    }

    // ── inline DTOs ──────────────────────────────────────────────────────────────

    public class DailyLeaderboardEntry
    {
        public string PlayerName { get; set; } = "";
        public double Score { get; set; }
        public string ChallengeDate { get; set; } = "";
        public DateTime GameDate { get; set; }
    }

    public class DailyLeaderboardData
    {
        public List<DailyLeaderboardEntry> Entries { get; set; } = new();
    }
}
