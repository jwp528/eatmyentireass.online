using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Data.Tables;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class PlayerFunction
    {
        private readonly ILogger _logger;
        private const string TableName = "players";
        private const int Pbkdf2Iterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        // In-memory rate limiter: key = normalizedName, value = (attempts, windowStart)
        private static readonly ConcurrentDictionary<string, (int Count, DateTime Window)> _rateLimit = new();
        private const int MaxAttemptsPerWindow = 5;
        private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(10);

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

        public PlayerFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PlayerFunction>();
        }

        [Function("GetPlayerStatus")]
        public async Task<HttpResponseData> GetPlayerStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "player/status")] HttpRequestData req)
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
                var normalized = PlayerNameHelper.Normalize(name);
                var table = await GetTableAsync();
                var entity = await TryGetPlayerAsync(table, normalized);

                var result = new PlayerStatusResponse { IsClaimed = entity != null };
                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteAsJsonAsync(result);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking player status for {Name}", name);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error checking status");
                return error;
            }
        }

        [Function("ClaimPlayerName")]
        public async Task<HttpResponseData> ClaimPlayerName(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "player/claim")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            PlayerClaimRequest? body;
            try
            {
                var json = await new StreamReader(req.Body).ReadToEndAsync();
                body = JsonSerializer.Deserialize<PlayerClaimRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                AddCorsHeaders(bad);
                await bad.WriteStringAsync("Invalid request body");
                return bad;
            }

            if (body == null || string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.Password))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                AddCorsHeaders(bad);
                await bad.WriteStringAsync("Name and password are required");
                return bad;
            }

            if (body.Password.Length < 4)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                AddCorsHeaders(bad);
                await bad.WriteStringAsync("Password must be at least 4 characters");
                return bad;
            }

            var normalized = PlayerNameHelper.Normalize(body.Name);

            if (IsRateLimited(normalized))
            {
                var tooMany = req.CreateResponse(HttpStatusCode.TooManyRequests);
                AddCorsHeaders(tooMany);
                await tooMany.WriteStringAsync("Too many attempts. Try again later.");
                return tooMany;
            }

            RecordAttempt(normalized);

            try
            {
                var table = await GetTableAsync();
                var existing = await TryGetPlayerAsync(table, normalized);

                if (existing != null)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    AddCorsHeaders(conflict);
                    await conflict.WriteStringAsync("Name is already claimed");
                    return conflict;
                }

                var salt = RandomNumberGenerator.GetBytes(SaltSize);
                var hash = HashPassword(body.Password, salt);
                var token = GenerateToken();
                var tokenHash = HashToken(token);

                var entity = new TableEntity("player", normalized)
                {
                    ["DisplayName"] = body.Name.Trim(),
                    ["PasswordHash"] = Convert.ToBase64String(hash),
                    ["Salt"] = Convert.ToBase64String(salt),
                    ["TokenHash"] = tokenHash,
                    ["ClaimedAt"] = DateTimeOffset.UtcNow
                };

                await table.AddEntityAsync(entity);
                _logger.LogInformation("Name claimed: {Name}", normalized);

                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteAsJsonAsync(new PlayerAuthResponse { Token = token });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error claiming name {Name}", normalized);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error claiming name");
                return error;
            }
        }

        [Function("VerifyPlayerName")]
        public async Task<HttpResponseData> VerifyPlayerName(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "player/verify")] HttpRequestData req)
        {
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                return CorsOptions(req);

            PlayerClaimRequest? body;
            try
            {
                var json = await new StreamReader(req.Body).ReadToEndAsync();
                body = JsonSerializer.Deserialize<PlayerClaimRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                AddCorsHeaders(bad);
                await bad.WriteStringAsync("Invalid request body");
                return bad;
            }

            if (body == null || string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.Password))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                AddCorsHeaders(bad);
                await bad.WriteStringAsync("Name and password are required");
                return bad;
            }

            var normalized = PlayerNameHelper.Normalize(body.Name);

            if (IsRateLimited(normalized))
            {
                var tooMany = req.CreateResponse(HttpStatusCode.TooManyRequests);
                AddCorsHeaders(tooMany);
                await tooMany.WriteStringAsync("Too many attempts. Try again later.");
                return tooMany;
            }

            RecordAttempt(normalized);

            try
            {
                var table = await GetTableAsync();
                var entity = await TryGetPlayerAsync(table, normalized);

                if (entity == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    AddCorsHeaders(notFound);
                    await notFound.WriteStringAsync("Name is not claimed");
                    return notFound;
                }

                var salt = Convert.FromBase64String(entity.GetString("Salt") ?? throw new Exception("Missing salt"));
                var storedHash = Convert.FromBase64String(entity.GetString("PasswordHash") ?? throw new Exception("Missing hash"));
                var computedHash = HashPassword(body.Password, salt);

                if (!CryptographicOperations.FixedTimeEquals(storedHash, computedHash))
                {
                    var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                    AddCorsHeaders(unauthorized);
                    await unauthorized.WriteStringAsync("Incorrect password");
                    return unauthorized;
                }

                // Rotate token on successful verify
                var token = GenerateToken();
                var tokenHash = HashToken(token);
                entity["TokenHash"] = tokenHash;
                await table.UpdateEntityAsync(entity, entity.ETag);

                _logger.LogInformation("Name verified: {Name}", normalized);
                var response = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(response);
                await response.WriteAsJsonAsync(new PlayerAuthResponse { Token = token });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying name {Name}", normalized);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(error);
                await error.WriteStringAsync("Error verifying name");
                return error;
            }
        }

        // Called by LeaderboardFunction to verify a claimed name's token before saving
        public static async Task<TokenVerifyResult> VerifyTokenAsync(TableClient table, string normalizedName, string? token)
        {
            try
            {
                var entity = await TryGetPlayerAsync(table, normalizedName);
                if (entity == null)
                    return TokenVerifyResult.NameFree;

                if (string.IsNullOrWhiteSpace(token))
                    return TokenVerifyResult.TokenRequired;

                var storedHash = entity.GetString("TokenHash");
                if (string.IsNullOrWhiteSpace(storedHash))
                    return TokenVerifyResult.TokenRequired;

                var computedHash = HashToken(token);
                return computedHash == storedHash
                    ? TokenVerifyResult.Valid
                    : TokenVerifyResult.Invalid;
            }
            catch
            {
                return TokenVerifyResult.Error;
            }
        }

        private static async Task<TableEntity?> TryGetPlayerAsync(TableClient table, string normalizedName)
        {
            try
            {
                var response = await table.GetEntityAsync<TableEntity>("player", normalizedName);
                return response.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        private static byte[] HashPassword(string password, byte[] salt) =>
            Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        private static bool IsRateLimited(string key)
        {
            if (_rateLimit.TryGetValue(key, out var state))
            {
                if (DateTime.UtcNow - state.Window < RateWindow && state.Count >= MaxAttemptsPerWindow)
                    return true;
            }
            return false;
        }

        private static void RecordAttempt(string key)
        {
            _rateLimit.AddOrUpdate(key,
                _ => (1, DateTime.UtcNow),
                (_, state) =>
                {
                    if (DateTime.UtcNow - state.Window >= RateWindow)
                        return (1, DateTime.UtcNow);
                    return (state.Count + 1, state.Window);
                });
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
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        }
    }

    public enum TokenVerifyResult
    {
        NameFree,
        Valid,
        TokenRequired,
        Invalid,
        Error
    }
}
