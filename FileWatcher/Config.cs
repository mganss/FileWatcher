using System;
using System.Collections.Generic;
using System.IO;

namespace FileWatcher
{
    /// <summary>
    /// Represents configuration information.
    /// </summary>
    public class Config
    {
        public List<WatchTask> Tasks { get; private set; } = new List<WatchTask>();
    }

    public class WatchTask
    {
        public string Path { get; set; }

        public string Filter { get; set; } = "*";

        public bool IncludeSubdirectories { get; set; } = false;

        public NotifyFilters NotifyFilter { get; set; } = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

        public WatcherChangeTypes ChangeTypes { get; set; } = WatcherChangeTypes.All;

        public string Command { get; set; }
    }
}
