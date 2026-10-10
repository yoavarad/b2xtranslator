using b2xtranslator.OpenXmlLib;
using NUnit.Framework;
using System.IO;
using System.IO.Compression;

namespace UnitTests
{
    [TestFixture]
    public class ZipFactoryTests
    {
        [Test]
        public void OpenArchive_DisposingReader_LeavesCallerStreamOpen()
        {
            var ms = new MemoryStream();
            using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
                za.CreateEntry("a.txt");
            ms.Position = 0;

            using (ZipFactory.OpenArchive(ms)) { }

            Assert.IsTrue(ms.CanRead);
            Assert.DoesNotThrow(() => { var _ = ms.Length; });
        }

        static MemoryStream BuildZip(string entryName, string content)
        {
            var ms = new MemoryStream();
            using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
            using (var w = new StreamWriter(za.CreateEntry(entryName).Open()))
                w.Write(content);
            ms.Position = 0;
            return ms;
        }

        [Test]
        public void GetEntry_ExistingEntry_ReturnsContent()
        {
            using (var reader = ZipFactory.OpenArchive(BuildZip("word/document.xml", "hello")))
            using (var sr = new StreamReader(reader.GetEntry("word/document.xml")))
                Assert.AreEqual("hello", sr.ReadToEnd());
        }

        [Test]
        public void GetEntry_MissingEntry_ReturnsNull()
        {
            using (var reader = ZipFactory.OpenArchive(BuildZip("a.txt", "x")))
                Assert.IsNull(reader.GetEntry("missing.txt"));
        }

        [Test]
        public void GetEntry_DotDotSegments_AreResolved()
        {
            using (var reader = ZipFactory.OpenArchive(BuildZip("word/document.xml", "hello")))
            using (var sr = new StreamReader(reader.GetEntry("word/theme/../document.xml")))
                Assert.AreEqual("hello", sr.ReadToEnd());
        }

        [Test]
        public void OpenArchive_Path_ReadsEntryAndReleasesFile()
        {
            string path = Path.GetTempFileName();
            try
            {
                using (var zip = BuildZip("a.txt", "x")) File.WriteAllBytes(path, zip.ToArray());
                using (var reader = ZipFactory.OpenArchive(path))
                    Assert.IsNotNull(reader.GetEntry("a.txt"));
                Assert.DoesNotThrow(() => new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose());
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
