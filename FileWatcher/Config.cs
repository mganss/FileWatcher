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
        /// <summary>
        /// Gets or sets a value indicating whether to perform only a dry run, i.e. not execute commands.
        /// </summary>
        /// <value>
        ///   <c>true</c> if to perform only a dry run; otherwise, <c>false</c>. Default is false.
        /// </value>
        public bool DryRun { get; set; } = false;

        /// <summary>Gets the tasks.</summary>
        /// <value>The tasks.</value>
        public List<WatchTask> Tasks { get; private set; } = new List<WatchTask>();
    }

    /// <summary>
    /// Represents a file system watch task.
    /// </summary>
    public class WatchTask
    {
        /// <summary>
        /// Gets or sets the path to watch.
        /// </summary>
        /// <value>
        /// The path to watch.
        /// </value>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the filter to apply when watching files. Default is "*".
        /// </summary>
        /// <value>
        /// The filter. Default is "*".
        /// </value>
        public string Filter { get; set; } = "*";

        /// <summary>
        /// Gets or sets a value indicating whether to include subdirectories when watching file system changes.
        /// </summary>
        /// <value>
        ///   <c>true</c> if subdirectories will be included; otherwise, <c>false</c>. Default is false.
        /// </value>
        public bool IncludeSubdirectories { get; set; } = false;

        /// <summary>
        /// Gets or sets the attributes to consider for the detection of file system changes.
        /// </summary>
        /// <value>
        /// The attributes to consider for the detection of file system changes. Default is LastWrite, FileName, DirectoryName.
        /// </value>
        public NotifyFilters NotifyFilter { get; set; } = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

        /// <summary>
        /// Gets or sets the types of changes to generate events for.
        /// </summary>
        /// <value>
        /// The change types. Default is All.
        /// </value>
        public WatcherChangeTypes ChangeTypes { get; set; } = WatcherChangeTypes.All;

        /// <summary>
        /// Gets or sets the path to the executable to invoke when a file system change has occurred.
        /// May contain environment variables, e.g. "%AppData%\cmd.exe".
        /// When invoked, gets passed several environment variables that describe the change that has occurred:
        /// <list type="bullet">
        ///     <item>FileWatcher_FullPath</item>
        ///     <item>FileWatcher_Name (relative to the <see cref="Path"/>)</item>
        ///     <item>FileWatcher_ChangeType</item>
        ///     <item>FileWatcher_OldPath (for renames)</item>
        ///     <item>FileWatcher_OldName (for renames)</item>
        /// </list>
        /// </summary>
        /// <value>
        /// The path to the executable.
        /// </value>
        public string Command { get; set; }

        /// <summary>
        /// Gets or sets the arguments to pass to the command. May contain environment variables.
        /// May contain a number of placeholders:
        /// <list type="bullet">
        ///     <item>{FullPath}</item>
        ///     <item>{Name} (relative to <see cref="Path"/>)</item>
        ///     <item>{ChangeType}</item>
        ///     <item>{OldPath} (for renames, otherwise replaced with "")</item>
        ///     <item>{OldName} (for renames, otherwise replaced with "")</item>
        /// </list>
        /// </summary>
        /// <value>
        /// The arguments.
        /// </value>
        public string Arguments { get; set; } = "";

        /// <summary>
        /// Gets or sets the working directory to start the command in.
        /// </summary>
        /// <value>
        /// The working directory.
        /// </value>
        public string WorkingDirectory { get; set; } = "";

        /// <summary>
        /// Gets or sets the number of milliseconds to wait in between command invocations.
        /// </summary>
        /// <value>
        /// The number of ms to wait. The default is 0.
        /// </value>
        public int Throttle { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether to merge consecutive events within the <see cref="Throttle"/> timespan.
        /// Only the last event within the timespan is used to invoke the command.
        /// </summary>
        /// <value>
        ///   <c>true</c> if consecutive events will be merged; otherwise, <c>false</c>. Default is false.
        /// </value>
        public bool Merge { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to wait for a started command to exit before starting a new one.
        /// </summary>
        /// <value>
        ///   <c>true</c> if commands will be waited on; otherwise, <c>false</c>. Default is true.
        /// </value>
        public bool Wait { get; set; } = true;

        /// <summary>
        /// Gets or sets the time in seconds to wait for a command to exit. -1 means wait indefinitely.
        /// If a command timeout expires, the process will be killed.
        /// </summary>
        /// <value>
        /// The timeout in seconds. Default is -1 (wait indefinitely).
        /// </value>
        public int Timeout { get; set; } = -1;
    }
}
