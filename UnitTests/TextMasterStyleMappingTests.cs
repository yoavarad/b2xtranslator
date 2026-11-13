using b2xtranslator.OpenXmlLib.PresentationML;
using b2xtranslator.PptFileFormat;
using b2xtranslator.PresentationMLMapping;
using b2xtranslator.StructuredStorage.Reader;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTests
{
    [TestFixture]
    public class TextMasterStyleMappingTests
    {
        private static string ReadEntry(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            Assert.IsNotNull(entry, "missing part " + name);
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static void Convert(string fixture, Action<ZipArchive> assert)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "files", fixture);
            string output = Path.Combine(Path.GetTempPath(), "b2x_tms_" + Guid.NewGuid().ToString("N") + ".pptx");
            try
            {
                using (var reader = new StructuredStorageReader(path))
                {
                    var ppt = new PowerpointDocument(reader);
                    var outType = Converter.DetectOutputType(ppt);
                    var pptx = PresentationDocument.Create(Converter.GetConformFilename(output, outType), outType);
                    Converter.Convert(ppt, pptx);
                }
                using (var zip = ZipFile.OpenRead(output)) assert(zip);
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }

        private static System.Collections.Generic.List<string> Parts(ZipArchive zip, string prefix) =>
            zip.Entries
                .Where(e => e.FullName.StartsWith(prefix) && e.FullName.EndsWith(".xml"))
                .Select(e => ReadEntry(zip, e.FullName)).ToList();

        // Master body level 2 has the indent property but not leftMargin (issue #161).
        [Test]
        public void Convert_MasterLevelWithIndentWithoutLeftMargin_DoesNotThrow()
        {
            Convert("54111.ppt", zip =>
            {
                var slides = Parts(zip, "ppt/slides/slide");
                Assert.That(slides.Count, Is.EqualTo(1));
                StringAssert.Contains("Row 3, Cell 4", slides[0]);
                StringAssert.Contains("Dummy text", slides[0]);
            });
            Convert("numbers2.ppt", zip =>
            {
                var slides = Parts(zip, "ppt/slides/slide");
                Assert.That(slides.Count, Is.EqualTo(2));
                Assert.That(slides.Exists(x => x.Contains("List Item One")));
                Assert.That(slides.Exists(x => x.Contains("This list should start with #6")));
            });
        }

        [Test]
        public void Convert_MasterLevelWithIndentWithoutLeftMargin_OmitsIndentAndMarL()
        {
            Convert("54111.ppt", zip =>
            {
                var master = Parts(zip, "ppt/slideMasters/slideMaster").Single();
                string body = Regex.Match(master, "<p:bodyStyle>.*?</p:bodyStyle>", RegexOptions.Singleline).Value;
                Assert.That(body, Is.Not.Empty);
                string lvl2 = Regex.Match(body, "<a:lvl2pPr[^>]*>").Value;
                string lvl1 = Regex.Match(body, "<a:lvl1pPr[^>]*>").Value;
                Assert.That(lvl2, Is.Not.Empty);
                Assert.That(lvl2, Does.Not.Contain("indent="));
                Assert.That(lvl2, Does.Not.Contain("marL="));
                StringAssert.Contains("marL=\"342900\"", lvl1);
            });
        }
    }
}
