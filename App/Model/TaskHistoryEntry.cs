
using System;

namespace PomodoroForObsidian.Model
{
    public class TaskHistoryEntry
    {
        public string TaskText { get; set; } = string.Empty;
        public int UsageCount { get; set; }
        public DateTime LastUsed { get; set; }
        public DateTime FirstUsed { get; set; }
    }
}
