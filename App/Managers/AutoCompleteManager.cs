
using PomodoroForObsidian.Interfaces;
using PomodoroForObsidian.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PomodoroForObsidian.Managers
{
    public class AutoCompleteManager
    {
        private readonly ITaskHistoryRepository _taskHistoryRepository;

        public AutoCompleteManager(ITaskHistoryRepository taskHistoryRepository)
        {
            _taskHistoryRepository = taskHistoryRepository;
        }

        public async Task<List<string>> GetSuggestionsAsync(string query, int maxResults = 7)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return new List<string>();

            var allTasks = await _taskHistoryRepository.GetAllTasksAsync();
            if (allTasks == null || !allTasks.Any())
                return new List<string>();

            var queryLower = query.ToLower();

            var scoredSuggestions = new List<(TaskHistoryEntry entry, double score)>();

            // Normalize Frequency and Recency
            double maxUsage = allTasks.Max(t => t.UsageCount);
            double minUsage = allTasks.Min(t => t.UsageCount);

            DateTime maxDate = allTasks.Max(t => t.LastUsed);
            DateTime minDate = allTasks.Min(t => t.LastUsed);

            foreach (var task in allTasks)
            {
                double relevance = GetRelevanceScore(task.TaskText, queryLower);
                if (relevance > 0)
                {
                    double frequencyScore = (maxUsage == minUsage) ? 1.0 : (task.UsageCount - minUsage) / (maxUsage - minUsage);
                    double recencyScore = (maxDate == minDate) ? 1.0 : (task.LastUsed - minDate).TotalDays / (maxDate - minDate).TotalDays;

                    double finalScore = (relevance * 0.25) + (recencyScore * 0.35) + (frequencyScore * 0.40);
                    scoredSuggestions.Add((task, finalScore));
                }
            }

            return scoredSuggestions
                .OrderByDescending(s => s.score)
                .Select(s => s.entry.TaskText)
                .Take(maxResults)
                .ToList();
        }

        private double GetRelevanceScore(string taskText, string queryLower)
        {
            var taskTextLower = taskText.ToLower();

            // 1. Exact prefix match
            if (taskTextLower.StartsWith(queryLower))
                return 1.0;

            // 2. Word-beginning match
            var words = taskTextLower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Any(w => w.StartsWith(queryLower)))
                return 0.7;

            // 3. Substring match
            if (taskTextLower.Contains(queryLower))
                return 0.3;

            return 0.0;
        }

        public async Task<List<string>> GetRecentSuggestionsAsync(int count = 10)
        {
            var allTasks = await _taskHistoryRepository.GetAllTasksAsync();
            return allTasks
                .OrderByDescending(t => t.LastUsed)
                .Select(t => "🕒 " + t.TaskText)
                .Take(count)
                .ToList();
        }

        public async Task<List<string>> GetFrequentSuggestionsAsync(int count = 10)
        {
            var allTasks = await _taskHistoryRepository.GetAllTasksAsync();
            return allTasks
                .OrderByDescending(t => t.UsageCount)
                .Select(t => "⭐ " + t.TaskText)
                .Take(count)
                .ToList();
        }
    }
}

