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
    public class StatsFunction
    {
        private readonly ILogger _logger;
        private const string TableName = "gamestats";
        private const string PartitionKey = "global";
        private const string RowKey = "totals";

        private static readonly Lazy<TableClient> _tableClient = new(() =>
        {
            var conn = Environment.GetEnvironmentVariable("AzureStorageConnection")
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                ?? "UseDevelopmentStorage=true";
            return new TableClient(conn, TableName);
        });

        private static volatile bool _tableEnsured = false;

        private static async Task<TableClient> GetTableAsync()
        {
            var client = _tableClient.Value;
            if (!_tableEnsured)
            {
                await client.CreateIfNotExistsAsync();
                _tableEnsured = true;
            }
            return client;
        }

        public StatsFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<StatsFunction>();
        }

        [Function("GetStats")]
        public async Task<HttpResponseData> GetStats(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "stats")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            _logger.LogInformation("GetStats triggered");
            try
            {
                var stats = await ReadStatsAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteAsJsonAsync(stats);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading stats");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error reading stats");
                return error;
            }
        }

        [Function("UpdateStats")]
        public async Task<HttpResponseData> UpdateStats(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "stats/update")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            _logger.LogInformation("UpdateStats triggered");
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var update = JsonSerializer.Deserialize<GameStatsUpdate>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (update == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    AddCorsHeaders(bad);
                    await bad.WriteStringAsync("Invalid stats update");
                    return bad;
                }

                await ApplyStatsUpdateAsync(update);

                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteStringAsync("Stats updated");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stats");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error updating stats");
                return error;
            }
        }

        private async Task<GameStats> ReadStatsAsync()
        {
            var table = await GetTableAsync();
            try
            {
                var resp = await table.GetEntityAsync<TableEntity>(PartitionKey, RowKey);
                return EntityToStats(resp.Value);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return CreateEmptyStats();
            }
        }

        private async Task ApplyStatsUpdateAsync(GameStatsUpdate update)
        {
            var table = await GetTableAsync();

            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    TableEntity entity;
                    Azure.ETag etag;
                    var isNew = false;

                    try
                    {
                        var resp = await table.GetEntityAsync<TableEntity>(PartitionKey, RowKey);
                        entity = resp.Value;
                        etag = entity.ETag;
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                        entity = new TableEntity(PartitionKey, RowKey);
                        etag = Azure.ETag.All;
                        isNew = true;
                    }

                    entity["TotalClicks"] = (entity.GetInt64("TotalClicks") ?? 0) + update.Clicks;
                    entity["RoundsPlayed"] = (entity.GetInt64("RoundsPlayed") ?? 0) + 1;
                    entity["TotalTimePlayedSeconds"] = (entity.GetInt64("TotalTimePlayedSeconds") ?? 0) + update.DurationSeconds;
                    entity["LastUpdated"] = DateTimeOffset.UtcNow;

                    foreach (var kvp in update.AssTypeBreakdown)
                        entity[$"Stat_{kvp.Key}"] = (entity.GetInt64($"Stat_{kvp.Key}") ?? 0) + kvp.Value;

                    if (isNew)
                        await table.AddEntityAsync(entity);
                    else
                        await table.UpdateEntityAsync(entity, etag, TableUpdateMode.Replace);

                    _logger.LogInformation("Stats updated: +{Clicks} clicks, +1 round", update.Clicks);
                    return;
                }
                catch (RequestFailedException ex) when (ex.Status == 412 && attempt == 0)
                {
                    _logger.LogWarning("Stats update conflict (412), retrying...");
                }
            }

            throw new Exception("Failed to update stats after 2 attempts");
        }

        private static GameStats EntityToStats(TableEntity entity) => new()
        {
            TotalClicks = entity.GetInt64("TotalClicks") ?? 0,
            RoundsPlayed = entity.GetInt64("RoundsPlayed") ?? 0,
            TotalTimePlayedSeconds = entity.GetInt64("TotalTimePlayedSeconds") ?? 0,
            LastUpdated = entity.GetDateTimeOffset("LastUpdated")?.UtcDateTime ?? DateTime.UtcNow,
            AssTypeStats = new Dictionary<string, long>
            {
                ["Boney"] = entity.GetInt64("Stat_Boney") ?? 0,
                ["Cartoon"] = entity.GetInt64("Stat_Cartoon") ?? 0,
                ["Flat"] = entity.GetInt64("Stat_Flat") ?? 0,
                ["Golden"] = entity.GetInt64("Stat_Golden") ?? 0,
                ["GYAT"] = entity.GetInt64("Stat_GYAT") ?? 0,
                ["Hairy"] = entity.GetInt64("Stat_Hairy") ?? 0
            }
        };

        private static GameStats CreateEmptyStats() => new()
        {
            AssTypeStats = new Dictionary<string, long>
            {
                ["Boney"] = 0,
                ["Cartoon"] = 0,
                ["Flat"] = 0,
                ["Golden"] = 0,
                ["GYAT"] = 0,
                ["Hairy"] = 0
            }
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
}
