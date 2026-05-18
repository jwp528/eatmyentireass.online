using Blazored.LocalStorage;
using static BlazorApp.Shared.Assets;

namespace BlazorApp.Client.Services
{
    public interface ICollectionService
    {
        Task<HashSet<AssTypeEnum>> GetUnlockedTypesAsync();
        Task<bool> MarkUnlockedAsync(AssTypeEnum assType);
    }

    public class CollectionService : ICollectionService
    {
        private const string StorageKey = "assdex_unlocked";
        private readonly ILocalStorageService _localStorage;

        public CollectionService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task<HashSet<AssTypeEnum>> GetUnlockedTypesAsync()
        {
            try
            {
                var saved = await _localStorage.GetItemAsync<List<string>>(StorageKey);
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

        public async Task<bool> MarkUnlockedAsync(AssTypeEnum assType)
        {
            var unlocked = await GetUnlockedTypesAsync();
            if (unlocked.Contains(assType)) return false; // already unlocked

            unlocked.Add(assType);
            await _localStorage.SetItemAsync(StorageKey, unlocked.Select(a => a.ToString()).ToList());
            return true; // newly unlocked
        }
    }
}
