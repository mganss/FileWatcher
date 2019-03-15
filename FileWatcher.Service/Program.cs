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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileWatcher.Service
{
    class Program
    {
        static Logger Log = LogManager.GetCurrentClassLogger();

        public static List<string> ConfigFiles;
        public static bool DryRun;
        public static bool IsConsole;
        public static bool Error = false;

        static async Task<int> Main(string[] args)
        {
            try
            {
                var showHelp = false;

                System.Console.OutputEncoding = Encoding.UTF8;
                IsConsole = Debugger.IsAttached || Process.GetCurrentProcess().SessionId != 0;

                try
                {
                    var options = new OptionSet {
                        { "c|console", "Run as a console application", v => IsConsole = v != null },
                        { "h|help", "Show this message and exit", v => showHelp = v != null },
                        { "d|dryrun", "Do not execute commands, only perform a test run", v => DryRun = v != null },
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
                    services.AddHostedService<FileWriterService>();
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
            System.Console.WriteLine("Usage: FileWatcher [OPTION]... CONFIGFILE...");
            System.Console.WriteLine("Watch file system changes.");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            p.WriteOptionDescriptions(System.Console.Out);
        }
    }
}
