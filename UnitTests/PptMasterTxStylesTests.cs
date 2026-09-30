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
using System.Xml;

namespace UnitTests
{
    // Issue #87: pre-PowerPoint-2007 files carry no RoundTripOArtTextStyles12 atom, so master
    // text styles must be derived from TextMasterStyleAtom.
    // Fixtures (Apache POI test data, testcases/test-data/slideshow, Apache-2.0):
    //   files/WithLinks.ppt      - master has TextMasterStyleAtoms for title/body/notes/other; notes master, no handout master.
    //   files/45537_Header.ppt   - has a handout master.
    // Neither contains RoundTripOArtTextStyles12 (asserted below).
    [TestFixture]
    public class PptMasterTxStylesTests
    {
        private const string P = "http://schemas.openxmlformats.org/presentationml/2006/main";
        private const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";

        private static XmlDocument LoadEntry(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            Assert.IsNotNull(entry, "missing part " + name);
            var doc = new XmlDocument();
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                doc.LoadXml(reader.ReadToEnd());
            return doc;
        }

        private static XmlNamespaceManager Ns(XmlDocument doc)
        {
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("p", P);
            ns.AddNamespace("a", A);
            return ns;
        }

        private static void AssertPopulated(XmlDocument doc, string path)
        {
            var ns = Ns(doc);
            var node = doc.SelectSingleNode(path, ns);
            Assert.IsNotNull(node, "missing " + path);
            var levels = node.SelectNodes("a:lvl1pPr | a:lvl2pPr | a:lvl3pPr | a:lvl4pPr | a:lvl5pPr", ns);
            Assert.That(levels.Count, Is.GreaterThan(0), path + " has no level properties");
            Assert.IsNotNull(node.SelectSingleNode("a:lvl1pPr/a:defRPr", ns), path + " lvl1 has no defRPr");
        }

        private static void ConvertAndInspect(string fixture, Action<PowerpointDocument> checkInput, Action<ZipArchive> checkOutput)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "files", fixture);
            string output = Path.Combine(Path.GetTempPath(), "b2x_txstyles_" + Guid.NewGuid().ToString("N") + ".pptx");
            try
            {
                using (var reader = new StructuredStorageReader(path))
                {
                    var ppt = new PowerpointDocument(reader);
                    Assert.That(ppt.DocumentRecord.AllChildrenWithType<RoundTripOArtTextStyles12>().Count, Is.EqualTo(0));
                    foreach (var m in ppt.MainMasterRecords)
                        Assert.That(m.AllChildrenWithType<RoundTripOArtTextStyles12>().Count, Is.EqualTo(0));
                    checkInput(ppt);
                    var outType = Converter.DetectOutputType(ppt);
                    var pptx = PresentationDocument.Create(Converter.GetConformFilename(output, outType), outType);
                    Converter.Convert(ppt, pptx);
                }

                using (var zip = ZipFile.OpenRead(output))
                    checkOutput(zip);
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }

        [Test]
        public void Convert_PptWithoutRoundTripTxStyles_PopulatesMasterAndNotesStyles()
        {
            ConvertAndInspect("WithLinks.ppt",
                ppt =>
                {
                    Assert.That(ppt.NotesMasterRecords.Count, Is.GreaterThan(0), "fixture has no notes master");
                    Assert.That(ppt.MainMasterRecords[0].AllChildrenWithType<TextMasterStyleAtom>().Any(a => a.Instance == 4 && a.IndentLevelCount > 0),
                        "fixture has no populated 'other' TextMasterStyleAtom");
                },
                zip =>
                {
                    var masters = zip.Entries.Where(e => e.FullName.StartsWith("ppt/slideMasters/slideMaster") && e.FullName.EndsWith(".xml")).ToList();
                    Assert.That(masters.Count, Is.GreaterThan(0));
                    foreach (var m in masters)
                    {
                        var doc = LoadEntry(zip, m.FullName);
                        AssertPopulated(doc, "/p:sldMaster/p:txStyles/p:titleStyle");
                        AssertPopulated(doc, "/p:sldMaster/p:txStyles/p:bodyStyle");
                        AssertPopulated(doc, "/p:sldMaster/p:txStyles/p:otherStyle");
                    }

                    var notes = zip.Entries.Where(e => e.FullName.StartsWith("ppt/notesMasters/notesMaster") && e.FullName.EndsWith(".xml")).ToList();
                    Assert.That(notes.Count, Is.EqualTo(1));
                    AssertPopulated(LoadEntry(zip, notes[0].FullName), "/p:notesMaster/p:notesStyle");
                });
        }

        // CT_HandoutMaster has no txStyles child, so none must be emitted.
        [Test]
        public void Convert_PptWithHandoutMaster_EmitsNoTxStyles()
        {
            ConvertAndInspect("45537_Header.ppt",
                ppt => Assert.That(ppt.HandoutMasterRecords.Count, Is.GreaterThan(0), "fixture has no handout master"),
                zip =>
                {
                    var handouts = zip.Entries.Where(e => e.FullName.StartsWith("ppt/handoutMasters/") && e.FullName.EndsWith(".xml")).ToList();
                    Assert.That(handouts.Count, Is.EqualTo(1));
                    var doc = LoadEntry(zip, handouts[0].FullName);
                    Assert.IsNotNull(doc.SelectSingleNode("/p:handoutMaster", Ns(doc)));
                    Assert.IsNull(doc.SelectSingleNode("//p:txStyles | //p:handoutStyle", Ns(doc)));
                });
        }
    }
}
