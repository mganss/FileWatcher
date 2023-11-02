using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using System.Linq;

namespace FileWatcher.Service
{
    public class FileWatcherService : IHostedService, IDisposable
    {
        class WatcherInfo
        {
            public string FileName { get; set; }
            public Config Config { get; set; }
            public List<Watcher> Watchers { get; set; } = new();
            public FileSystemWatcher FileSystemWatcher { get; set; }
        }

        static readonly Logger Log = LogManager.GetLogger("FileWatcher.Service");
        readonly Dictionary<string, WatcherInfo> watchers = new();

        private static Config LoadConfig(string configFile)
        {
            var config = new Config();

            try
            {
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFile));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading configuration file {config}.", configFile);
                Program.Error = true;
            }

            config.DryRun = config.DryRun || Program.DryRun;
            config.AutoReload = config.AutoReload || Program.AutoReload;

            return config;
        }

        private static void StartWatchers(WatcherInfo info)
        {
            try
            {
                var config = info.Config;

                foreach (var task in config.Tasks)
                {
                    var watcher = new Watcher(task) { DryRun = config.DryRun };

                    info.Watchers.Add(watcher);
                    watcher.Start();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error watching file system for configuration {config}.", info.FileName);
                Program.Error = true;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var configFile in Program.ConfigFiles)
            {
                var config = LoadConfig(configFile);
                var info = new WatcherInfo { FileName = configFile, Config = config };

                watchers[configFile] = info;

                StartWatchers(info);

                if (config.AutoReload)
                {
                    try
                    {
                        var configPath = Path.IsPathFullyQualified(configFile) ? configFile
                            : Path.Combine(Environment.CurrentDirectory, configFile);
                        var dir = Path.GetDirectoryName(configPath);
                        var fn = Path.GetFileName(configPath);
                        var fsWatcher = new FileSystemWatcher(dir, fn)
                        {
                            NotifyFilter = NotifyFilters.LastWrite,
                            IncludeSubdirectories = false,
                        };
                        fsWatcher.Changed += (s, e) => ConfigChanged(info);
                        fsWatcher.EnableRaisingEvents = true;
                        info.FileSystemWatcher = fsWatcher;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error initializing auto reload for configuration {config}.", configFile);
                        Program.Error = true;
                    }
                }
            }

            return Task.CompletedTask;
        }

        private static void ConfigChanged(WatcherInfo info)
        {
            Log.Info("Configuration file {config} has changed.", info.FileName);

            var config = LoadConfig(info.FileName);

            if (!info.Config.Equals(config))
            {
                Log.Info("Reloading configuration {config}", info.FileName);

                foreach (var watcher in info.Watchers.ToList())
                {
                    info.Watchers.Remove(watcher);
                    watcher.Stop();
                    watcher.Dispose();
                }

                info.Config = LoadConfig(info.FileName);

                StartWatchers(info);
            }
            else
            {
                Log.Info("Configuration in {config} is unchanged, not reloading.", info.FileName);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var (configFile, info) in watchers)
            {
                if (info.FileSystemWatcher != null)
                    info.FileSystemWatcher.EnableRaisingEvents = false;

                foreach (var watcher in info.Watchers)
                    watcher.Stop();
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            foreach (var (configFile, info) in watchers)
            {
                info.FileSystemWatcher?.Dispose();

                foreach (var watcher in info.Watchers)
                    watcher.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
