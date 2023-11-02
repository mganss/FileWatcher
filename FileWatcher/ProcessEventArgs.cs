using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace FileWatcher;

/// <summary>
/// Provides data for the <see cref="Watcher.ProcessStarted"/> and <see cref="Watcher.ProcessExited"/> events.
/// </summary>
public class ProcessEventArgs: EventArgs
{
    /// <summary>
    /// Gets the process that was started or has exited.
    /// </summary>
    public Process Process { get; }

    /// <summary>
    /// Gets the task that corresponds to the file system change event.
    /// </summary>
    public WatchTask Task { get; }

    /// <summary>
    /// Gets the file system change event.
    /// </summary>
    public FileSystemEventArgs Event { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessEventArgs"/> class.
    /// </summary>
    /// <param name="process">The process that has been started or has exited.</param>
    /// <param name="task">The task that corresponds to the file system change event.</param>
    /// <param name="ev">The file system change event.</param>
    public ProcessEventArgs(Process process, WatchTask task, FileSystemEventArgs ev)
    {
        Process = process;
        Task = task;
        Event = ev;
    }
}
