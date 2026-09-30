using System.Diagnostics;

namespace b2xtranslator.Tools
{
    /// <summary>
    /// Stage-level tracing for the conversion pipeline. Activities are only
    /// created when an ActivityListener subscribes to <see cref="SourceName"/>.
    /// Stages: open-storage, parse, map, write.
    /// </summary>
    public static class Instrumentation
    {
        public const string SourceName = "b2xtranslator";

        public static readonly ActivitySource Source = new ActivitySource(SourceName);
    }
}
