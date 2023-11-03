using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcher.Service
{
    /// <summary>
    /// Represents configuration information.
    /// </summary>
    public record Config
    {
        /// <summary>
        /// Gets or sets a value indicating whether to automatically reload the configuration file when it changes.
        /// </summary>
        /// <value>
        ///     <c>true</c> if to automatically reload the configuration file; otherwise, <c>false</c>. Default is true.
        /// </value>
        public bool AutoReload { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to perform only a dry run, i.e. not execute commands.
        /// </summary>
        /// <value>
        ///   <c>true</c> if to perform only a dry run; otherwise, <c>false</c>. Default is false.
        /// </value>
        public bool DryRun { get; set; } = false;

        /// <summary>Gets the tasks.</summary>
        /// <value>The tasks.</value>
        public List<WatchTask> Tasks { get; private set; } = new();

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="other">The object to compare with the current object.</param>
        /// <returns><c>true</c> if the specified object is equal to the current object; otherwise, <c>false</c>.</returns>
        public virtual bool Equals(Config other)
        {
            return other != null && other.DryRun == DryRun && other.AutoReload == AutoReload
                && other.Tasks.SequenceEqual(Tasks);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 31 + AutoReload.GetHashCode();
            hash = hash * 31 + DryRun.GetHashCode();
            foreach (var task in Tasks)
                hash = hash * 31 + task.GetHashCode();
            return hash;
        }
    }
}
