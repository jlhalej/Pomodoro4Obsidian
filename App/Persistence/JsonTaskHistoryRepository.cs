using PomodoroForObsidian.Interfaces;
using PomodoroForObsidian.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PomodoroForObsidian.Persistence
{
    public class JsonTaskHistoryRepository : ITaskHistoryRepository
    {
        private readonly string _dataFilePath;
        private List<TaskHistoryEntry> _taskHistory;

        public JsonTaskHistoryRepository()
        {
            var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PomodoroForObsidian");
            _dataFilePath = Path.Combine(appDataFolder, "task-history.json");
        }

        public async Task InitializeAsync()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = await File.ReadAllTextAsync(_dataFilePath);
                    _taskHistory = JsonSerializer.Deserialize<List<TaskHistoryEntry>>(json) ?? new List<TaskHistoryEntry>();
                }
                else
                {
                    _taskHistory = new List<TaskHistoryEntry>();
                }
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Error loading task history: {ex.Message}");
                _taskHistory = new List<TaskHistoryEntry>();
            }
        }

        public async Task AddOrUpdateTaskAsync(string taskText)
        {
            var existingEntry = _taskHistory.FirstOrDefault(t => t.TaskText.Equals(taskText, StringComparison.OrdinalIgnoreCase));
            if (existingEntry != null)
            {
                existingEntry.UsageCount++;
                existingEntry.LastUsed = DateTime.UtcNow;
            }
            else
            {
                _taskHistory.Add(new TaskHistoryEntry
                {
                    TaskText = taskText,
                    UsageCount = 1,
                    LastUsed = DateTime.UtcNow,
                    FirstUsed = DateTime.UtcNow
                });
            }
            await SaveTaskHistoryAsync();
            await CleanupOldEntriesAsync();
        }

        public async Task<List<TaskHistoryEntry>> GetAllTasksAsync()
        {
            return await Task.Run(() => _taskHistory);
        }

        public async Task CleanupOldEntriesAsync(int maxEntries = 500)
        {
            if (_taskHistory.Count > maxEntries)
            {
                _taskHistory = _taskHistory.OrderByDescending(t => t.LastUsed).Take(maxEntries).ToList();
                await SaveTaskHistoryAsync();
            }
        }

        private async Task SaveTaskHistoryAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_taskHistory, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Error saving task history: {ex.Message}");
            }
        }
    }
}
