using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace FileWatcher
{
    public class Watcher
    {
        /// <summary>
        /// Gets or sets a value indicating whether commands will be executed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if commands will be executed; otherwise, <c>false</c>.
        /// </value>
        public bool DryRun { get; set; } = false;

        static Logger Log = LogManager.GetCurrentClassLogger();
        Config Config { get; set; }
        bool Error { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Watcher"/> class.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <exception cref="System.ArgumentException"><paramref name="config"/> is null</exception>
        public Watcher(Config config)
        {
            Config = config ?? throw new ArgumentException("config is null", nameof(config));
        }

        Dictionary<FileSystemWatcher, WatchTask> Watchers = new Dictionary<FileSystemWatcher, WatchTask>();
        bool Initialized = false;

        public void Init()
        {
            if (Initialized) return;

            foreach (var task in Config.Tasks)
            {
                Log.Info($"Creating watcher for path {task.Path}");
                Log.Info($"Filter: {task.Filter}");
                Log.Info($"IncludeSubdirectories: {task.IncludeSubdirectories}");
                Log.Info($"NotifyFilter: {task.NotifyFilter}");
                Log.Info($"ChangeTypes: {task.ChangeTypes}");

                var watcher = new FileSystemWatcher(task.Path)
                {
                    Filter = task.Filter,
                    IncludeSubdirectories = task.IncludeSubdirectories,
                    NotifyFilter = task.NotifyFilter
                };

                Watchers[watcher] = task;

                if (task.ChangeTypes.HasFlag(WatcherChangeTypes.Changed))
                    watcher.Changed += Watcher_Changed;
                if (task.ChangeTypes.HasFlag(WatcherChangeTypes.Created))
                    watcher.Created += Watcher_Changed;
                if (task.ChangeTypes.HasFlag(WatcherChangeTypes.Deleted))
                    watcher.Deleted += Watcher_Changed;
                if (task.ChangeTypes.HasFlag(WatcherChangeTypes.Renamed))
                    watcher.Renamed += Watcher_Renamed; ;

                watcher.Error += Watcher_Error;
            }
        }

        public void Start()
        {
            foreach (var kvp in Watchers)
            {
                var watcher = kvp.Key;
                var task = kvp.Value;

                Log.Info($"Starting watcher for path {task.Path}, filter {task.Filter}");

                watcher.EnableRaisingEvents = true;
            }
        }

        public void Stop()
        {
            foreach (var kvp in Watchers)
            {
                var watcher = kvp.Key;
                var task = kvp.Value;

                Log.Info($"Starting watcher for path {task.Path}, filter {task.Filter}");

                watcher.EnableRaisingEvents = true;
            }
        }

        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
