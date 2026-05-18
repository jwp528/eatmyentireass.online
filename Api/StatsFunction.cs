using System.Net;
using System.Text.Json;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class StatsFunction
    {
        private readonly ILogger _logger;
        private static readonly SemaphoreSlim FileSemaphore = new(1, 1);

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
                var stats = await ReadStatsFromFile();
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

                await ApplyStatsUpdate(update);

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

        private async Task<GameStats> ReadStatsFromFile()
        {
            await FileSemaphore.WaitAsync();
            try
            {
                var path = GetStatsFilePath();
                if (!File.Exists(path)) return CreateEmptyStats();

                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<GameStats>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? CreateEmptyStats();
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        private async Task ApplyStatsUpdate(GameStatsUpdate update)
        {
            await FileSemaphore.WaitAsync();
            try
            {
                var path = GetStatsFilePath();
                var stats = File.Exists(path)
                    ? JsonSerializer.Deserialize<GameStats>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? CreateEmptyStats()
                    : CreateEmptyStats();

                stats.TotalClicks += update.Clicks;
                stats.RoundsPlayed += 1;
                stats.TotalTimePlayedSeconds += update.DurationSeconds;
                stats.LastUpdated = DateTime.UtcNow;

                foreach (var kvp in update.AssTypeBreakdown)
                {
                    if (stats.AssTypeStats.ContainsKey(kvp.Key))
                        stats.AssTypeStats[kvp.Key] += kvp.Value;
                    else
                        stats.AssTypeStats[kvp.Key] = kvp.Value;
                }

                var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await File.WriteAllTextAsync(path, json);
                _logger.LogInformation($"Stats updated: {stats.RoundsPlayed} rounds, {stats.TotalClicks} total clicks");
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        private static string GetStatsFilePath()
        {
            var dir = AppContext.BaseDirectory;
            // Walk up to find Client/wwwroot/data
            var current = new DirectoryInfo(dir);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "Client", "wwwroot", "data", "stats.json");
                if (File.Exists(candidate)) return candidate;
                current = current.Parent;
            }
            // Fallback: same directory as Api
            return Path.Combine(dir, "..", "Client", "wwwroot", "data", "stats.json");
        }

        private static GameStats CreateEmptyStats() => new GameStats
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

        private HttpResponseData CorsOptions(HttpRequestData req)
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
