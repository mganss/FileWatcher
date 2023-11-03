using Ganss.IO;
using System.Diagnostics;
using System.Text.Json;

namespace FileWatcher.Test;

public class Tests
{
    public string TestCommand { get; set; } = string.Empty;
    public string TestDirectory { get; set; } = string.Empty;

    [SetUp]
    public void Setup()
    {
        TestCommand = Path.Combine(AppContext.BaseDirectory, "FileWatcher.TestCommand.exe");
        TestDirectory = Path.Combine(AppContext.BaseDirectory, "test");

        if (Directory.Exists(TestDirectory))
            Directory.Delete(TestDirectory, true);

        Directory.CreateDirectory(TestDirectory);

        foreach (var f in Glob.ExpandNames(Path.Combine(AppContext.BaseDirectory, "test.*.json")))
            File.Delete(f);
    }

    private static List<CommandInfo> RunTask(WatchTask task, Action action, int numProcesses = 1)
    {
        var watcher = new Watcher(task);
        var countdown = new CountdownEvent(numProcesses);

        watcher.ProcessExited += (s, e) => countdown.Signal();

        watcher.Start();

        action();

        countdown.Wait(TimeSpan.FromSeconds(30));

        watcher.Stop();

        var info = Glob.ExpandNames(Path.Combine(AppContext.BaseDirectory, "test.*.json"))
            .Select(f => File.ReadAllText(f))
            .Select(j => JsonSerializer.Deserialize<CommandInfo>(j)!)
            .ToList();

        return info;
    }

