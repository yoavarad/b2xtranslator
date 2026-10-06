using b2xtranslator.StructuredStorage.Reader;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace UnitTests
{
    [TestFixture]
    public class StorageReaderCharacterizationTests
    {
        static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "b2xtranslator.sln")))
                dir = dir.Parent;
            return dir.FullName;
        }

        static string Digest(string file, int chunk)
        {
            using var reader = new StructuredStorageReader(Path.Combine(RepoRoot(), "UnitTests", "files", file));
            using var all = SHA256.Create();
            foreach (var path in reader.FullNameOfAllStreamEntries.OrderBy(p => p, StringComparer.Ordinal))
            {
                var s = reader.GetStream(path);
                var buf = new byte[chunk];
                s.Position = 0;
                int n;
                long total = 0;
                var data = new MemoryStream();
                while ((n = s.Read(buf, 0, buf.Length)) > 0) { data.Write(buf, 0, n); total += n; }
                Assert.That(total, Is.EqualTo(s.Length), path);
                var h = all.ComputeHash(data.ToArray());
                all.TransformBlock(h, 0, h.Length, null, 0);
            }
            all.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(all.Hash!);
        }

        [TestCase("simple.doc", "207D6CF86B081886597AD49401DD0148B6DAC447EEABCE86228AA150C7BEE1AD")]
        [TestCase("simple.xls", "33D3463D3AF206679682B4509FAFEC1629048EBAC0194867D5D9E4A6328C064D")]
        public void StreamContents_AreStableAcrossChunkSizes(string file, string expected)
        {
            var a = Digest(file, 4096);
            Assert.That(Digest(file, 7), Is.EqualTo(a));
            Assert.That(Digest(file, 512), Is.EqualTo(a));
            Assert.That(Digest(file, 100000), Is.EqualTo(a));
            Assert.That(a, Is.EqualTo(expected));
        }
    }
}


