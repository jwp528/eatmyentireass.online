using System.Net;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Services;
using System.Text.Json;

namespace Api
{
    public class LeaderboardFunction
    {
        private readonly ILogger _logger;
        private readonly ILeaderboardStorage _leaderboardStorage;

        public LeaderboardFunction(ILoggerFactory loggerFactory, ILeaderboardStorage leaderboardStorage)
        {
            _logger = loggerFactory.CreateLogger<LeaderboardFunction>();
            _leaderboardStorage = leaderboardStorage;
        }

        [Function("GetLeaderboard")]
        public async Task<HttpResponseData> GetLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting leaderboard scores");

            try
            {
                var countParam = req.Query["count"];
                var count = int.TryParse(countParam, out var c) ? c : 10;

                var scores = await _leaderboardStorage.GetTopScoresAsync(count);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonSerializer.Serialize(scores));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting leaderboard");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Internal server error");
                return response;
            }
        }

        [Function("SaveScore")]
        public async Task<HttpResponseData> SaveScore([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Saving score to leaderboard");

            try
            {
                string requestBody = await req.ReadAsStringAsync() ?? "";
                var entry = JsonSerializer.Deserialize<LeaderboardEntry>(requestBody);

                if (entry == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid score entry");
                    return badResponse;
                }

                await _leaderboardStorage.SaveScoreAsync(entry);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true }));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving score");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("Internal server error");
                return response;
            }
        }
    }
}