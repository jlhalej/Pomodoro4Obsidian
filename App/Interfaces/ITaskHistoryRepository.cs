using PomodoroForObsidian.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PomodoroForObsidian.Interfaces
{
    public interface ITaskHistoryRepository
    {
        Task InitializeAsync();
        Task<List<TaskHistoryEntry>> GetAllTasksAsync();
        Task AddOrUpdateTaskAsync(string taskText);
        Task CleanupOldEntriesAsync(int maxEntries = 500);
    }
}
