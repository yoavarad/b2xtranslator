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
    }
}
