using b2xtranslator.DocFileFormat;
using b2xtranslator.OpenXmlLib.WordprocessingML;
using b2xtranslator.StructuredStorage.Common;
using b2xtranslator.StructuredStorage.Reader;
using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using DocConverter = b2xtranslator.WordprocessingMLMapping.Converter;

namespace UnitTests
{
    /// <summary>
    /// Encrypted (password-protected) input must fail deterministically with EncryptedDocumentException,
    /// not be reported as corrupt (ByteParseException) or parsed as ciphertext (issue #156).
    /// </summary>
    [TestFixture]
    public class WordDocumentTests
    {
        [OneTimeSetUp]
        public void SetUp() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        static string FilePath(string name) =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "files", name);

        static void AssertDocEncrypted(string file, string expectedInMessage)
        {
            for (int i = 0; i < 3; i++)
            {
                using (var reader = new StructuredStorageReader(FilePath(file)))
                {
                    var ex = Assert.Throws<EncryptedDocumentException>(() => new WordDocument(reader));
                    Assert.That(ex, Is.Not.InstanceOf<ByteParseException>());
                    StringAssert.Contains(expectedInMessage, ex.Message);
                }
            }
        }

        [Test]
        public void Doc_Rc4Encrypted_ThrowsEncryptedDocumentException() =>
            AssertDocEncrypted("PasswordProtected.doc", "RC4");

        [Test]
        public void Doc_CryptoApiEncrypted_ThrowsEncryptedDocumentException() =>
            AssertDocEncrypted("password_password_cryptoapi.doc", "RC4");

        [Test]
        public void Doc_XorObfuscated_ThrowsEncryptedDocumentException() =>
            AssertDocEncrypted("xor-obfuscation-abc.doc", "XOR");

        [Test]
        public void Doc_Plain_StillConverts()
        {
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".docx");
            try
            {
                using (var reader = new StructuredStorageReader(FilePath("simple.doc")))
                {
                    var doc = new WordDocument(reader);
                    var outType = DocConverter.DetectOutputType(doc);
                    var docx = WordprocessingDocument.Create(DocConverter.GetConformFilename(output, outType), outType);
                    DocConverter.Convert(doc, docx);
                }
                Assert.That(new FileInfo(output).Length, Is.GreaterThan(0));
            }
            finally
            {
                if (File.Exists(output)) File.Delete(output);
            }

            using (var reader = new StructuredStorageReader(FilePath("xor-obfuscation-abc-plain.doc")))
            {
                var doc = new WordDocument(reader);
                Assert.That(doc.Text.Count, Is.EqualTo(15));
                StringAssert.StartsWith("This is a test", new string(doc.Text.ToArray()));
            }
        }
    }
}
