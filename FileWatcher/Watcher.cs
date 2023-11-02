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

namespace FileWatcher;

/// <summary>
/// Allows invocation of commands for file system change events.
/// </summary>
public class Watcher: IDisposable
{
    readonly Logger Log;
    WatchTask WatchTask { get; set; }

    /// <summary>
    /// Occurs when a process has been started in response to a file system change event.
    /// </summary>
    public event EventHandler<ProcessEventArgs> ProcessStarted;

    /// <summary>
    /// Occurs when a process has exited which had previously been started in response to a file system change event.
    /// </summary>
    public event EventHandler<ProcessEventArgs> ProcessExited;

    /// <summary>
    /// Occurs when a process has not exited within the configured timeout period.
    /// </summary>
    public event EventHandler<ProcessEventArgs> ProcessTimeout;

    /// <summary>
    /// Gets or sets a value indicating whether to perform a test run.
    /// </summary>
    /// <value>
    ///   <c>true</c> if only a test run should be performed; otherwise, <c>false</c>.
    /// </value>
    public bool DryRun { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Watcher"/> class.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <exception cref="System.ArgumentException"><paramref name="task"/> is null</exception>
    public Watcher(WatchTask task)
    {
        WatchTask = task ?? throw new ArgumentNullException(nameof(task));

        if (string.IsNullOrWhiteSpace(task.Name))
            throw new ArgumentException("Name is empty", nameof(task));
        if (string.IsNullOrWhiteSpace(task.Command))
            throw new ArgumentException("Command is empty", nameof(task));
        if (string.IsNullOrWhiteSpace(task.Path))
            throw new ArgumentException("Path is empty", nameof(task));

        Log = LogManager.GetLogger(task.Name);
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

    readonly Dictionary<FileSystemWatcher, WatchInfo> Watchers = new();
    readonly bool Initialized = false;

    /// <summary>
    /// Initializes the file system watch process.
    /// </summary>
    public void Init()
    {
        if (Initialized) return;

        var task = WatchTask;

        task.Path = Environment.ExpandEnvironmentVariables(task.Path);

        Log.Info("Creating watcher for path {path}.", task.Path);
        Log.Info("Filter: {filter}.", task.Filter);
        Log.Info("IncludeSubdirectories: {includesubdirectories}.", task.IncludeSubdirectories);
        Log.Info("NotifyFilter: {notifyfilter}.", task.NotifyFilter);
        Log.Info("ChangeTypes: {changetypes}.", task.ChangeTypes);

        task.Command = Environment.ExpandEnvironmentVariables(task.Command);
        task.Arguments = Environment.ExpandEnvironmentVariables(task.Arguments);

        Log.Info("Command: {command}.", task.Command);
        if (!string.IsNullOrWhiteSpace(task.Arguments))
            Log.Info("Arguments: {arguments}.", task.Arguments);
        if (!string.IsNullOrWhiteSpace(task.WorkingDirectory))
            Log.Info("WorkingDirectory: {workingdirectory}.", task.WorkingDirectory);
        if (task.Throttle > 0)
            Log.Info("Throttle: {throttle}ms.", task.Throttle);
        if (task.Merge)
            Log.Info("Merge: True.");
        if (!task.Wait)
            Log.Info("Wait: False.");
        if (task.Timeout != -1)
            Log.Info("Timeout: {timeout}s.", task.Timeout);

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

            Log.Info("Starting watcher for path {path}, filter {filter}.", info.WatchTask.Path, info.WatchTask.Filter);

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

            Log.Info("Stopping watcher for path {path}, filter {filter}.", info.WatchTask.Path, info.WatchTask.Filter);

            watcher.EnableRaisingEvents = false;
            info.CancellationTokenSource.Cancel();
            info.Handler.Wait();
        }
    }

    private void Watcher_Error(object sender, ErrorEventArgs e)
    {
        var info = Watchers[(FileSystemWatcher)sender];
        Log.Error(e.GetException(), "Error watching file system for path {path}, filter {filter}.", info.WatchTask.Path, info.WatchTask.Filter);
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        var info = Watchers[(FileSystemWatcher)sender];
        info.Events.Add(new WatchEvent { EventArgs = e, Time = DateTime.UtcNow });
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
                    var msToWait = task.Throttle - (int)(DateTime.UtcNow - watchEvent.Time).TotalMilliseconds;
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
                        Log.Info("Merging {changetype} event for path {path}, filter {filter}: {name}.", ev.ChangeType, task.Path, task.Filter, ev.Name);

                        if (ev is RenamedEventArgs skippedRenameEvent)
                            Log.Info("Old path was {oldpath}.", skippedRenameEvent.OldFullPath);

                        ev = mergeEvent.EventArgs;
                    }
                }

                Log.Info("Received {changeType} event for path {path}, filter {filter}: {name}.", ev.ChangeType, task.Path, task.Filter, ev.Name);

                if (ev is RenamedEventArgs re)
                    Log.Info("Old path was {oldpath}.", re.OldFullPath);

                var process = StartProcess(task, ev);

                if (!DryRun)
                    ProcessStarted?.Invoke(this, new ProcessEventArgs(process, task, ev));

                if (!DryRun && task.Wait && !process.HasExited)
                {
                    using (process)
                    {
                        if (!process.WaitForExit(task.Timeout <= 0 ? -1 : task.Timeout * 1000))
                        {
                            ProcessTimeout?.Invoke(this, new ProcessEventArgs(process, task, ev));
                            Log.Warn("Process {processid} has not exited within {timeout}s.", process.Id, task.Timeout);
                            process.Kill();
                        }
                        else
                        {
                            ProcessExited?.Invoke(this, new ProcessEventArgs(process, task, ev));
                            Log.Log(process.ExitCode != 0 ? LogLevel.Error : LogLevel.Info,
                                "Process {processid} has exited with code {exitcode}.", process.Id, process.ExitCode);
                        }
                    }
                }
                else
                {
                    runningProcesses.Add(process);
                    process.Exited += (s, e) =>
                    {
                        Log.Log(process.ExitCode != 0 ? LogLevel.Error : LogLevel.Info,
                            "Process {processid} has exited with code {exitCode}.", process.Id, process.ExitCode);
                        runningProcesses.Remove(process);
                        ProcessExited?.Invoke(this, new ProcessEventArgs(process, task, ev));
                        process.Dispose();
                    };
                }
            }
            catch (OperationCanceledException)
            {
                Log.Info("Stopping event handler for path {path}, filter {filter}.", task.Path, task.Filter);
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error has occurred handling events for path {path}, filter {filter}.", task.Path, task.Filter);
            }
        }

        if (!DryRun)
        {
            foreach (var process in runningProcesses)
            {
                if (!process.HasExited)
                    Log.Warn("Process {processid} is still running.", process.Id);
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

        process.OutputDataReceived += (s, oe) => { if (oe.Data != null) Log.Info("{processid}> {data}", process.Id, oe.Data); };
        process.ErrorDataReceived += (s, ee) => { if (ee.Data != null) Log.Error("{processid}> {data}", process.Id, ee.Data); };

        Log.Info("Starting command: {command} {arguments}.", task.Command, task.Arguments);

        if (!DryRun)
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Log.Info("Process ID is {processid}.", process.Id);
        }

        return process;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    public virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var watcher in Watchers.Keys)
            {
                watcher.Dispose();
            }
        }
    }
}
