using Blazored.LocalStorage;
using static BlazorApp.Shared.Assets;

namespace BlazorApp.Client.Services
{
    public interface ICollectionService
    {
        Task<HashSet<AssTypeEnum>> GetUnlockedTypesAsync(string playerName);
        Task<bool> MarkUnlockedAsync(string playerName, AssTypeEnum assType);
    }

    public class CollectionService : ICollectionService
    {
        private const string StorageKeyPrefix = "assdex_unlocked";
        private readonly ILocalStorageService _localStorage;

        public CollectionService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        private static string GetStorageKey(string playerName)
        {
            var normalized = playerName.Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(normalized)
                ? string.Empty
                : $"{StorageKeyPrefix}_{normalized}";
        }

        public async Task<HashSet<AssTypeEnum>> GetUnlockedTypesAsync(string playerName)
        {
            var key = GetStorageKey(playerName);
            if (string.IsNullOrEmpty(key)) return new HashSet<AssTypeEnum>();

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

        public async Task<bool> MarkUnlockedAsync(string playerName, AssTypeEnum assType)
        {
            var key = GetStorageKey(playerName);
            if (string.IsNullOrEmpty(key)) return false;

            var unlocked = await GetUnlockedTypesAsync(playerName);
            if (unlocked.Contains(assType)) return false;

            unlocked.Add(assType);
            await _localStorage.SetItemAsync(key, unlocked.Select(a => a.ToString()).ToList());
            return true;
        }
    }
}
