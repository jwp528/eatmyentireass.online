using Blazored.LocalStorage;
using BlazorApp.Shared;
using System.Text.Json;
using static BlazorApp.Shared.Assets;

namespace BlazorApp.Client.Services
{
    public interface IProgressService
    {
        Task<Dictionary<AssTypeEnum, AssTypeProgress>> LoadAsync(string playerName);
        Task SaveAsync(string playerName, Dictionary<AssTypeEnum, AssTypeProgress> progress);
        Task<AssTypeProgress> GetProgressAsync(string playerName, AssTypeEnum assType);
        Task MigrateFromAnonymousAsync(string playerName);
        int GetEffectiveCompleteThreshold(AssTypeEnum assType, AssTypeProgress progress);
        double GetEffectivePoints(AssTypeEnum assType, AssTypeProgress progress);
    }

    public class ProgressService : IProgressService
    {
        private const string StorageKeyPrefix = "ass_type_progress_v1";
        private readonly ILocalStorageService _localStorage;
        private readonly HttpClient _http;
        private readonly IPlayerService _playerService;

        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public ProgressService(ILocalStorageService localStorage, HttpClient http, IPlayerService playerService)
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

        public async Task<Dictionary<AssTypeEnum, AssTypeProgress>> LoadAsync(string playerName)
        {
            // Anonymous players always use localStorage
            if (string.IsNullOrWhiteSpace(playerName))
                return await LoadFromLocalStorageAsync(playerName);

            // Named players: try API first, fall back to localStorage on failure
            try
            {
                var json = await _http.GetStringAsync($"api/player/progress?name={Uri.EscapeDataString(playerName)}");
                var saved = JsonSerializer.Deserialize<Dictionary<string, AssTypeProgress>>(json, _jsonOptions);
                var result = Empty();
                if (saved != null)
                {
                    foreach (var assType in Enum.GetValues<AssTypeEnum>())
                    {
                        if (saved.TryGetValue(assType.ToString(), out var p))
                            result[assType] = p;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProgressService] API load failed, falling back to localStorage: {ex.Message}");
                return await LoadFromLocalStorageAsync(playerName);
            }
        }

        public async Task SaveAsync(string playerName, Dictionary<AssTypeEnum, AssTypeProgress> progress)
        {
            // Anonymous players always use localStorage
            if (string.IsNullOrWhiteSpace(playerName))
            {
                await SaveToLocalStorageAsync(playerName, progress);
                return;
            }

            // Named players: use API if authenticated, otherwise localStorage
            var token = await _playerService.GetStoredTokenAsync(playerName);
            if (!string.IsNullOrWhiteSpace(token))
            {
                await SaveToApiAsync(playerName, token, progress);
            }
            else
            {
                await SaveToLocalStorageAsync(playerName, progress);
            }
        }

        public async Task<AssTypeProgress> GetProgressAsync(string playerName, AssTypeEnum assType)
        {
            var all = await LoadAsync(playerName);
            return all.TryGetValue(assType, out var p) ? p : new AssTypeProgress();
        }

        /// <summary>
        /// Migrates any locally stored progress to Azure Tables.
        /// Called whenever a player sets/changes their name (including after claim/verify).
        /// </summary>
        public async Task MigrateFromAnonymousAsync(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return;

            var token = await _playerService.GetStoredTokenAsync(playerName);
            if (string.IsNullOrWhiteSpace(token))
            {
                // No token — can't write to server; do local anon→named migration only
                var anonKey = $"{StorageKeyPrefix}_anon";
                var namedKey = GetStorageKey(playerName);
                if (anonKey == namedKey) return;

                try
                {
                    var anonData = await _localStorage.GetItemAsync<Dictionary<string, AssTypeProgress>>(anonKey);
                    if (anonData == null || anonData.Count == 0) return;

                    var existing = await LoadFromLocalStorageAsync(playerName);
                    foreach (var assType in Enum.GetValues<AssTypeEnum>())
                    {
                        if (!anonData.TryGetValue(assType.ToString(), out var anonProgress)) continue;
                        existing[assType].Eaten = Math.Max(existing[assType].Eaten, anonProgress.Eaten);
                        existing[assType].ClicksUsed = Math.Max(existing[assType].ClicksUsed, anonProgress.ClicksUsed);
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

                var anonData = await _localStorage.GetItemAsync<Dictionary<string, AssTypeProgress>>(anonKey);
                var namedData = await _localStorage.GetItemAsync<Dictionary<string, AssTypeProgress>>(namedKey);

                bool hasLocalData = (anonData != null && anonData.Count > 0) || (namedData != null && namedData.Count > 0);
                if (!hasLocalData) return;

                // Build merged local data (MAX semantics)
                var merged = Empty();
                foreach (var assType in Enum.GetValues<AssTypeEnum>())
                {
                    var key = assType.ToString();
                    var anonP = anonData != null && anonData.TryGetValue(key, out var a) ? a : null;
                    var namedP = namedData != null && namedData.TryGetValue(key, out var n) ? n : null;

                    if (anonP != null || namedP != null)
                    {
                        merged[assType] = new AssTypeProgress
                        {
                            Eaten = Math.Max(anonP?.Eaten ?? 0, namedP?.Eaten ?? 0),
                            ClicksUsed = Math.Max(anonP?.ClicksUsed ?? 0, namedP?.ClicksUsed ?? 0)
                        };
                    }
                }

                await SaveToApiAsync(playerName, token, merged);

                // Clear local keys after successful API write
                if (anonData != null) await _localStorage.RemoveItemAsync(anonKey);
                if (namedData != null) await _localStorage.RemoveItemAsync(namedKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProgressService] Migration to API failed: {ex.Message}");
            }
        }

        // Returns the piecesEaten value at which completion triggers.
        public int GetEffectiveCompleteThreshold(AssTypeEnum assType, AssTypeProgress progress)
        {
            int frameCount = Frames[assType.ToString()].Count;
            int baseThreshold = frameCount - 1;
            if (progress.HasPerk50)
                return Math.Max(0, (int)Math.Ceiling(frameCount / 2.0) - 1);
            if (progress.HasPerk10)
                return Math.Max(0, baseThreshold - 1);
            return baseThreshold;
        }

        public double GetEffectivePoints(AssTypeEnum assType, AssTypeProgress progress)
        {
            double basePoints = GetPointsForAssType(assType);
            return progress.HasPerk100 ? basePoints + 1.0 : basePoints;
        }

        private static Dictionary<AssTypeEnum, AssTypeProgress> Empty()
            => Enum.GetValues<AssTypeEnum>().ToDictionary(t => t, _ => new AssTypeProgress());

        private async Task<Dictionary<AssTypeEnum, AssTypeProgress>> LoadFromLocalStorageAsync(string playerName)
        {
            var key = GetStorageKey(playerName);
            try
            {
                var saved = await _localStorage.GetItemAsync<Dictionary<string, AssTypeProgress>>(key);
                var result = Empty();
                if (saved != null)
                {
                    foreach (var assType in Enum.GetValues<AssTypeEnum>())
                    {
                        if (saved.TryGetValue(assType.ToString(), out var p))
                            result[assType] = p;
                    }
                }
                return result;
            }
            catch
            {
                return Empty();
            }
        }

        private async Task SaveToLocalStorageAsync(string playerName, Dictionary<AssTypeEnum, AssTypeProgress> progress)
        {
            var key = GetStorageKey(playerName);
            try
            {
                var toSave = progress.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
                await _localStorage.SetItemAsync(key, toSave);
            }
            catch { }
        }

        private async Task SaveToApiAsync(string playerName, string token, Dictionary<AssTypeEnum, AssTypeProgress> progress)
        {
            try
            {
                var request = new PlayerProgressRequest
                {
                    Name = playerName,
                    AuthToken = token,
                    Progress = progress.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
                };
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _http.PostAsync("api/player/progress", content);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ProgressService] API save failed ({response.StatusCode}): {err}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProgressService] API save error: {ex.Message}");
            }
        }
    }
}
