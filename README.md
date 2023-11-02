FileWatcher
===========

[![NuGet version](https://badge.fury.io/nu/FileWatcher.svg)](http://badge.fury.io/nu/FileWatcher)
[![Build status](https://ci.appveyor.com/api/projects/status/pn3y41ltb8tcq4kk?svg=true)](https://ci.appveyor.com/project/mganss/FileWatcher/branch/master)
[![codecov.io](https://codecov.io/github/mganss/FileWatcher/coverage.svg?branch=master)](https://codecov.io/github/mganss/FileWatcher?branch=master)

A Windows service, console application, and library that allows execution of commands in response to file system changes.

Usage
-----

FileWatcher can be used either as a console application, as a Windows service, or as a library in your own applications. If you want to use the service or console application just grab a zip from [releases](https://github.com/mganss/FileWatcher/releases).

```
Usage: FileWatcher.Service [OPTION]... CONFIGFILE...
Watch file system changes.

Options:
  -c, --console              Run as a console application
  -h, --help                 Show this message and exit
  -d, --dryrun               Do not execute commands, only perform a test run
  -r, --reload               Reload when configuration file changes (default is
                               true)
```

A configuration file looks like this:

```json
{
  "DryRun": true,
  "AutoReload": true,
  "Tasks": [
    {
      "Name": "MyTask",
      "Path": "c:/path/to/watch",
      "Filter": "*",
      "IncludeSubdirectories": false,
      "NotifyFilter": "Attributes, CreationTime, LastAccess, LastWrite",
      "ChangeTypes": "Changed, Created, Deleted, Renamed",
      "Command": "c:/path/to/command",
      "Arguments": "-x -y -z",
      "WorkingDirectory": "c:/path/to/cwd",
      "Throttle": 2000,
      "Merge": true,
      "Wait": true,
      "Timeout": 10
    }
  ]
}
```

Property | Description
--- | ---
`DryRun` | If true, no actual commands will be executed (default is false). Will be considered true if either this or the command line argument `-d` is true.
`AutoReload` | If true, will automatically reload the configuration file when it changes (default is true). Will be considered true if this or the command line argument `-r` is true.
`Name` | Required. Name of the task used for identification in the log.
`Path` | Required. The path of the directory to watch.
`Filter` | The filter that determines the files that will be watched (default is "*"). Syntax is the same as for [`FileSystemWatcher.Filter`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher.filter#remarks).
`IncludeSubdirectories` | If true, will include subdirectories when watching file system changes (default is false).
`NotifyFilter` | The attributes to consider for detection of file system changes (default is `LastWrite`, `FileName`, `DirectoryName`). Possible values are `Attributes`, `CreationTime`, `LastAccess`, `LastWrite`, `FileName`, `DirectoryName`, `Security`, and `Size` (see also the documentation for the [`NotifyFilters enum`](https://learn.microsoft.com/en-us/dotnet/api/system.io.notifyfilters)).
`ChangeTypes` | The types of changes to watch (default is `All`). Possible values are `All`, `Changed`, `Created`, `Deleted`, `Renamed` (see also the documentation for [`WatcherChangeTypes enum`](https://learn.microsoft.com/en-us/dotnet/api/system.io.watcherchangetypes)).
`Command` | Required. The path to the executable to invoke when a file system change has occurred. May contain environment variables, e.g. `"%AppData%\cmd.exe"`. When invoked, gets passed several environment variables that describe the change that has occurred: <ul><li>`FileWatcher_FullPath`</li><li>`FileWatcher_Name` (relative to the `Path`)</li><li>`FileWatcher_ChangeType`</li><li>`FileWatcher_OldPath` (for renames)</li><li>`FileWatcher_OldName` (for renames)</li></ul>
`Arguments` | The arguments to pass to the command. May contain environment variables. May contain a number of placeholders: <ul><li>`{FullPath}`</li><li>`{Name}` (relative to `Path`)</li><li>`{ChangeType}`</li><li>`{OldPath}` (for renames, otherwise replaced with "")</li><li>`{OldName}` (for renames, otherwise replaced with "")</li></ul>
`WorkingDirectory` | The working directory to start the command in.
`Throttle` | The number of milliseconds to wait in between command invocations (default is 0).
`Merge` | If true, will merge consecutive events within the `Throttle` timespan (default is false). Only the last event within the timespan is used to invoke the command.
`Wait` | If true, will wait for a started command to exit before starting a new one (default is true).
`Timeout` | The time in seconds to wait for a command to exit. -1 means wait indefinitely (the default). If a command timeout expires, the process will be killed.

Logging
-------

FileWatcher uses [NLog](https://github.com/NLog/NLog). If you're using the console application, you can customize the `NLog.config` to your needs. The default configuration logs to the console as well as a daily rolling file `log.txt` in the same folder as the executable and keeps a maximum of 10 archived log files.

Service
-------

In addition to the command line you can also run FileWatcher as a Windows service. 
To install the service, use the [sc](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/sc-create) command:

```
sc create FileWatcher binpath= "c:\path\to\FileWatcher.Service.exe config.json"
sc description FileWatcher "Executes commands in response to file system changes"
```

Note the space after `binpath=`. The current working directory of the service is the directory containing the executable and will be used to find the configuration file if it is given as a relative path (like in the example above).

To start the service:

```
net start FileWatcher
```
