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
        private static readonly SemaphoreSlim FileSemaphore = new(1, 1);

        public LeaderboardFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LeaderboardFunction>();
        }

        [Function("GetLeaderboard")]
        public async Task<HttpResponseData> GetLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "leaderboard")] HttpRequestData req)
        {
            // Handle OPTIONS request for CORS preflight
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
                optionsResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                optionsResponse.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
                optionsResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                return optionsResponse;
            }

            _logger.LogInformation("GetLeaderboard function triggered");
            try
            {
                var leaderboard = await GetLeaderboardFromSharedFile();
                var response = req.CreateResponse(HttpStatusCode.OK);

                // Set CORS headers explicitly
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                // Return top 10 scores
                var topScores = leaderboard.Entries
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.GameDate)
                    .Take(10)
                    .ToList();

                _logger.LogInformation($"Returning {topScores.Count} scores from shared leaderboard");
                await response.WriteAsJsonAsync(topScores);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving shared leaderboard");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                await response.WriteStringAsync("Error retrieving leaderboard");
                return response;
            }
        }

        [Function("SaveScore")]
        public async Task<HttpResponseData> SaveScore([HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "leaderboard/save")] HttpRequestData req)
        {
            // Handle OPTIONS request for CORS preflight
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
                optionsResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                optionsResponse.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
                optionsResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                return optionsResponse;
            }

            _logger.LogInformation("SaveScore function triggered - saving to shared leaderboard file");
            try
            {
                // Read the score entry from request body
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Received request body: {requestBody}");

                var entry = JsonSerializer.Deserialize<LeaderboardEntry>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (entry == null || string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    _logger.LogWarning("Invalid score entry received: entry is null or player name is empty");
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    badResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                    await badResponse.WriteStringAsync("Invalid score entry");
                    return badResponse;
                }

                _logger.LogInformation($"Attempting to save score {entry.Score} for player {entry.PlayerName} to shared file");

                // Save directly to Client/wwwroot/data/leaderboard.json
                await SaveScoreToSharedFile(entry);

                var response = req.CreateResponse(HttpStatusCode.OK);
                // Set CORS headers explicitly
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                await response.WriteStringAsync("Score saved to shared leaderboard");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving score to shared leaderboard");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                await response.WriteStringAsync("Error saving score to shared leaderboard");
                return response;
            }
        }

        [Function("ClearLeaderboard")]
        public async Task<HttpResponseData> ClearLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "delete", "options", Route = "leaderboard/clear")] HttpRequestData req)
        {
            // Handle OPTIONS request for CORS preflight
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
                optionsResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                optionsResponse.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
                optionsResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                return optionsResponse;
            }

            _logger.LogInformation("ClearLeaderboard function triggered");
            try
            {
                await ClearLeaderboardFile();

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                await response.WriteStringAsync("Shared leaderboard cleared successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing shared leaderboard");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                await response.WriteStringAsync("Error clearing shared leaderboard");
                return response;
            }
        }

        private async Task<Leaderboard> GetLeaderboardFromSharedFile()
        {
            await FileSemaphore.WaitAsync();
            try
            {
                var filePath = GetSharedLeaderboardPath();
                _logger.LogInformation($"Reading shared leaderboard from: {filePath}");

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Shared leaderboard file does not exist, creating empty leaderboard file");
                    // Create the file if it doesn't exist
                    await CreateEmptyLeaderboardFile();
                }

                var json = await File.ReadAllTextAsync(filePath);
                _logger.LogInformation($"Read {json.Length} characters from shared leaderboard file");

                // Support both formats: direct array or object with entries property
                try
                {
                    var leaderboardData = JsonSerializer.Deserialize<LeaderboardData>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (leaderboardData?.Entries != null)
                    {
                        return new Leaderboard { Entries = leaderboardData.Entries };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse as LeaderboardData format, trying array format");
                    // Try direct array format as fallback
                    try
                    {
                        var entries = JsonSerializer.Deserialize<List<LeaderboardEntry>>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        return new Leaderboard { Entries = entries ?? new List<LeaderboardEntry>() };
                    }
                    catch (JsonException ex2)
                    {
                        _logger.LogError(ex2, "Failed to parse leaderboard file in any format, returning empty leaderboard");
                        return new Leaderboard { Entries = new List<LeaderboardEntry>() };
                    }
                }

                return new Leaderboard { Entries = new List<LeaderboardEntry>() };
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        private async Task SaveScoreToSharedFile(LeaderboardEntry newEntry)
        {
            await FileSemaphore.WaitAsync();
            try
            {
                var leaderboard = await GetLeaderboardFromSharedFileUnsafe();
                leaderboard.Entries.Add(newEntry);

                // Keep top 100 scores but prioritize recent high scores
                var topScores = leaderboard.Entries
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.GameDate)
                    .Take(100)
                    .ToList();

                leaderboard.Entries = topScores;

                var filePath = GetSharedLeaderboardPath();

                // Use the format expected by the client (object with entries property)
                var leaderboardData = new LeaderboardData { Entries = leaderboard.Entries };
                var json = JsonSerializer.Serialize(leaderboardData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation($"Created directory: {directory}");
                }

                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation($"? SHARED LEADERBOARD UPDATED: Saved score {newEntry.Score} for {newEntry.PlayerName}. File now has {leaderboard.Entries.Count} entries at {filePath}");
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        private async Task ClearLeaderboardFile()
        {
            await FileSemaphore.WaitAsync();
            try
            {
                var filePath = GetSharedLeaderboardPath();

                var emptyLeaderboard = new LeaderboardData { Entries = new List<LeaderboardEntry>() };
                var json = JsonSerializer.Serialize(emptyLeaderboard, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation($"? SHARED LEADERBOARD CLEARED: Cleared shared leaderboard at {filePath}");
            }
            finally
            {
                FileSemaphore.Release();
            }
        }

        private async Task CreateEmptyLeaderboardFile()
        {
            var filePath = GetSharedLeaderboardPath();

            var emptyLeaderboard = new LeaderboardData { Entries = new List<LeaderboardEntry>() };
            var json = JsonSerializer.Serialize(emptyLeaderboard, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation($"Created directory: {directory}");
            }

            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation($"Created empty leaderboard file at {filePath}");
        }

        private async Task<Leaderboard> GetLeaderboardFromSharedFileUnsafe()
        {
            var filePath = GetSharedLeaderboardPath();

            if (!File.Exists(filePath))
            {
                await CreateEmptyLeaderboardFile();
                return new Leaderboard { Entries = new List<LeaderboardEntry>() };
            }

            var json = await File.ReadAllTextAsync(filePath);

            // Support both formats
            try
            {
                var leaderboardData = JsonSerializer.Deserialize<LeaderboardData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (leaderboardData?.Entries != null)
                {
                    return new Leaderboard { Entries = leaderboardData.Entries };
                }
            }
            catch (JsonException)
            {
                // Try direct array format as fallback
                try
                {
                    var entries = JsonSerializer.Deserialize<List<LeaderboardEntry>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return new Leaderboard { Entries = entries ?? new List<LeaderboardEntry>() };
                }
                catch (JsonException)
                {
                    // If both formats fail, return empty leaderboard
                    return new Leaderboard { Entries = new List<LeaderboardEntry>() };
                }
            }

            return new Leaderboard { Entries = new List<LeaderboardEntry>() };
        }

        private string GetSharedLeaderboardPath()
        {
            // Try multiple path resolution strategies
            var strategies = new[]
            {
                // Strategy 1: Relative to current directory (standard dev setup)
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../Client/wwwroot/data/leaderboard.json")),
                
                // Strategy 2: Look for solution directory by finding .sln file
                FindSolutionBasedPath(),
                
                // Strategy 3: Direct relative path from Api folder
                Path.GetFullPath(Path.Combine("../Client/wwwroot/data/leaderboard.json")),
                
                // Strategy 4: Absolute path based on current directory structure
                GetAbsolutePathFromCurrentDirectory()
            };

            foreach (var strategy in strategies)
            {
                if (!string.IsNullOrEmpty(strategy))
                {
                    var directory = Path.GetDirectoryName(strategy);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        _logger.LogInformation($"Trying path strategy: {strategy}");

                        // Ensure directory exists
                        if (!Directory.Exists(directory))
                        {
                            try
                            {
                                Directory.CreateDirectory(directory);
                                _logger.LogInformation($"Created directory: {directory}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"Could not create directory: {directory}");
                                continue;
                            }
                        }

                        // Return the first strategy where we can create/access the directory
                        _logger.LogInformation($"Using leaderboard path: {strategy}");
                        return strategy;
                    }
                }
            }

            // Fallback: use a temp directory if all else fails
            var fallbackPath = Path.Combine(Path.GetTempPath(), "leaderboard.json");
            _logger.LogWarning($"All path strategies failed, using fallback: {fallbackPath}");
            return fallbackPath;
        }

        private string FindSolutionBasedPath()
        {
            try
            {
                var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

                // Look up the directory tree for a .sln file
                while (currentDir != null)
                {
                    var solutionFiles = currentDir.GetFiles("*.sln");
                    if (solutionFiles.Length > 0)
                    {
                        // Found solution directory, construct path relative to it
                        var solutionDir = currentDir.FullName;
                        var leaderboardPath = Path.Combine(solutionDir, "Client", "wwwroot", "data", "leaderboard.json");
                        _logger.LogInformation($"Found solution at {solutionDir}, leaderboard path: {leaderboardPath}");
                        return leaderboardPath;
                    }
                    currentDir = currentDir.Parent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while searching for solution directory");
            }

            return null;
        }

        private string GetAbsolutePathFromCurrentDirectory()
        {
            try
            {
                var currentDir = Directory.GetCurrentDirectory();
                _logger.LogInformation($"Current directory: {currentDir}");

                // Check if we're in the Api folder
                if (currentDir.EndsWith("Api") || currentDir.EndsWith("Api/") || currentDir.EndsWith("Api\\"))
                {
                    var parentDir = Directory.GetParent(currentDir)?.FullName;
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        return Path.Combine(parentDir, "Client", "wwwroot", "data", "leaderboard.json");
                    }
                }

                // Check if we're in the solution root
                if (Directory.Exists(Path.Combine(currentDir, "Client")) &&
                    Directory.Exists(Path.Combine(currentDir, "Api")))
                {
                    return Path.Combine(currentDir, "Client", "wwwroot", "data", "leaderboard.json");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while determining absolute path from current directory");
            }

            return null;
        }

        // Helper class to match the JSON structure expected by client
        private class LeaderboardData
        {
            public List<LeaderboardEntry> Entries { get; set; } = new();
        }
    }
}