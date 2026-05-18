using System.Text.Json;

namespace BlazorApp.Client.Services
{
    public interface IVersionCheckService
    {
        Task<bool> IsUpdateAvailableAsync();
        string CurrentVersion { get; }
        string? ServerVersion { get; }
    }

    public class VersionCheckService : IVersionCheckService
    {
        // Bump this constant with every deployment; must match wwwroot/version.json
        public const string AppVersion = "2.0.0";

        private readonly HttpClient _httpClient;
        private string? _serverVersion;

        public string CurrentVersion => AppVersion;
        public string? ServerVersion => _serverVersion;

        public VersionCheckService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> IsUpdateAvailableAsync()
        {
            try
            {
                var bust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var json = await _httpClient.GetStringAsync($"/version.json?v={bust}");
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                _serverVersion = doc.GetProperty("version").GetString();
                return _serverVersion != null && _serverVersion != AppVersion;
            }
            catch
            {
                return false;
            }
        }
    }
}
