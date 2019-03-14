using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileWatcher
{
    /// <summary>
    /// Allows invocation of commands for file system change events.
    /// </summary>
    public class Watcher: IDisposable
    {
        static Logger Log = LogManager.GetCurrentClassLogger();
        Config Config { get; set; }
        bool Error { get; set; }
        bool DryRun { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Watcher"/> class.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <exception cref="System.ArgumentException"><paramref name="config"/> is null</exception>
        public Watcher(Config config)
        {
            Config = config ?? throw new ArgumentException("config is null", nameof(config));
            DryRun = config.DryRun;
            if (DryRun)
                Log.Info("Only performing test run, not executing commands.");
            foreach (var task in config.Tasks)
            {
                if (string.IsNullOrWhiteSpace(task.Command))
                    throw new ArgumentException("Command is empty", nameof(config));
                if (string.IsNullOrWhiteSpace(task.Path))
                    throw new ArgumentException("Path is empty", nameof(config));
            }
        }

        class WatchInfo
        {
            public WatchTask WatchTask { get; set; }
            public Task Handler { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public BlockingCollection<WatchEvent> Events { get; set; }
        }

        class WatchEvent
        {
            public FileSystemEventArgs EventArgs { get; set; }
            public DateTime Time { get; set; }
        }

        readonly Dictionary<FileSystemWatcher, WatchInfo> Watchers = new Dictionary<FileSystemWatcher, WatchInfo>();
        readonly bool Initialized = false;

        /// <summary>
        /// Initializes the file system watch process.
        /// </summary>
        public void Init()
        {
            if (Initialized) return;

            foreach (var task in Config.Tasks)
            {
                task.Path = Environment.ExpandEnvironmentVariables(task.Path);

                Log.Info($"Creating watcher for path {task.Path}.");
                Log.Info($"Filter: {task.Filter}.");
                Log.Info($"IncludeSubdirectories: {task.IncludeSubdirectories}.");
                Log.Info($"NotifyFilter: {task.NotifyFilter}.");
                Log.Info($"ChangeTypes: {task.ChangeTypes}.");

                task.Command = Environment.ExpandEnvironmentVariables(task.Command);
                task.Arguments = Environment.ExpandEnvironmentVariables(task.Arguments);

                Log.Info($"Command: {task.Command}.");
                if (!string.IsNullOrWhiteSpace(task.Arguments))
                    Log.Info($"Arguments: {task.Arguments}.");
                if (!string.IsNullOrWhiteSpace(task.WorkingDirectory))
                    Log.Info($"WorkingDirectory: {task.WorkingDirectory}.");
                if (task.Throttle > 0)
                    Log.Info($"Throttle: {task.Throttle}ms.");
                if (task.Merge)
                    Log.Info("Merge: True.");
                if (!task.Wait)
                    Log.Info("Wait: False.");
                if (task.Timeout != -1)
                    Log.Info($"Timeout: {task.Timeout}s.");

                var watcher = new FileSystemWatcher(task.Path)
                {
                    Filter = task.Filter,
                    IncludeSubdirectories = task.IncludeSubdirectories,
                    NotifyFilter = task.NotifyFilter
                };

                var cancellationTokenSource = new CancellationTokenSource();
                var events = new BlockingCollection<WatchEvent>();
                var handler = Task.Factory.StartNew(() => HandleEvents(task, events, cancellationTokenSource.Token),
                    cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                Watchers[watcher] = new WatchInfo
                {
                    WatchTask = task,
                    Handler = handler,
                    CancellationTokenSource = cancellationTokenSource,
                    Events = events
                };

                if (task.ChangeTypes.HasFlag(WatcherChangeTypes.Changed))
                    watcher.Changed += Watcher_Changed;
                if (task.ChangeTypes.HasFlag(WatcherChangeTypes.Created))
                    watcher.Created += Watcher_Changed;
                if (task.ChangeTypes.HasFlag(WatcherChangeTypes.Deleted))
                    watcher.Deleted += Watcher_Changed;
                if (task.ChangeTypes.HasFlag(WatcherChangeTypes.Renamed))
                    watcher.Renamed += Watcher_Changed;

                watcher.Error += Watcher_Error;
            }
        }

        /// <summary>
        /// Starts file system watching.
        /// </summary>
        public void Start()
        {
            if (!Initialized) Init();

            foreach (var kvp in Watchers)
            {
                var watcher = kvp.Key;
                var info = kvp.Value;

                Log.Info($"Starting watcher for path {info.WatchTask.Path}, filter {info.WatchTask.Filter}.");

                watcher.EnableRaisingEvents = true;
            }
        }

        /// <summary>
        /// Stops file system watching.
        /// </summary>
        public void Stop()
        {
            foreach (var kvp in Watchers)
            {
                var watcher = kvp.Key;
                var info = kvp.Value;

                Log.Info($"Stopping watcher for path {info.WatchTask.Path}, filter {info.WatchTask.Filter}.");

                watcher.EnableRaisingEvents = false;
                info.CancellationTokenSource.Cancel();
                info.Handler.Wait();
            }
        }

        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            var info = Watchers[(FileSystemWatcher)sender];
            Log.Error(e.GetException(), $"Error watching file system for path {info.WatchTask.Path}, filter {info.WatchTask.Filter}.");
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            var info = Watchers[(FileSystemWatcher)sender];
            info.Events.Add(new WatchEvent { EventArgs = e, Time = DateTime.Now });
        }

        private void HandleEvents(WatchTask task, BlockingCollection<WatchEvent> events, CancellationToken token)
        {
            var runningProcesses = new HashSet<Process>();

            while (true)
            {
                try
                {
                    var watchEvent = events.Take(token);
                    var ev = watchEvent.EventArgs;

                    Log.Info("Received event.");

                    if (task.Throttle > 0)
                    {
                        var msToWait = task.Throttle - (int)(DateTime.Now - watchEvent.Time).TotalMilliseconds;
                        if (msToWait > 0 && token.WaitHandle.WaitOne(msToWait))
                        {
                            Log.Info("Stopping event handler.");
                            return;
                        }
                    }

                    if (task.Merge)
                    {
                        while (events.TryTake(out var mergeEvent))
                        {
                            var skipMsg = $"Merging {ev.ChangeType} event for path {task.Path}, filter {task.Filter}: {ev.Name}.";

                            if (ev is RenamedEventArgs skippedRenameEvent)
                                skipMsg += $" Old path was {skippedRenameEvent.OldFullPath}.";

                            Log.Info(skipMsg);

                            ev = mergeEvent.EventArgs;
                        }
                    }

                    var msg = $"Received {ev.ChangeType} event for path {task.Path}, filter {task.Filter}: {ev.Name}.";

                    if (ev is RenamedEventArgs re)
                        msg += $" Old path was {re.OldFullPath}.";

                    Log.Info(msg);

                    var process = StartProcess(task, ev);

                    if (!DryRun && task.Wait && !process.HasExited)
                    {
                        using (process)
                        {
                            if (!process.WaitForExit(task.Timeout <= 0 ? -1 : task.Timeout * 1000))
                            {
                                Log.Warn($"Process {process.Id} has not exited within {task.Timeout}s.");
                                process.Kill();
                            }
                            else
                                Log.Info($"Process {process.Id} has exited with code {process.ExitCode}.");
                        }
                    }
                    else
                    {
                        runningProcesses.Add(process);
                        process.Exited += (s, e) =>
                        {
                            Log.Info($"Process {process.Id} has exited with code {process.ExitCode}.");
                            runningProcesses.Remove(process);
                            process.Dispose();
                        };
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Info($"Stopping event handler for path {task.Path}, filter {task.Filter}.");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"An error has occurred handling events for path {task.Path}, filter {task.Filter}.");
                }
            }

            if (!DryRun)
            {
                foreach (var process in runningProcesses)
                {
                    if (!process.HasExited)
                        Log.Warn($"Process {process.Id} is still running.");
                    process.Dispose();
                }
            }
        }

        private Process StartProcess(WatchTask task, FileSystemEventArgs e)
        {
            var processStartInfo = new ProcessStartInfo(task.Command)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!string.IsNullOrEmpty(task.WorkingDirectory))
                processStartInfo.WorkingDirectory = task.WorkingDirectory;

            var changeType = e.ChangeType.ToString();

            processStartInfo.Environment["FileWatcher_FullPath"] = e.FullPath;
            processStartInfo.Environment["FileWatcher_Name"] = e.Name;
            processStartInfo.Environment["FileWatcher_ChangeType"] = changeType;

            var arguments = task.Arguments;
            arguments = arguments.Replace("{FullPath}", e.FullPath);
            arguments = arguments.Replace("{Name}", e.Name);
            arguments = arguments.Replace("{ChangeType}", changeType);

            if (e is RenamedEventArgs re)
            {
                processStartInfo.Environment["FileWatcher_OldPath"] = re.OldFullPath;
                processStartInfo.Environment["FileWatcher_OldName"] = re.OldName;
                arguments = arguments.Replace("{OldPath}", re.OldFullPath);
                arguments = arguments.Replace("{OldName}", re.OldName);
            }
            else
            {
                arguments = arguments.Replace("{OldPath}", "");
                arguments = arguments.Replace("{OldName}", "");
            }

            if (!string.IsNullOrWhiteSpace(arguments))
                processStartInfo.Arguments = arguments;

            var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, oe) => { if (oe.Data != null) Log.Info($"{process.Id}> {oe.Data}"); };
            process.ErrorDataReceived += (s, ee) => { if (ee.Data != null) Log.Error($"{process.Id}> {ee.Data}"); };

            Log.Info($"Starting command: {task.Command} {task.Arguments}");

            if (!DryRun)
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                Log.Info($"Process ID is {process.Id}");
            }

            return process;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            foreach (var watcher in Watchers.Keys)
            {
                watcher.Dispose();
            }
        }
    }
}
