using b2xtranslator.DocFileFormat;
using b2xtranslator.PptFileFormat;
using b2xtranslator.Spreadsheet.XlsFileFormat;
using b2xtranslator.StructuredStorage.Common;
using b2xtranslator.StructuredStorage.Reader;
using b2xtranslator.StructuredStorage.Writer;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace UnitTests
{
    /// <summary>
    /// Truncated / corrupted .doc, .xls and .ppt input must fail with an exception defined by
    /// b2xtranslator (not a raw runtime fault such as IndexOutOfRangeException), within bounded
    /// time and without unbounded allocations.
    /// </summary>
    [TestFixture]
    public class MalformedInputTests
    {
        static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(30);
        const long AllocationLimit = 256L * 1024 * 1024;

        [OneTimeSetUp]
        public void RegisterCodePages() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        static string FilePath(string name)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "b2xtranslator.sln")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "b2xtranslator.sln not found above " + AppContext.BaseDirectory);
            return Path.Combine(dir.FullName, "UnitTests", "files", name);
        }

        /// <summary>Deterministic truncations and random byte corruptions of the given input.</summary>
        static IEnumerable<(string name, byte[] data)> Mutations(byte[] original, int seed, int corruptions)
        {
            for (int i = 1; i < 32; i++)
            {
                int len = (int)((long)original.Length * i / 32);
                yield return ("trunc" + len, original.Take(len).ToArray());
            }
            var rnd = new Random(seed);
            for (int i = 0; i < corruptions; i++)
            {
                var copy = (byte[])original.Clone();
                int n = 1 + rnd.Next(16);
                for (int k = 0; k < n; k++)
                    copy[rnd.Next(copy.Length)] = (byte)rnd.Next(256);
                yield return ("corrupt" + i, copy);
            }
        }

        static bool IsDefined(Exception ex) => ex.GetType().Namespace?.StartsWith("b2xtranslator") == true;

        /// <summary>Parses the input on a separate thread; returns a failure description or null.</summary>
        static string ParseBounded(byte[] data, Action<StructuredStorageReader> parse, out Exception caught)
        {
            Exception error = null;
            long allocated = 0;
            var t = new Thread(() =>
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                try
                {
                    using (var reader = new StructuredStorageReader(new MemoryStream(data)))
                        parse(reader);
                }
                catch (Exception ex) { error = ex; }
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            }, 16 * 1024 * 1024) { IsBackground = true };
            t.Start();
            caught = null;
            if (!t.Join(TimeLimit))
                return "did not finish within " + TimeLimit;
            caught = error;
            if (allocated > AllocationLimit)
                return "allocated " + allocated + " bytes";
            if (error != null && !IsDefined(error))
                return error.GetType().FullName + ": " + error.Message + "\n" + error.StackTrace;
            return null;
        }

        static void AssertAllMutationsFailCleanly(IEnumerable<(string name, byte[] data)> inputs, Action<StructuredStorageReader> parse)
        {
            var failures = new List<string>();
            foreach (var (name, data) in inputs)
            {
                string failure = ParseBounded(data, parse, out _);
                if (failure != null)
                    failures.Add(name + ": " + failure);
            }
            Assert.IsEmpty(failures, failures.Count + " malformed inputs not rejected cleanly:\n" + string.Join("\n\n", failures.Take(5)));
        }

        // ---------- .doc ----------

        [Test]
        public void Doc_OriginalSampleParses()
        {
            Assert.IsNull(ParseBounded(File.ReadAllBytes(FilePath("simple.doc")), r => new WordDocument(r), out var ex));
            Assert.IsNull(ex);
        }

        [Test]
        public void Doc_TruncatedOrCorrupted_FailsWithDefinedException()
        {
            AssertAllMutationsFailCleanly(Mutations(File.ReadAllBytes(FilePath("simple.doc")), 1, 600), r => new WordDocument(r));
        }

        // ---------- .xls ----------

        [Test]
        public void Xls_OriginalSampleParses()
        {
            Assert.IsNull(ParseBounded(File.ReadAllBytes(FilePath("simple.xls")), r => new XlsDocument(r), out var ex));
            Assert.IsNull(ex);
        }

        [Test]
        public void Xls_TruncatedOrCorrupted_FailsWithDefinedException()
        {
            AssertAllMutationsFailCleanly(Mutations(File.ReadAllBytes(FilePath("simple.xls")), 2, 600), r => new XlsDocument(r));
        }

        // ---------- .ppt (synthetic, no binary sample in repo) ----------

        static byte[] Rec(uint version, uint instance, ushort type, params byte[][] body)
        {
            var content = body.SelectMany(b => b).ToArray();
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write((ushort)(version | (instance << 4)));
            w.Write(type);
            w.Write((uint)content.Length);
            w.Write(content);
            return ms.ToArray();
        }

        static byte[] U32(params uint[] values) => values.SelectMany(BitConverter.GetBytes).ToArray();

        /// <summary>
        /// Minimal PowerPoint Document stream: DocumentContainer at 0, PersistDirectoryAtom, UserEditAtom.
        /// Returns the stream and the offset of the UserEditAtom.
        /// </summary>
        static byte[] BuildPptDocumentStream(bool selfReferencingEdit, out uint userEditOffset)
        {
            var documentAtom = Rec(1, 0, 1001, new byte[40]);
            var document = Rec(0xF, 0, 1000, documentAtom);

            uint persistDirOffset = (uint)document.Length;
            // persistId 1, cPersist 1 -> offset 0
            var persistDir = Rec(0, 0, 6002, U32(1u | (1u << 20), 0));

            userEditOffset = persistDirOffset + (uint)persistDir.Length;
            var userEditBody = new MemoryStream();
            var w = new BinaryWriter(userEditBody);
            w.Write(0u);                                              // lastSlideIdRef
            w.Write((ushort)0); w.Write((byte)0); w.Write((byte)3);   // version, minor, major
            w.Write(selfReferencingEdit ? userEditOffset : 0u);       // offsetLastEdit
            w.Write(persistDirOffset);                                // offsetPersistDirectory
            w.Write(1u);                                              // docPersistIdRef
            w.Write(2u);                                              // persistIdSeed
            w.Write((ushort)1); w.Write((ushort)0);                   // lastView, unused
            var userEdit = Rec(0, 0, 4085, userEditBody.ToArray());

            return document.Concat(persistDir).Concat(userEdit).ToArray();
        }

        static byte[] BuildCurrentUserStream(uint offsetToCurrentEdit)
        {
            var body = new MemoryStream();
            var w = new BinaryWriter(body);
            w.Write(0x14u);                           // size
            w.Write(0xE391C05Fu);                     // headerToken (not encrypted)
            w.Write(offsetToCurrentEdit);
            w.Write((ushort)1);                       // lenUserName
            w.Write((ushort)0x03F4);                  // docFileVersion
            w.Write((byte)3); w.Write((byte)0);       // major, minor
            w.Write((ushort)0);                       // unused
            w.Write((byte)'a');                       // ansiUserName
            w.Write(8u);                              // relVersion
            return Rec(0, 0, 4086, body.ToArray());
        }

        static byte[] BuildPpt(byte[] currentUser, byte[] document)
        {
            var sso = new StructuredStorageWriter();
            sso.RootDirectoryEntry.AddStreamDirectoryEntry("Current User", new MemoryStream(currentUser));
            sso.RootDirectoryEntry.AddStreamDirectoryEntry("PowerPoint Document", new MemoryStream(document));
            var ms = new MemoryStream();
            sso.write(ms);
            return ms.ToArray();
        }

        static byte[] ValidPpt()
        {
            var doc = BuildPptDocumentStream(false, out uint editOffset);
            return BuildPpt(BuildCurrentUserStream(editOffset), doc);
        }

        [Test]
        public void Ppt_SyntheticSampleParses()
        {
            Assert.IsNull(ParseBounded(ValidPpt(), r => new PowerpointDocument(r), out var ex));
            Assert.IsNull(ex);
        }

        [Test]
        public void Ppt_UserEditAtomCycle_FailsWithDefinedException()
        {
            var doc = BuildPptDocumentStream(true, out uint editOffset);
            var data = BuildPpt(BuildCurrentUserStream(editOffset), doc);

            Assert.IsNull(ParseBounded(data, r => new PowerpointDocument(r), out var ex));
            Assert.IsInstanceOf<InvalidStreamException>(ex);
        }

        [Test]
        public void Ppt_TruncatedOrCorrupted_FailsWithDefinedException()
        {
            var doc = BuildPptDocumentStream(false, out uint editOffset);
            var currentUser = BuildCurrentUserStream(editOffset);

            // corrupt the record streams themselves (keeps the container intact) ...
            var inputs = Mutations(doc, 3, 400).Select(m => ("doc-" + m.name, BuildPpt(currentUser, m.data)))
                .Concat(Mutations(currentUser, 4, 100).Select(m => ("cu-" + m.name, BuildPpt(m.data, doc))))
                // ... and the whole compound file
                .Concat(Mutations(ValidPpt(), 5, 200).Select(m => ("file-" + m.name, m.data)));

            AssertAllMutationsFailCleanly(inputs, r => new PowerpointDocument(r));
        }

        // ---------- OLE / CFB ----------

        [Test]
        public void Cfb_DirectoryEntryPointingOutsideDirectory_FailsWithDefinedException()
        {
            var data = File.ReadAllBytes(FilePath("simple.doc"));
            var header = new StructuredStorageReader(new MemoryStream(data)); // sanity: valid
            header.Close();

            // Root entry's child sid -> far beyond the directory chain.
            uint dirStart = BitConverter.ToUInt32(data, 0x30);
            int rootEntry = 512 + (int)dirStart * 512;
            BitConverter.GetBytes(0x00FFFFF0u).CopyTo(data, rootEntry + 0x4C);

            Assert.IsNull(ParseBounded(data, r => { }, out var ex));
            Assert.IsInstanceOf<InvalidValueInDirectoryEntryException>(ex);
        }
    }
}
