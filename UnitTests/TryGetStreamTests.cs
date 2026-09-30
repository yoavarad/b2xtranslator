using b2xtranslator.StructuredStorage.Common;
using b2xtranslator.StructuredStorage.Reader;
using b2xtranslator.StructuredStorage.Writer;
using NUnit.Framework;
using System.IO;

namespace UnitTests
{
    [TestFixture]
    public class TryGetStreamTests
    {
        static StructuredStorageReader BuildStorage()
        {
            var writer = new StructuredStorageWriter();
            writer.RootDirectoryEntry.AddStreamDirectoryEntry("Present", new MemoryStream(new byte[] { 1, 2, 3 }));
            writer.RootDirectoryEntry.AddStorageDirectoryEntry("SubStorage");
            var ms = new MemoryStream();
            writer.write(ms);
            ms.Position = 0;
            return new StructuredStorageReader(ms);
        }

        [Test]
        public void PresentStream_ReturnsTrueWithStream()
        {
            using var reader = BuildStorage();
            Assert.That(reader.TryGetStream("Present", out var stream), Is.True);
            Assert.That(stream, Is.Not.Null);
            Assert.That(stream.Length, Is.EqualTo(3));
        }

        [Test]
        public void AbsentStream_ReturnsFalseWithNull()
        {
            using var reader = BuildStorage();
            Assert.That(reader.TryGetStream("Missing", out var stream), Is.False);
            Assert.That(stream, Is.Null);
        }

        [Test]
        public void StorageEntry_ReturnsFalse()
        {
            using var reader = BuildStorage();
            Assert.That(reader.TryGetStream("SubStorage", out var stream), Is.False);
            Assert.That(stream, Is.Null);
        }

        [Test]
        public void GetStream_StillThrowsForAbsentStream()
        {
            using var reader = BuildStorage();
            Assert.Throws<StreamNotFoundException>(() => reader.GetStream("Missing"));
        }
    }
}
