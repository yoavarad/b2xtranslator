using b2xtranslator.OpenXmlLib;
using b2xtranslator.OpenXmlLib.WordprocessingML;
using NUnit.Framework;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace UnitTests
{
    /// <summary>Package write options (task #107): CompressionLevel and Indent, defaults unchanged.</summary>
    [TestFixture]
    public class OpenXmlWriterOptionsTests
    {
        private string _path;

        [SetUp]
        public void SetUp() => _path = Path.Combine(Path.GetTempPath(), "b2x-opts-" + System.Guid.NewGuid().ToString("N") + ".docx");

        [TearDown]
        public void TearDown() => File.Delete(_path);

        private static string ReadEntry(ZipArchive zip, string name)
        {
            using var reader = new StreamReader(zip.GetEntry(name).Open());
            return reader.ReadToEnd();
        }

        private void WriteDocument(System.Action<WordprocessingDocument> configure)
        {
            var doc = WordprocessingDocument.Create(_path, OpenXmlPackage.DocumentType.Document);
            configure(doc);
            var w = doc.MainDocumentPart.XmlWriter;
            w.WriteStartElement("a");
            w.WriteStartElement("b");
            w.WriteEndElement();
            w.WriteEndElement();
            w.Flush();
            doc.Close();
        }

        [Test]
        public void Defaults_AreOptimalAndIndented()
        {
            var doc = WordprocessingDocument.Create(_path, OpenXmlPackage.DocumentType.Document);
            Assert.That(doc.CompressionLevel, Is.EqualTo(CompressionLevel.Optimal));
            Assert.That(doc.Indent, Is.True);
            doc.Close();

            WriteDocument(_ => { });
            using var zip = ZipFile.OpenRead(_path);
            Assert.That(ReadEntry(zip, "_rels/.rels"), Does.Contain("\n  <Relationship"));
            Assert.That(ReadEntry(zip, "word/document.xml"), Does.Contain("\n  <b"));
        }

        [Test]
        public void IndentFalse_WritesUnindentedXml()
        {
            WriteDocument(doc => doc.Indent = false);
            using var zip = ZipFile.OpenRead(_path);
            Assert.That(ReadEntry(zip, "_rels/.rels"), Does.Not.Contain("\n"));
            Assert.That(ReadEntry(zip, "[Content_Types].xml"), Does.Not.Contain("\n"));
            Assert.That(ReadEntry(zip, "word/document.xml"), Does.Not.Contain("\n"));
        }

        [Test]
        public void NoCompression_StoresEntries()
        {
            WriteDocument(doc => doc.CompressionLevel = CompressionLevel.NoCompression);
            using var zip = ZipFile.OpenRead(_path);
            Assert.That(zip.Entries, Is.Not.Empty);
            Assert.That(zip.Entries.All(e => e.Length > 0 && e.CompressedLength == e.Length), Is.True);
        }

        [Test]
        public void ExistingFile_IsOverwritten()
        {
            File.WriteAllBytes(_path, Enumerable.Repeat((byte)0x55, 100_000).ToArray());
            WriteDocument(_ => { });
            WriteDocument(_ => { });
            using var zip = ZipFile.OpenRead(_path);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.That(names, Is.Unique);
            Assert.That(names, Does.Contain("word/document.xml"));
        }
    }
}