    [Test]
    public void TestCreate()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestCreate),
            Path = TestDirectory,
        };

        var fn = "test.txt";
        var info = RunTask(task, () =>
        {
            var path = Path.Combine(TestDirectory, fn);
            File.WriteAllText(path, "");
        });

        Assert.Multiple(() =>
        {
            Assert.That(info, Is.Not.Null);
            Assert.That(info, Has.Count.EqualTo(1));
            var i0 = info[0];
            Assert.That(Path.GetDirectoryName(AppContext.BaseDirectory), Is.EqualTo(i0.WorkingDirectory));
            Assert.That(new Dictionary<string, string>
            {
                ["FileWatcher_ChangeType"] = "Created",
                ["FileWatcher_FullPath"] = Path.Combine(TestDirectory, fn),
                ["FileWatcher_Name"] = fn,
            }, Is.EqualTo(i0.Environment));
        });
    }

    [Test]
    public void TestChange()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestChange),
            Path = TestDirectory,
            Filter = "test.*",
            Throttle = 1,
            Merge = true,
            Arguments = "test {FullPath} {Name} {ChangeType}"
        };

        var fn = "test.txt";
        var info = RunTask(task, () =>
        {
            var path = Path.Combine(TestDirectory, fn);
            File.WriteAllText(path, "");
            File.WriteAllText(Path.Combine(TestDirectory, "xyz.txt"), "");
            File.WriteAllText(path, "changed");
        });

        Assert.Multiple(() =>
        {
            Assert.That(info, Is.Not.Null);
            Assert.That(info, Has.Count.EqualTo(1));

            var fp = Path.Combine(TestDirectory, fn);
            var i = info[0];

            Assert.That(new List<string> { "test", fp, fn, "Changed" }, Is.EqualTo(i.Arguments));
            Assert.That(Path.GetDirectoryName(AppContext.BaseDirectory), Is.EqualTo(i.WorkingDirectory));
            Assert.That(new Dictionary<string, string>
            {
                ["FileWatcher_ChangeType"] = "Changed",
                ["FileWatcher_FullPath"] = fp,
                ["FileWatcher_Name"] = fn,
            }, Is.EqualTo(i.Environment));
        });
    }

    [Test]
    public void TestDelete()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestDelete),
            Path = TestDirectory,
            ChangeTypes = WatcherChangeTypes.Created | WatcherChangeTypes.Deleted,
            Merge = true,
            Throttle = 1,
            Arguments = "test {FullPath} {Name} {ChangeType}"
        };

        var fn = "test.txt";
        var info = RunTask(task, () =>
        {
            var path = Path.Combine(TestDirectory, fn);
            File.WriteAllText(path, "");
            File.Delete(path);
        });

        Assert.Multiple(() =>
        {
            Assert.That(info, Is.Not.Null);
            Assert.That(info, Has.Count.EqualTo(1));

            var fp = Path.Combine(TestDirectory, fn);
            var i = info[0];

            Assert.That(new List<string> { "test", fp, fn, "Deleted" }, Is.EqualTo(i.Arguments));
            Assert.That(Path.GetDirectoryName(AppContext.BaseDirectory), Is.EqualTo(i.WorkingDirectory));
            Assert.That(new Dictionary<string, string>
            {
                ["FileWatcher_ChangeType"] = "Deleted",
                ["FileWatcher_FullPath"] = fp,
                ["FileWatcher_Name"] = fn,
            }, Is.EqualTo(i.Environment));
        });
    }

    [Test]
    public void TestTimeout()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestTimeout),
            Path = TestDirectory,
            Arguments = "delay 10",
            Timeout = 1
        };

        var fn = "test.txt";
        var watcher = new Watcher(task);
        var mre = new ManualResetEventSlim();

        watcher.ProcessTimeout += (s, e) => mre.Set();

        watcher.Start();

        var path = Path.Combine(TestDirectory, fn);
        File.WriteAllText(path, "");

        var timeout = mre.Wait(TimeSpan.FromSeconds(5));

        watcher.Stop();

        Assert.That(timeout, Is.True);
    }

    [Test]
    public void TestWait()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestWait),
            Path = TestDirectory,
            Wait = true,
            Arguments = "delay 1"
        };

        var fn = "test.txt";
        var watcher = new Watcher(task);
        var countdown = new CountdownEvent(2);
        var hasExited = false;
        var waited = false;
        var started = 0;

        watcher.ProcessExited += (s, e) =>
        {
            hasExited = true;
            countdown.Signal();
        };

        watcher.ProcessStarted += (s, e) =>
        {
            if (started == 1 && hasExited) waited = true;
            started++;
        };

        watcher.Start();

        var path = Path.Combine(TestDirectory, fn);
        File.WriteAllText(path, "");
        File.Delete(path);

        countdown.Wait(TimeSpan.FromSeconds(5));

        watcher.Stop();

        Assert.That(waited, Is.True);
    }

    [Test]
    public void TestNoWait()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestNoWait),
            Path = TestDirectory,
            Wait = false,
            Arguments = "delay 1"
        };

        var fn = "test.txt";
        var watcher = new Watcher(task);
        var countdown = new CountdownEvent(2);
        var hasExited = false;
        var waited = true;
        var started = 0;

        watcher.ProcessExited += (s, e) =>
        {
            Assert.That(e.Task, Is.EqualTo(task));
            hasExited = true;
            countdown.Signal();
        };

        watcher.ProcessStarted += (s, e) =>
        {
            Assert.That(e.Process, Is.Not.Null);
            if (started == 0)
                Assert.That(e.Event.ChangeType, Is.EqualTo(WatcherChangeTypes.Created));
            if (started == 1)
                Assert.That(e.Event.ChangeType, Is.EqualTo(WatcherChangeTypes.Deleted));
            if (started == 1 && !hasExited) waited = false;
            started++;
        };

        watcher.Start();

        var path = Path.Combine(TestDirectory, fn);
        File.WriteAllText(path, "");
        File.Delete(path);

        countdown.Wait(TimeSpan.FromSeconds(5));

        watcher.Stop();

        Assert.That(waited, Is.False);
    }

    [Test]
    public void TestNoSubdir()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestNoSubdir),
            Path = TestDirectory,
            Filter = "*.txt"
        };

        var fn = "test.txt";
        var info = RunTask(task, () =>
        {
            var path = Path.Combine(TestDirectory, fn);
            File.WriteAllText(path, "");
            Directory.CreateDirectory(Path.Combine(TestDirectory, "sub"));
            File.WriteAllText(Path.Combine(TestDirectory, "sub", "test.txt"), "");
        });

        Assert.Multiple(() =>
        {
            Assert.That(info, Is.Not.Null);
            Assert.That(info, Has.Count.EqualTo(1));
            var i0 = info[0];
            Assert.That(Path.GetDirectoryName(AppContext.BaseDirectory), Is.EqualTo(i0.WorkingDirectory));
            Assert.That(new Dictionary<string, string>
            {
                ["FileWatcher_ChangeType"] = "Created",
                ["FileWatcher_FullPath"] = Path.Combine(TestDirectory, fn),
                ["FileWatcher_Name"] = fn,
            }, Is.EqualTo(i0.Environment));
        });
    }

    [Test]
    public void TestSubdir()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestSubdir),
            Path = TestDirectory,
            Filter = "*.txt",
            IncludeSubdirectories = true,
            ChangeTypes = WatcherChangeTypes.Created,
            WorkingDirectory = AppContext.BaseDirectory
        };

        var fn = "test.txt";
        var info = RunTask(task, () =>
        {
            var path = Path.Combine(TestDirectory, fn);
            File.WriteAllText(path, "");
            Directory.CreateDirectory(Path.Combine(TestDirectory, "sub"));
            File.WriteAllText(Path.Combine(TestDirectory, "sub", fn), "");
        }, 2);

        Assert.Multiple(() =>
        {
            Assert.That(info, Is.Not.Null);
            Assert.That(info, Has.Count.EqualTo(2));

            var i0 = info[0];
            Assert.That(Path.GetDirectoryName(AppContext.BaseDirectory), Is.EqualTo(i0.WorkingDirectory));
            Assert.That(new Dictionary<string, string>
            {
                ["FileWatcher_ChangeType"] = "Created",
                ["FileWatcher_FullPath"] = Path.Combine(TestDirectory, fn),
                ["FileWatcher_Name"] = fn,
            }, Is.EqualTo(i0.Environment));

            var i1 = info[1];
            Assert.That(Path.GetDirectoryName(AppContext.BaseDirectory), Is.EqualTo(i1.WorkingDirectory));
            Assert.That(new Dictionary<string, string>
            {
                ["FileWatcher_ChangeType"] = "Created",
                ["FileWatcher_FullPath"] = Path.Combine(TestDirectory, "sub", fn),
                ["FileWatcher_Name"] = Path.Combine("sub", fn),
            }, Is.EqualTo(i1.Environment));
        });
    }

    [Test]
    public void TestDryRun()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestDryRun),
            Path = TestDirectory,
        };

        var fn = "test.txt";
        var watcher = new Watcher(task) { DryRun = true };
        var countdown = new CountdownEvent(1);
        var started = false;

        watcher.ProcessStarted += (s, e) =>
        {
            started = true;
            countdown.Signal();
        };

        watcher.Start();

        var path = Path.Combine(TestDirectory, fn);
        File.WriteAllText(path, "");

        countdown.Wait(TimeSpan.FromSeconds(1));

        Assert.That(started, Is.False);

        watcher.Stop();
    }

    [Test]
    public void TestArgs()
    {
        Assert.Throws(typeof(ArgumentNullException), () => new Watcher(null));
        Assert.Throws(typeof(ArgumentException), () => new Watcher(new WatchTask
        {
            Name = null
        }));
        Assert.Throws(typeof(ArgumentException), () => new Watcher(new WatchTask
        {
            Name = "Test",
            Command = null
        }));
        Assert.Throws(typeof(ArgumentException), () => new Watcher(new WatchTask
        {
            Name = "Test",
            Command = "xyz",
            Path = null
        }));
    }

    [Test]
    public void TestStop()
    {
        var task = new WatchTask
        {
            Command = TestCommand,
            Name = nameof(TestStop),
            Path = TestDirectory,
            Throttle = 1000
        };

        var fn = "test.txt";
        var watcher = new Watcher(task) { DryRun = true };

        watcher.Start();

        var path = Path.Combine(TestDirectory, fn);
        File.WriteAllText(path, "");
        File.Delete(path);

        Task.Factory.StartNew(() =>
        {
            Task.Delay(500);
            Stopwatch stopwatch = Stopwatch.StartNew();
            watcher.Stop();
            stopwatch.Stop();
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000));
        });

        Task.Delay(5000);
    }
}