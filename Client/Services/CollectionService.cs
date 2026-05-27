using Blazored.LocalStorage;
using BlazorApp.Shared;
using System.Text.Json;
using static BlazorApp.Shared.Assets;

namespace BlazorApp.Client.Services
{
    public interface ICollectionService
    {
        Task<HashSet<AssTypeEnum>> GetUnlockedTypesAsync(string playerName);
        Task<bool> MarkUnlockedAsync(string playerName, AssTypeEnum assType);
        Task MigrateFromAnonymousAsync(string playerName);
    }

    public class CollectionService : ICollectionService
    {
        private const string StorageKeyPrefix = "assdex_unlocked";
        private readonly ILocalStorageService _localStorage;
        private readonly HttpClient _http;
        private readonly IPlayerService _playerService;

        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public CollectionService(ILocalStorageService localStorage, HttpClient http, IPlayerService playerService)
        {
            _localStorage = localStorage;
            _http = http;
            _playerService = playerService;
        }

        private static string GetStorageKey(string playerName)
        {
            var normalized = playerName.Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(normalized)
                ? $"{StorageKeyPrefix}_anon"
                : $"{StorageKeyPrefix}_{normalized}";
        }

        public async Task<HashSet<AssTypeEnum>> GetUnlockedTypesAsync(string playerName)
        {
            // Anonymous players always use localStorage
            if (string.IsNullOrWhiteSpace(playerName))
                return await LoadFromLocalStorageAsync(playerName);

            // Named players: try API first, fall back to localStorage on failure
            try
            {
                var json = await _http.GetStringAsync($"api/player/collection?name={Uri.EscapeDataString(playerName)}");
                var saved = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);
                if (saved == null) return new HashSet<AssTypeEnum>();

                var result = new HashSet<AssTypeEnum>();
                foreach (var name in saved)
                {
                    if (Enum.TryParse<AssTypeEnum>(name, out var assType))
                        result.Add(assType);
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CollectionService] API load failed, falling back to localStorage: {ex.Message}");
                return await LoadFromLocalStorageAsync(playerName);
            }
        }

        public async Task<bool> MarkUnlockedAsync(string playerName, AssTypeEnum assType)
        {
            // Check current state first (to return accurate "was it new?" result)
            var unlocked = await GetUnlockedTypesAsync(playerName);
            if (unlocked.Contains(assType)) return false;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                // Anonymous: write to localStorage
                unlocked.Add(assType);
                await SaveToLocalStorageAsync(playerName, unlocked);
                return true;
            }

            var token = await _playerService.GetStoredTokenAsync(playerName);
            if (!string.IsNullOrWhiteSpace(token))
            {
                // Authenticated: POST new type to API (server will union)
                await SaveToApiAsync(playerName, token, new HashSet<AssTypeEnum> { assType });
            }
            else
            {
                // Named but unclaimed: use localStorage
                unlocked.Add(assType);
                await SaveToLocalStorageAsync(playerName, unlocked);
            }
            return true;
        }

        /// <summary>
        /// Migrates any locally stored collection to Azure Tables.
        /// Called whenever a player sets/changes their name (including after claim/verify).
        /// </summary>
        public async Task MigrateFromAnonymousAsync(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return;

            var token = await _playerService.GetStoredTokenAsync(playerName);
            if (string.IsNullOrWhiteSpace(token))
            {
                // No token — local anon→named migration only
                var anonKey = $"{StorageKeyPrefix}_anon";
                var namedKey = GetStorageKey(playerName);
                if (anonKey == namedKey) return;

                try
                {
                    var anonData = await _localStorage.GetItemAsync<List<string>>(anonKey);
                    if (anonData == null || anonData.Count == 0) return;

                    var existing = await LoadFromLocalStorageAsync(playerName);
                    foreach (var name in anonData)
                    {
                        if (Enum.TryParse<AssTypeEnum>(name, out var t))
                            existing.Add(t);
                    }

                    await SaveToLocalStorageAsync(playerName, existing);
                    await _localStorage.RemoveItemAsync(anonKey);
                }
                catch { }
                return;
            }

            // Has token — migrate both anon and named localStorage data to API, then clear local keys
            try
            {
                var anonKey = $"{StorageKeyPrefix}_anon";
                var namedKey = GetStorageKey(playerName);

                var anonData = await _localStorage.GetItemAsync<List<string>>(anonKey);
                var namedData = await _localStorage.GetItemAsync<List<string>>(namedKey);

                bool hasLocalData = (anonData != null && anonData.Count > 0) || (namedData != null && namedData.Count > 0);
                if (!hasLocalData) return;

                var merged = new HashSet<AssTypeEnum>();
                foreach (var src in new[] { anonData, namedData })
                {
                    if (src == null) continue;
                    foreach (var name in src)
                    {
                        if (Enum.TryParse<AssTypeEnum>(name, out var t))
                            merged.Add(t);
                    }
                }

                await SaveToApiAsync(playerName, token, merged);

                // Clear local keys after successful API write
                if (anonData != null) await _localStorage.RemoveItemAsync(anonKey);
                if (namedData != null) await _localStorage.RemoveItemAsync(namedKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CollectionService] Migration to API failed: {ex.Message}");
            }
        }

        private async Task<HashSet<AssTypeEnum>> LoadFromLocalStorageAsync(string playerName)
        {
            var key = GetStorageKey(playerName);
            try
            {
                var saved = await _localStorage.GetItemAsync<List<string>>(key);
                if (saved == null) return new HashSet<AssTypeEnum>();

                var result = new HashSet<AssTypeEnum>();
                foreach (var name in saved)
                {
                    if (Enum.TryParse<AssTypeEnum>(name, out var assType))
                        result.Add(assType);
                }
                return result;
            }
            catch
            {
                return new HashSet<AssTypeEnum>();
            }
        }

        private async Task SaveToLocalStorageAsync(string playerName, HashSet<AssTypeEnum> unlocked)
        {
            var key = GetStorageKey(playerName);
            try
            {
                await _localStorage.SetItemAsync(key, unlocked.Select(a => a.ToString()).ToList());
            }
            catch { }
        }

        private async Task SaveToApiAsync(string playerName, string token, HashSet<AssTypeEnum> types)
        {
            try
            {
                var request = new PlayerCollectionRequest
                {
                    Name = playerName,
                    AuthToken = token,
                    UnlockedTypes = types.Select(t => t.ToString()).ToList()
                };
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _http.PostAsync("api/player/collection", content);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[CollectionService] API save failed ({response.StatusCode}): {err}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CollectionService] API save error: {ex.Message}");
            }
        }
    }
}
