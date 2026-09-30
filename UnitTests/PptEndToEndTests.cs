using b2xtranslator.OpenXmlLib.PresentationML;
using b2xtranslator.PptFileFormat;
using b2xtranslator.PresentationMLMapping;
using b2xtranslator.StructuredStorage.Reader;
using b2xtranslator.StructuredStorage.Writer;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace UnitTests
{
    [TestFixture]
    public class PptEndToEndTests
    {
        private static string FixturePath =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "files", "vertical.ppt");

        private static string ReadEntry(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            Assert.IsNotNull(entry, "missing part " + name);
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        // Full ppt -> pptx conversion (same steps as Shell/ppt2x processFile) of a fixture with
        // a vertical title/body slide (issue #83) and a regular title/body slide.
        [Test]
        public void Convert_VerticalPlaceholderFixture_EmitsVerticalLayoutAndPlaceholders()
        {
            string output = Path.Combine(Path.GetTempPath(), "b2x_vertical_" + Guid.NewGuid().ToString("N") + ".pptx");
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
                    // part numbers come from a process-wide counter, so locate slides by content
                    var slides = zip.Entries
                        .Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"))
                        .Select(e => ReadEntry(zip, e.FullName)).ToList();
                    Assert.That(slides.Count, Is.EqualTo(2));
                    foreach (string slide in slides)
                    {
                        StringAssert.Contains("type=\"title\"", slide);
                        StringAssert.Contains("type=\"body\"", slide);
                    }
                    Assert.That(slides.Exists(x => x.Contains("Vertical Title")));
                    Assert.That(slides.Exists(x => x.Contains("Normal Title")));

                    var layoutTypes = new List<string>();
                    foreach (var e in zip.Entries)
                    {
                        if (e.FullName.StartsWith("ppt/slideLayouts/slideLayout") && e.FullName.EndsWith(".xml"))
                            layoutTypes.Add(ReadEntry(zip, e.FullName));
                    }
                    Assert.That(layoutTypes.Exists(x => x.Contains("type=\"vertTitleAndTx\"")), "no vertTitleAndTx layout");
                    Assert.That(layoutTypes.Exists(x => x.Contains("type=\"obj\"") || x.Contains("type=\"tx\"")), "no regular layout");
                }
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }
        }

        // Regenerates UnitTests/files/vertical.ppt from scratch (no third-party content).
        // Run with: dotnet test UnitTests --filter Name=GenerateVerticalFixture
        [Test, Explicit("Regenerates the vertical.ppt fixture")]
        public void GenerateVerticalFixture()
        {
            string dest = Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory, "..", "..", "..", "files", "vertical.ppt"));
            File.WriteAllBytes(dest, BuildVerticalPpt());
        }

        #region fixture builder

        private static byte[] Rec(int ver, int inst, int type, params byte[][] body)
        {
            var ms = new MemoryStream();
            foreach (var b in body) ms.Write(b, 0, b.Length);
            var w = new BinaryWriter(new MemoryStream());
            w.Write((ushort)((inst << 4) | ver));
            w.Write((ushort)type);
            w.Write((uint)ms.Length);
            w.Write(ms.ToArray());
            return ((MemoryStream)w.BaseStream).ToArray();
        }

        private static byte[] Bytes(Action<BinaryWriter> f)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            f(w);
            return ms.ToArray();
        }

        private static byte[] U32(uint v) => BitConverter.GetBytes(v);

        private static byte[] Placeholder(int position, PlaceholderEnum id) =>
            Rec(0, 0, 3011, Bytes(w => { w.Write(position); w.Write((byte)id); w.Write((byte)0); w.Write((ushort)0); }));

        private static byte[] TextShape(int spid, short top, short left, short right, short bottom,
            PlaceholderEnum ph, int position, TextType textType, string text) =>
            Rec(0xF, 0, 0xF004,
                Rec(2, 1, 0xF00A, Bytes(w => { w.Write(spid); w.Write(0xA00u); })),
                Rec(3, 0, 0xF00B),
                Rec(0, 0, 0xF010, Bytes(w => { w.Write(top); w.Write(left); w.Write(right); w.Write(bottom); })),
                Rec(0, 0, 0xF011, Placeholder(position, ph)),
                Rec(0xF, 0, 0xF00D,
                    Rec(0, 0, 3999, U32((uint)textType)),
                    Rec(0, 0, 4000, Encoding.Unicode.GetBytes(text))));

        private static byte[] Drawing(params byte[][] shapes)
        {
            var groupChildren = new List<byte[]>
            {
                Rec(0xF, 0, 0xF004,
                    Rec(1, 0, 0xF009, Bytes(w => { w.Write(0); w.Write(0); w.Write(5760); w.Write(4320); })),
                    Rec(2, 0, 0xF00A, Bytes(w => { w.Write(1024); w.Write(5u); })))
            };
            groupChildren.AddRange(shapes);
            return Rec(0xF, 0, 1036,
                Rec(0xF, 0, 0xF002,
                    Rec(0, 1, 0xF008, Bytes(w => { w.Write((uint)shapes.Length + 1); w.Write(1024); })),
                    Rec(0xF, 0, 0xF003, groupChildren.ToArray())));
        }

        private static byte[] SlideAtom(SlideLayoutType layout, PlaceholderEnum p0, PlaceholderEnum p1, uint masterId) =>
            Rec(2, 0, 1007, Bytes(w =>
            {
                w.Write((int)layout);
                w.Write((byte)p0); w.Write((byte)p1);
                for (int i = 0; i < 6; i++) w.Write((byte)0);
                w.Write(masterId); w.Write(0); w.Write((ushort)1); w.Write((ushort)0);
            }));

        private static byte[] StyleAtom(int instance) =>
            Rec(0, instance, 4003, Bytes(w => { w.Write((ushort)1); w.Write(0u); w.Write(0u); }));

        private static byte[] PersistAtom(uint persistId, uint slideId) =>
            Rec(0, 0, 1011, Bytes(w => { w.Write(persistId); w.Write(0u); w.Write(0); w.Write(slideId); w.Write(0u); }));

        private static byte[] BuildVerticalPpt()
        {
            const uint masterId = 0x80000000;
            var ms = new MemoryStream();
            void Add(byte[] b) => ms.Write(b, 0, b.Length);

            var offsets = new uint[5];

            var docAtom = Rec(1, 0, 1001, Bytes(w =>
            {
                w.Write(5760); w.Write(4320);   // slide size
                w.Write(4320); w.Write(5760);   // notes size
                w.Write(1); w.Write(2);         // server zoom
                w.Write(0u); w.Write(0u);       // no notes / handout master
                w.Write((ushort)1); w.Write((short)0);
                w.Write(new byte[4]);
            }));
            var env = Rec(0xF, 0, 1010, StyleAtom(0));
            var masters = Rec(0xF, 1, 4080, PersistAtom(2, masterId));
            var slides = Rec(0xF, 0, 4080, PersistAtom(3, 256), PersistAtom(4, 257));

            offsets[1] = 0;
            var zoom = Rec(0, 0, 1021, Bytes(w =>
            {
                w.Write(100); w.Write(100); w.Write(100); w.Write(100); // scale
                w.Write(new byte[24]);
                w.Write(0); w.Write(0);                                 // origin
                w.Write((byte)0); w.Write((byte)0);
            }));
            var docInfo = Rec(0xF, 0, 2000, Rec(0xF, 0, 1018, zoom));

            Add(Rec(0xF, 0, 1000, docAtom, env, masters, slides, docInfo));

            offsets[2] = (uint)ms.Length;
            Add(Rec(0xF, 0, 1016,
                SlideAtom(SlideLayoutType.TitleMaster, PlaceholderEnum.None, PlaceholderEnum.None, 0),
                Drawing(),
                StyleAtom(0), StyleAtom(1)));

            offsets[3] = (uint)ms.Length;
            Add(Rec(0xF, 0, 1006,
                SlideAtom(SlideLayoutType.VerticalTitleRightBodyLeft, PlaceholderEnum.VerticalTextTitle, PlaceholderEnum.VerticalTextBody, masterId),
                Drawing(
                    TextShape(1025, 200, 4000, 5500, 4000, PlaceholderEnum.VerticalTextTitle, 0, TextType.Title, "Vertical Title"),
                    TextShape(1026, 200, 300, 3800, 4000, PlaceholderEnum.VerticalTextBody, 1, TextType.Body, "Vertical body text"))));

            offsets[4] = (uint)ms.Length;
            Add(Rec(0xF, 0, 1006,
                SlideAtom(SlideLayoutType.TitleAndBody, PlaceholderEnum.Title, PlaceholderEnum.Body, masterId),
                Drawing(
                    TextShape(1025, 200, 300, 5400, 900, PlaceholderEnum.Title, 0, TextType.Title, "Normal Title"),
                    TextShape(1026, 1100, 300, 5400, 4000, PlaceholderEnum.Body, 1, TextType.Body, "Normal body text"))));

            uint pdOffset = (uint)ms.Length;
            var pdBody = Bytes(w =>
            {
                w.Write(1u | (4u << 20));
                for (int i = 1; i <= 4; i++) w.Write(offsets[i]);
            });
            Add(Rec(0, 0, 6002, pdBody));

            uint ueOffset = (uint)ms.Length;
            Add(Rec(0, 0, 4085, Bytes(w =>
            {
                w.Write(257u);               // LastSlideIdRef
                w.Write((ushort)0); w.Write((byte)0); w.Write((byte)3);
                w.Write(0u);                 // OffsetLastEdit
                w.Write(pdOffset);
                w.Write(1u);                 // DocPersistIdRef
                w.Write(5u);                 // PersistIdSeed
                w.Write((ushort)1); w.Write((ushort)0);
            })));

            var currentUser = Rec(0, 0, 4086, Bytes(w =>
            {
                w.Write(0x14u); w.Write(0xE391C05Fu); w.Write(ueOffset);
                w.Write((ushort)0); w.Write((ushort)0x3F4);
                w.Write((byte)3); w.Write((byte)0); w.Write((ushort)0);
                w.Write(8u);
            }));

            var sso = new StructuredStorageWriter();
            sso.RootDirectoryEntry.AddStreamDirectoryEntry("Current User", new MemoryStream(currentUser));
            sso.RootDirectoryEntry.AddStreamDirectoryEntry("PowerPoint Document", new MemoryStream(ms.ToArray()));
            var outMs = new MemoryStream();
            sso.write(outMs);
            return outMs.ToArray();
        }

        #endregion
    }
}
