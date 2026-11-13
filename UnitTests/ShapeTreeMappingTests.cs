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

namespace UnitTests
{
    // Fixture files/ppt_with_png_encrypted_plain.ppt is derived from Apache POI
    // test-data/slideshow/ppt_with_png_encrypted.ppt (Apache-2.0), decrypted (RC4 CryptoAPI,
    // password 'password'); 'PowerPoint Document' and 'Pictures' streams decrypted, Current User
    // headerToken set to 0xE391C05F. Plaintext 'PowerPoint Document' stream sha256
    // 7b510ce248088a3a5ab7b233af2673e42810ea3eeb58cd3d92a83c6b12398776. Issue #160.
    [TestFixture]
    public class ShapeTreeMappingTests
    {
        private static string FixturePath =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "files", "ppt_with_png_encrypted_plain.ppt");

        private static string ReadEntry(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            Assert.IsNotNull(entry, "missing part " + name);
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        [Test]
        public void Convert_SixteenByteClientAnchorPicture_WritesPictureXfrm()
        {
            string output = Path.Combine(Path.GetTempPath(), "b2x_png_" + Guid.NewGuid().ToString("N") + ".pptx");
            try
            {
                using (var reader = new StructuredStorageReader(FixturePath))
                {
                    var ppt = new PowerpointDocument(reader);
                    var outType = Converter.DetectOutputType(ppt);
                    string conform = Converter.GetConformFilename(output, outType);
                    Assert.That(conform, Is.EqualTo(output));
                    var pptx = PresentationDocument.Create(conform, outType);
                    Converter.Convert(ppt, pptx);
                }

                using (var zip = ZipFile.OpenRead(output))
                {
                    var slides = zip.Entries
                        .Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"))
                        .Select(e => ReadEntry(zip, e.FullName))
                        .Where(x => x.Contains("Here is a PNG image:")).ToList();
                    Assert.That(slides.Count, Is.EqualTo(1));
                    var slide = slides[0];
                    StringAssert.Contains("<p:pic", slide);
                    StringAssert.Contains("<a:off x=\"3367087\" y=\"2300287\"", slide);
                    StringAssert.Contains("<a:ext cx=\"2409825\" cy=\"2257425\"", slide);
                }
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }
    }
}
