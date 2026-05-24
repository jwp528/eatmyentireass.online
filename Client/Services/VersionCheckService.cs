using System.Reflection;
using System.Text.Json;
using Microsoft.JSInterop;

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
        public static readonly string AppVersion =
            typeof(VersionCheckService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "dev";

        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _js;
        private string? _serverVersion;

        public string CurrentVersion => AppVersion;
        public string? ServerVersion => _serverVersion;

        public VersionCheckService(HttpClient httpClient, IJSRuntime js)
        {
            _httpClient = httpClient;
            _js = js;
        }

        public async Task<bool> IsUpdateAvailableAsync()
        {
            try
            {
                var bust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var json = await _httpClient.GetStringAsync($"/version.json?v={bust}");
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                _serverVersion = doc.GetProperty("version").GetString();

                if (_serverVersion == null) return false;

                // Best case: compiled app version matches server — no update needed.
                if (_serverVersion == AppVersion) return false;

                // Server version differs from compiled version. However, if the user
                // already clicked "Reload Now" for this exact version, skip the modal.
                // This prevents an infinite loop when the browser stubbornly serves old
                // cached WASM even after a forced navigation.
                var acknowledged = await _js.InvokeAsync<string?>(
                    "localStorage.getItem", "emea_loaded_version");
                if (acknowledged == _serverVersion) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
