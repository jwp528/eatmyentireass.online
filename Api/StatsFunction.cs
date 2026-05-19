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

        private string GetStatsFilePath()
        {
            // Use the same multi-strategy path resolution as LeaderboardFunction
            var strategies = new[]
            {
                // Strategy 1: Relative to current working directory (standard dev setup — func start from Api/)
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../Client/wwwroot/data/stats.json")),

                // Strategy 2: Walk up directory tree looking for solution root
                FindSolutionBasedStatsPath(),

                // Strategy 3: Direct relative path
                Path.GetFullPath(Path.Combine("../Client/wwwroot/data/stats.json")),
            };

            foreach (var strategy in strategies)
            {
                if (string.IsNullOrEmpty(strategy)) continue;
                var directory = Path.GetDirectoryName(strategy);
                if (string.IsNullOrEmpty(directory)) continue;

                _logger.LogInformation($"[Stats] Trying path strategy: {strategy}");
                if (!Directory.Exists(directory))
                {
                    try { Directory.CreateDirectory(directory); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"[Stats] Could not create directory: {directory}");
                        continue;
                    }
                }
                _logger.LogInformation($"[Stats] Using stats path: {strategy}");
                return strategy;
            }

            var fallback = Path.Combine(Path.GetTempPath(), "stats.json");
            _logger.LogWarning($"[Stats] All path strategies failed, using fallback: {fallback}");
            return fallback;
        }

        private string? FindSolutionBasedStatsPath()
        {
            try
            {
                var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (currentDir != null)
                {
                    if (currentDir.GetFiles("*.sln").Length > 0 || currentDir.GetFiles("*.slnx").Length > 0)
                    {
                        var path = Path.Combine(currentDir.FullName, "Client", "wwwroot", "data", "stats.json");
                        _logger.LogInformation($"[Stats] Found solution at {currentDir.FullName}, stats path: {path}");
                        return path;
                    }
                    currentDir = currentDir.Parent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Stats] Error searching for solution directory");
            }
            return null;
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
