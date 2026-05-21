namespace BlazorApp.Shared
{
    public static class PlayerNameHelper
    {
        public static string Normalize(string name) =>
            name.Trim().ToLowerInvariant();
    }

    public class PlayerStatusResponse
    {
        public bool IsClaimed { get; set; }
    }

    public class PlayerClaimRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class PlayerAuthResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    public class SaveScoreRequest
    {
        public LeaderboardEntry Entry { get; set; } = new();
        public string? AuthToken { get; set; }
    }
}
