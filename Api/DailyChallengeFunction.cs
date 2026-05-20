using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class DailyChallengeFunction
    {
        private readonly ILogger _logger;
        private const string TableName = "dailyleaderboard";

        private static readonly Lazy<TableClient> _tableClient = new(() =>
        {
            var conn = Environment.GetEnvironmentVariable("AzureStorageConnection")
                ?? "UseDevelopmentStorage=true";
            var client = new TableClient(conn, TableName);
            client.CreateIfNotExists();
            return client;
        });

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
                var table = _tableClient.Value;
                var entries = new List<DailyLeaderboardEntry>();

                await foreach (var entity in table.QueryAsync<TableEntity>(
                    filter: $"PartitionKey eq '{todayKey}'", maxPerPage: 100))
                {
                    entries.Add(EntityToEntry(entity));
                }

                var top10 = entries
                    .OrderByDescending(e => e.Score)
                    .ThenByDescending(e => e.GameDate)
                    .Take(10)
                    .ToList();

                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteAsJsonAsync(new { date = todayKey, entries = top10 });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily leaderboard");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error retrieving daily leaderboard");
                return error;
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
                var entry = JsonSerializer.Deserialize<DailyLeaderboardEntry>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    AddCorsHeaders(bad);
                    await bad.WriteStringAsync("Invalid entry");
                    return bad;
                }

                if (string.IsNullOrWhiteSpace(entry.ChallengeDate))
                    entry.ChallengeDate = DailyChallenge.GetChallengeDateKey(DateOnly.FromDateTime(DateTime.UtcNow));

                if (entry.GameDate == default)
                    entry.GameDate = DateTime.UtcNow;

                var table = _tableClient.Value;
                await table.AddEntityAsync(EntryToEntity(entry, Guid.NewGuid().ToString()));

                _logger.LogInformation("Daily score saved: {Score} for {Player} on {Date}", entry.Score, entry.PlayerName, entry.ChallengeDate);
                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteStringAsync("Daily score saved");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving daily score");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error saving daily score");
                return error;
            }
        }

        private static TableEntity EntryToEntity(DailyLeaderboardEntry entry, string rowKey) =>
            new(entry.ChallengeDate, rowKey)
            {
                ["PlayerName"] = entry.PlayerName,
                ["Score"] = entry.Score,
                ["ChallengeDate"] = entry.ChallengeDate,
                ["GameDate"] = new DateTimeOffset(entry.GameDate, TimeSpan.Zero)
            };

        private static DailyLeaderboardEntry EntityToEntry(TableEntity entity) => new()
        {
            PlayerName = entity.GetString("PlayerName") ?? "",
            Score = entity.GetDouble("Score") ?? 0,
            ChallengeDate = entity.GetString("ChallengeDate") ?? entity.PartitionKey,
            GameDate = entity.GetDateTimeOffset("GameDate")?.UtcDateTime ?? DateTime.UtcNow
        };

        private static HttpResponseData CorsOptions(HttpRequestData req)
        {
            var res = req.CreateResponse(HttpStatusCode.OK);
            AddCorsHeaders(res);
            return res;
        }

        private static void AddCorsHeaders(HttpResponseData response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
        }
    }

    public class DailyLeaderboardEntry
    {
        public string PlayerName { get; set; } = "";
        public double Score { get; set; }
        public string ChallengeDate { get; set; } = "";
        public DateTime GameDate { get; set; }
    }
}
