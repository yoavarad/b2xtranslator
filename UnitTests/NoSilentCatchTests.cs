using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnitTests
{
    [TestFixture]
    public class NoSilentCatchTests
    {
        private static readonly string[] SourceDirs = { "Common", "Doc", "Ppt", "Xls", "Shell" };

        private static string FindSolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "b2xtranslator.sln")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "b2xtranslator.sln not found above " + AppContext.BaseDirectory);
            return dir.FullName;
        }

        [Test]
        public void NoCatchBlockHasEmptyOrCommentOnlyBody()
        {
            string root = FindSolutionRoot();
            var comments = new Regex(@"/\*.*?\*/|//[^\r\n]*", RegexOptions.Singleline);
            var emptyCatch = new Regex(@"\bcatch\b\s*(\([^)]*\))?\s*\{\s*\}");
            var offenders = new List<string>();

            foreach (string sub in SourceDirs)
            {
                string dir = Path.Combine(root, sub);
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    // comments are blanked out (keeping newlines) so comment-only bodies count as empty
                    // and commented-out catch blocks are ignored
                    string text = comments.Replace(File.ReadAllText(file), m => Regex.Replace(m.Value, @"[^\r\n]", " "));
                    foreach (Match m in emptyCatch.Matches(text))
                    {
                        int line = text.Take(m.Index).Count(c => c == '\n') + 1;
                        offenders.Add(file.Substring(root.Length + 1) + ":" + line);
                    }
                }
            }

            Assert.IsEmpty(offenders, "Empty catch blocks:\n" + string.Join("\n", offenders));
        }
    }
}
