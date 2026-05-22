using Blazored.LocalStorage;
using static BlazorApp.Shared.Assets;

namespace BlazorApp.Client.Services
{
    public class AssTypeProgress
    {
        public int Eaten { get; set; }
        public long ClicksUsed { get; set; }

        public bool HasPerk10 => Eaten >= 10;
        public bool HasPerk25 => Eaten >= 25;
        public bool HasPerk50 => Eaten >= 50;
        public bool HasPerk100 => Eaten >= 100;

        public int NextMilestone => Eaten switch
        {
            < 10 => 10,
            < 25 => 25,
            < 50 => 50,
            < 100 => 100,
            _ => 0
        };
    }

    public interface IProgressService
    {
        Task<Dictionary<AssTypeEnum, AssTypeProgress>> LoadAsync();
        Task SaveAsync(Dictionary<AssTypeEnum, AssTypeProgress> progress);
        Task<AssTypeProgress> GetProgressAsync(AssTypeEnum assType);
        int GetEffectiveCompleteThreshold(AssTypeEnum assType, AssTypeProgress progress);
        double GetEffectivePoints(AssTypeEnum assType, AssTypeProgress progress);
    }

    public class ProgressService : IProgressService
    {
        private const string StorageKey = "ass_type_progress_v1";
        private readonly ILocalStorageService _localStorage;

        public ProgressService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task<Dictionary<AssTypeEnum, AssTypeProgress>> LoadAsync()
        {
            try
            {
                var saved = await _localStorage.GetItemAsync<Dictionary<string, AssTypeProgress>>(StorageKey);
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

        public async Task SaveAsync(Dictionary<AssTypeEnum, AssTypeProgress> progress)
        {
            try
            {
                var toSave = progress.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
                await _localStorage.SetItemAsync(StorageKey, toSave);
            }
            catch { }
        }

        public async Task<AssTypeProgress> GetProgressAsync(AssTypeEnum assType)
        {
            var all = await LoadAsync();
            return all.TryGetValue(assType, out var p) ? p : new AssTypeProgress();
        }

        // Returns the piecesEaten value at which completion triggers.
        // Completion fires when piecesEaten >= threshold.
        // Base:   frameCount - 1  (requires frameCount total clicks)
        // Perk10: frameCount - 2  (requires frameCount - 1 total clicks)
        // Perk50: ceil(frameCount/2) - 1  (requires ceil(frameCount/2) total clicks)
        // Perk50 supersedes Perk10 (it's the bigger discount).
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
    }
}
