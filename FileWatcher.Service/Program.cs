using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mono.Options;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileWatcher.Service
{
    static class Program
    {
        static readonly Logger Log = LogManager.GetLogger("FileWatcher.Console");

        public static List<string> ConfigFiles { get; private set; }
        public static bool DryRun { get; private set; }
        public static bool IsConsole { get; private set; }
        public static bool Error { get; private set; } = false;
        public static bool AutoReload { get; private set; } = true;

        public static void SetError() => Error = true;

        [SupportedOSPlatform("windows")]
        static async Task<int> Main(string[] args)
        {
            try
            {
                var showHelp = false;

                IsConsole = Debugger.IsAttached || Process.GetCurrentProcess().SessionId != 0;

                if (IsConsole)
                    System.Console.OutputEncoding = Encoding.UTF8;
                else
                    Environment.CurrentDirectory = Path.GetDirectoryName(AppContext.BaseDirectory);

                try
                {
                    var options = new OptionSet {
                        { "c|console", "Run as a console application", v => IsConsole = v != null },
                        { "h|help", "Show this message and exit", v => showHelp = v != null },
                        { "d|dryrun", "Do not execute commands, only perform a test run", v => DryRun = v != null },
                        { "r|reload", "Reload when configuration file changes (default is true)", v => AutoReload = v != null },
                    };

                    ConfigFiles = options.Parse(args);

                    if (showHelp)
                    {
                        ShowHelp(options);
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error parsing command line arguments.");
                    return 1;
                }

                if (!ConfigFiles.Any())
                {
                    Log.Error("No config files supplied.");
                    return 1;
                }

                var builder = new HostBuilder().ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<FileWatcherService>();
                });

                if (!IsConsole)
                    await builder.RunAsServiceAsync();
                else
                    await builder.RunConsoleAsync();

                return Error ? 1 : 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error has occurred.");
                return 2;
            }
        }

        static void ShowHelp(OptionSet p)
        {
            System.Console.WriteLine("Usage: FileWatcher.Service [OPTION]... CONFIGFILE...");
            System.Console.WriteLine("Watch file system changes.");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            p.WriteOptionDescriptions(System.Console.Out);
        }
    }
}
