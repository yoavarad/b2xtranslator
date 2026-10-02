using System;
using System.IO;

namespace b2xtranslator.Benchmarks
{
    /// <summary>Locates the perf sample corpus (perf/corpus, see its README).</summary>
    internal static class Corpus
    {
        public const string RegenerateCommand = "python perf/corpus/generate.py";

        /// <summary>Walks up from the binary folder (BenchmarkDotNet runs from a child of it) to the repo's perf/corpus.</summary>
        public static string ResolveDir()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "perf", "corpus");
                if (File.Exists(Path.Combine(candidate, "manifest.json")))
                    return candidate;
            }
            throw new InvalidOperationException("perf/corpus not found above " + AppContext.BaseDirectory);
        }

        /// <summary>Path of the corpus file for a format ("doc", "xls", "ppt") and tier ("small", "large").</summary>
        public static string PathFor(string format, string tier)
        {
            string relative = tier == "small" ? Path.Combine("small", "simple." + format) : Path.Combine("large", "large." + format);
            string path = Path.Combine(ResolveDir(), relative);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Corpus file '{path}' is missing. Generate it with: {RegenerateCommand}", path);
            return path;
        }
    }
}
