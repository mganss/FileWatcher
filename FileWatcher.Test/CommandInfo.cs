using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcher.Test;

internal class CommandInfo
{
    public List<string> Arguments { get; set; } = new();
    public Dictionary<string, string> Environment { get; set; } = new();
    public string WorkingDirectory { get; set; } = string.Empty;
}
