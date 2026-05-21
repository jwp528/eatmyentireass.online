using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class LeaderboardFunction
    {
        private readonly ILogger _logger;
        private const string TableName = "leaderboard";

        private static readonly Lazy<TableClient> _leaderboardTableClient = new(() =>
        {
            var conn = Environment.GetEnvironmentVariable("AzureStorageConnection")
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                ?? "UseDevelopmentStorage=true";
            return new TableClient(conn, TableName);
        });

        private static readonly Lazy<TableClient> _playersTableClient = new(() =>
        {
            var conn = Environment.GetEnvironmentVariable("AzureStorageConnection")
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                ?? "UseDevelopmentStorage=true";
            return new TableClient(conn, "players");
        });

        private static volatile bool _tableEnsured = false;

        private static async Task<TableClient> GetTableAsync()
        {
            var client = _leaderboardTableClient.Value;
            if (!_tableEnsured)
            {
                await client.CreateIfNotExistsAsync();
                _tableEnsured = true;
            }
            return client;
        }

        public LeaderboardFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LeaderboardFunction>();
        }

        [Function("GetLeaderboard")]
        public async Task<HttpResponseData> GetLeaderboard(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "leaderboard")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            var period = GetQueryParam(req.Url, "period", "alltime");
            var partitionKey = BuildPartitionKey(period);
            _logger.LogInformation("GetLeaderboard: period={Period} pk={PK}", period, partitionKey);

            try
            {
                var table = await GetTableAsync();
                var entries = new List<LeaderboardEntry>();
                await foreach (var entity in table.QueryAsync<TableEntity>(
                    filter: $"PartitionKey eq '{partitionKey}'", maxPerPage: 200))
                {
                    entries.Add(EntityToEntry(entity));
                }

                // Deduplicate by player name (handles legacy multi-row entries), take top 100
                var top100 = entries
                    .GroupBy(e => PlayerNameHelper.Normalize(e.PlayerName))
                    .Select(g => g.OrderByDescending(e => e.Score).First())
                    .OrderByDescending(e => e.Score)
                    .ThenByDescending(e => e.GameDate)
                    .Take(100)
                    .ToList();

                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteAsJsonAsync(top100);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboard");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error retrieving leaderboard");
                return error;
            }
        }

        [Function("SaveScore")]
        public async Task<HttpResponseData> SaveScore(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "leaderboard/save")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            _logger.LogInformation("SaveScore triggered");
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<SaveScoreRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var entry = request?.Entry;
                if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    AddCorsHeaders(bad);
                    await bad.WriteStringAsync("Invalid score entry");
                    return bad;
                }

                if (entry.GameDate == default)
                    entry.GameDate = DateTime.UtcNow;

                // Auth check — fail closed if player table is unreachable
                var normalizedName = PlayerNameHelper.Normalize(entry.PlayerName);
                var playersTable = _playersTableClient.Value;
                await playersTable.CreateIfNotExistsAsync();

                var authResult = await PlayerFunction.VerifyTokenAsync(playersTable, normalizedName, request?.AuthToken);
                switch (authResult)
                {
                    case TokenVerifyResult.TokenRequired:
                    case TokenVerifyResult.Invalid:
                        {
                            var denied = req.CreateResponse(HttpStatusCode.Unauthorized);
                            AddCorsHeaders(denied);
                            await denied.WriteStringAsync("Name is claimed. Verify identity first.");
                            return denied;
                        }
                    case TokenVerifyResult.Error:
                        {
                            _logger.LogWarning("Player auth lookup failed for {Name} — rejecting save", normalizedName);
                            var svcErr = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                            AddCorsHeaders(svcErr);
                            await svcErr.WriteStringAsync("Unable to verify identity. Try again later.");
                            return svcErr;
                        }
                }

                var table = await GetTableAsync();
                var now = DateTime.UtcNow;
                var periods = new[]
                {
                    "alltime",
                    $"daily:{now:yyyy-MM-dd}",
                    $"monthly:{now:yyyy-MM}",
                    $"yearly:{now:yyyy}"
                };

                foreach (var p in periods)
                {
                    // Only save if this is the player's best score for the period
                    try
                    {
                        var existingResponse = await table.GetEntityAsync<TableEntity>(p, normalizedName);
                        var existingScore = existingResponse.Value.GetDouble("Score") ?? 0;
                        if (entry.Score <= existingScore)
                        {
                            _logger.LogInformation("Score {Score} for {Player} in {Period} not higher than {Existing}, skipping",
                                entry.Score, normalizedName, p, existingScore);
                            continue;
                        }
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                        // No existing entry — proceed
                    }

                    await table.UpsertEntityAsync(EntryToEntity(entry, p, normalizedName));
                }

                _logger.LogInformation("Score saved: {Score} for {Player}", entry.Score, entry.PlayerName);

                // Update per-player stats for claimed names (fire-and-forget, non-critical)
                if (authResult == TokenVerifyResult.Valid)
                    _ = PlayerFunction.UpdatePlayerStatsAsync(playersTable, normalizedName, entry);

                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteStringAsync("Score saved");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving score");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error saving score");
                return error;
            }
        }

        [Function("GetPlayerBest")]
        public async Task<HttpResponseData> GetPlayerBest(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "leaderboard/player")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            var name = GetQueryParam(req.Url, "name", string.Empty);
            if (string.IsNullOrWhiteSpace(name))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                AddCorsHeaders(bad);
                await bad.WriteStringAsync("name is required");
                return bad;
            }

            try
            {
                var table = await GetTableAsync();
                var normalized = PlayerNameHelper.Normalize(name);
                LeaderboardEntry? best = null;

                await foreach (var entity in table.QueryAsync<TableEntity>(
                    filter: $"PartitionKey eq 'alltime'", maxPerPage: 500))
                {
                    var entryName = PlayerNameHelper.Normalize(entity.GetString("PlayerName") ?? "");
                    if (entryName != normalized) continue;

                    var entry = EntityToEntry(entity);
                    if (best == null || entry.Score > best.Score)
                        best = entry;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteAsJsonAsync(best);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player best for {Name}", name);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error getting player best");
                return error;
            }
        }

        [Function("ClearLeaderboard")]
        public async Task<HttpResponseData> ClearLeaderboard(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", "options", Route = "leaderboard/clear")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            _logger.LogInformation("ClearLeaderboard triggered");
            try
            {
                var table = await GetTableAsync();
                var deleted = 0;
                await foreach (var entity in table.QueryAsync<TableEntity>(select: new[] { "PartitionKey", "RowKey" }))
                {
                    await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
                    deleted++;
                }

                _logger.LogInformation("Cleared {Count} leaderboard entities", deleted);
                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteStringAsync($"Cleared {deleted} entries");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing leaderboard");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error clearing leaderboard");
                return error;
            }
        }

        private static string BuildPartitionKey(string period) => period switch
        {
            "daily" => $"daily:{DateTime.UtcNow:yyyy-MM-dd}",
            "monthly" => $"monthly:{DateTime.UtcNow:yyyy-MM}",
            "yearly" => $"yearly:{DateTime.UtcNow:yyyy}",
            _ => "alltime"
        };

        private static TableEntity EntryToEntity(LeaderboardEntry entry, string partitionKey, string rowKey) =>
            new(partitionKey, rowKey)
            {
                ["PlayerName"] = entry.PlayerName,
                ["Score"] = entry.Score,
                ["TotalClicks"] = entry.TotalClicks,
                ["GameDate"] = new DateTimeOffset(DateTime.SpecifyKind(
                    entry.GameDate == default ? DateTime.UtcNow : entry.GameDate, DateTimeKind.Utc)),
                ["GameDurationSeconds"] = entry.GameDurationSeconds,
                ["AssTypeBreakdown"] = JsonSerializer.Serialize(entry.AssTypeBreakdown)
            };

        private static LeaderboardEntry EntityToEntry(TableEntity entity)
        {
            var breakdownJson = entity.GetString("AssTypeBreakdown") ?? "{}";
            Dictionary<string, int> breakdown;
            try { breakdown = JsonSerializer.Deserialize<Dictionary<string, int>>(breakdownJson) ?? new(); }
            catch { breakdown = new(); }

            return new LeaderboardEntry
            {
                PlayerName = entity.GetString("PlayerName") ?? "",
                Score = entity.GetDouble("Score") ?? 0,
                TotalClicks = entity.GetInt32("TotalClicks") ?? 0,
                GameDate = entity.GetDateTimeOffset("GameDate")?.UtcDateTime ?? DateTime.UtcNow,
                GameDurationSeconds = entity.GetInt32("GameDurationSeconds") ?? 60,
                AssTypeBreakdown = breakdown
            };
        }

        private static string GetQueryParam(Uri url, string name, string defaultValue = "")
        {
            var query = url.Query.TrimStart('?');
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == name)
                    return Uri.UnescapeDataString(parts[1]);
            }
            return defaultValue;
        }

        private static HttpResponseData CorsOptions(HttpRequestData req)
        {
            var res = req.CreateResponse(HttpStatusCode.OK);
            AddCorsHeaders(res);
            return res;
        }

        private static void AddCorsHeaders(HttpResponseData response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
        }
    }
}
