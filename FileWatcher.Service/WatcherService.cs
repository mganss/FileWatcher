using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace FileWatcher.Service
{
    public class FileWriterService : IHostedService, IDisposable
    {
        static Logger Log = LogManager.GetCurrentClassLogger();
        List<Watcher> watchers = new List<Watcher>();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var configFile in Program.ConfigFiles)
            {
                Config config = null;

                try
                {
                    config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFile));
                    config.DryRun = config.DryRun || Program.DryRun;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error reading configuration file {config}", configFile);
                    Program.Error = true;
                    continue;
                }

                try
                {
                    foreach (var task in config.Tasks)
                    {
                        var watcher = new Watcher(task) { DryRun = config.DryRun };

                        watchers.Add(watcher);
                        watcher.Start();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error watching file system for configuration {config}", configFile);
                    Program.Error = true;
                }
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var watcher in watchers)
                watcher.Stop();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            foreach (var watcher in watchers)
                watcher.Dispose();
        }
    }
}
