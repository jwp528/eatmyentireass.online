using Blazored.LocalStorage;

namespace BlazorApp.Client.Services
{
    public interface ISettingsService
    {
        Task<bool> GetSoundEnabledAsync();
        Task SetSoundEnabledAsync(bool enabled);
        Task<string?> GetLastPlayerNameAsync();
        Task SetLastPlayerNameAsync(string playerName);
        Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default);
        Task SetSettingAsync<T>(string key, T value);
    }

    public class SettingsService : ISettingsService
    {
        private readonly ILocalStorageService _localStorage;

        public SettingsService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task<bool> GetSoundEnabledAsync()
        {
            return await GetSettingAsync("soundEnabled", true);
        }

        public async Task SetSoundEnabledAsync(bool enabled)
        {
            await SetSettingAsync("soundEnabled", enabled);
        }

        public async Task<string?> GetLastPlayerNameAsync()
        {
            return await GetSettingAsync<string?>("lastPlayerName", null);
        }

        public async Task SetLastPlayerNameAsync(string playerName)
        {
            await SetSettingAsync("lastPlayerName", playerName);
        }

        public async Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default)
        {
            try
            {
                var value = await _localStorage.GetItemAsync<T>(key);
                return value ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public async Task SetSettingAsync<T>(string key, T value)
        {
            try
            {
                await _localStorage.SetItemAsync(key, value);
            }
            catch
            {
                // Silently handle localStorage errors
            }
        }
    }
}