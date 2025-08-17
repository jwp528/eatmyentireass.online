using System.Net;
using System.Text.Json;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class LeaderboardFunction
    {
        private readonly ILogger _logger;
        private const string LEADERBOARD_FILE = "leaderboard.json";
        private static readonly SemaphoreSlim FileSemaphore = new(1, 1);

        public LeaderboardFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LeaderboardFunction>();
        }

        [Function("GetLeaderboard")]
        public async Task<HttpResponseData> GetLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "leaderboard")] HttpRequestData req)
        {
            try
            {
                var leaderboard = await GetLeaderboardFromFile();
                var response = req.CreateResponse(HttpStatusCode.OK);
                
                // Return top 10 scores
                var topScores = leaderboard.Entries
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.GameDate)
                    .Take(10)
                    .ToList();
                
                await response.WriteAsJsonAsync(topScores);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboard");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Error retrieving leaderboard");
                return response;
            }
        }

        [Function("SaveScore")]
        public async Task<HttpResponseData> SaveScore([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "leaderboard")] HttpRequestData req)
        {
            try
            {
                // Read the score entry from request body
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var entry = JsonSerializer.Deserialize<LeaderboardEntry>(requestBody);
                
                if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid score entry");
                    return badResponse;
                }

                // Save to leaderboard (this will handle top 10 logic)
                await SaveScoreToFile(entry);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Score saved successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving score to leaderboard");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Error saving score");
                return response;
            }
        }

        private async Task<Leaderboard> GetLeaderboardFromFile()
        {
            await FileSemaphore.WaitAsync();
            try
            {
                var filePath = Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? "/tmp", LEADERBOARD_FILE);
                
                if (!File.Exists(filePath))
                {
                    return new Leaderboard { Entries = new List<LeaderboardEntry>() };
                }

                var json = await File.ReadAllTextAsync(filePath);
                var leaderboard = JsonSerializer.Deserialize<Leaderboard>(json);
                return leaderboard ?? new Leaderboard { Entries = new List<LeaderboardEntry>() };
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        private async Task SaveScoreToFile(LeaderboardEntry newEntry)
        {
            await FileSemaphore.WaitAsync();
            try
            {
                var leaderboard = await GetLeaderboardFromFileUnsafe();
                leaderboard.Entries.Add(newEntry);
                
                // Keep only top 10 scores
                var topScores = leaderboard.Entries
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.GameDate)
                    .Take(10)
                    .ToList();

                leaderboard.Entries = topScores;

                var filePath = Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? "/tmp", LEADERBOARD_FILE);
                var json = JsonSerializer.Serialize(leaderboard, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                
                _logger.LogInformation($"Saved score {newEntry.Score} for {newEntry.PlayerName}. Leaderboard now has {leaderboard.Entries.Count} entries");
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        private async Task<Leaderboard> GetLeaderboardFromFileUnsafe()
        {
            var filePath = Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? "/tmp", LEADERBOARD_FILE);
            
            if (!File.Exists(filePath))
            {
                return new Leaderboard { Entries = new List<LeaderboardEntry>() };
            }

            var json = await File.ReadAllTextAsync(filePath);
            var leaderboard = JsonSerializer.Deserialize<Leaderboard>(json);
            return leaderboard ?? new Leaderboard { Entries = new List<LeaderboardEntry>() };
        }
    }
}