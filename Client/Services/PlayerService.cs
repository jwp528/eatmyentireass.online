using BlazorApp.Shared;
using Blazored.LocalStorage;
using System.Text.Json;

namespace BlazorApp.Client.Services
{
    public interface IPlayerService
    {
        Task<bool> IsNameClaimedAsync(string name);
        Task<string> ClaimNameAsync(string name, string password);
        Task<string> VerifyNameAsync(string name, string password);
        Task<string?> GetStoredTokenAsync(string name);
        Task StoreTokenAsync(string name, string token);
        Task ClearTokenAsync(string name);
    }

    public class PlayerService : IPlayerService
    {
        private readonly HttpClient _http;
        private readonly ILocalStorageService _localStorage;

        public PlayerService(HttpClient http, ILocalStorageService localStorage)
        {
            _http = http;
            _localStorage = localStorage;
        }

        public async Task<bool> IsNameClaimedAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            try
            {
                var json = await _http.GetStringAsync($"api/player/status?name={Uri.EscapeDataString(name)}");
                var result = JsonSerializer.Deserialize<PlayerStatusResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result?.IsClaimed ?? false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayerService] IsNameClaimedAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<string> ClaimNameAsync(string name, string password)
        {
            var request = new PlayerClaimRequest { Name = name, Password = password };
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("api/player/claim", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PlayerAuthResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.Token ?? throw new Exception("No token returned");
        }

        public async Task<string> VerifyNameAsync(string name, string password)
        {
            var request = new PlayerClaimRequest { Name = name, Password = password };
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("api/player/verify", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PlayerAuthResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.Token ?? throw new Exception("No token returned");
        }

        public async Task<string?> GetStoredTokenAsync(string name)
        {
            try
            {
                var key = TokenKey(name);
                return await _localStorage.GetItemAsync<string>(key);
            }
            catch
            {
                return null;
            }
        }

        public async Task StoreTokenAsync(string name, string token)
        {
            try
            {
                await _localStorage.SetItemAsync(TokenKey(name), token);
            }
            catch { }
        }

        public async Task ClearTokenAsync(string name)
        {
            try
            {
                await _localStorage.RemoveItemAsync(TokenKey(name));
            }
            catch { }
        }

        private static string TokenKey(string name) =>
            $"playerToken_{PlayerNameHelper.Normalize(name)}";
    }
}
