using Blazored.LocalStorage;
using BlazorApp.Shared;

namespace BlazorApp.Client.Services
{
    public interface IDailyChallengeService
    {
        Task<DailyChallengeProgress> GetOrCreateTodayTasksAsync();
        Task<(DailyChallengeProgress Progress, bool AnyNewlyCompleted)> CheckGameResultAsync(
            int totalAssesCount,
            Dictionary<string, int> breakdown,
            double avgCps,
            int peakCombo,
            bool hasAllTypes);
    }

    public class DailyChallengeService : IDailyChallengeService
    {
        private const string StorageKey = "daily_challenge_progress";
        private readonly ILocalStorageService _localStorage;

        public DailyChallengeService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task<DailyChallengeProgress> GetOrCreateTodayTasksAsync()
        {
            var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
            try
            {
                var saved = await _localStorage.GetItemAsync<DailyChallengeProgress>(StorageKey);
                if (saved?.Date == todayKey && saved.Tasks?.Count == 3)
                    return saved;
            }
            catch { /* localStorage unavailable or corrupt */ }

            var progress = new DailyChallengeProgress
            {
                Date = todayKey,
                Tasks = DailyChallenge.GenerateDailyTasks(DateOnly.FromDateTime(DateTime.UtcNow))
            };

            try { await _localStorage.SetItemAsync(StorageKey, progress); }
            catch { }

            return progress;
        }

        public async Task<(DailyChallengeProgress Progress, bool AnyNewlyCompleted)> CheckGameResultAsync(
            int totalAssesCount,
            Dictionary<string, int> breakdown,
            double avgCps,
            int peakCombo,
            bool hasAllTypes)
        {
            var progress = await GetOrCreateTodayTasksAsync();
            bool anyNew = false;

            foreach (var task in progress.Tasks)
            {
                if (task.Completed) continue;

                bool met = task.Type switch
                {
                    DailyChallengeType.EatXAsses =>
                        totalAssesCount >= task.TargetValue,
                    DailyChallengeType.EatXTypeAsses =>
                        task.AssTypeName != null &&
                        breakdown.TryGetValue(task.AssTypeName, out var cnt) &&
                        cnt >= task.TargetValue,
                    DailyChallengeType.CpsOver10 =>
                        avgCps >= 10.0,
                    DailyChallengeType.ReachComboX =>
                        peakCombo >= task.TargetValue,
                    DailyChallengeType.EatAllTypes =>
                        hasAllTypes,
                    _ => false
                };

                if (met)
                {
                    task.Completed = true;
                    anyNew = true;
                }
            }

            if (anyNew)
            {
                try { await _localStorage.SetItemAsync(StorageKey, progress); }
                catch { }
            }

            return (progress, anyNew);
        }
    }
}
