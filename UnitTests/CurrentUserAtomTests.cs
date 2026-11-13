using b2xtranslator.PptFileFormat;
using b2xtranslator.StructuredStorage.Common;
using b2xtranslator.StructuredStorage.Reader;
using NUnit.Framework;
using System;
using System.IO;
using System.Text;

namespace UnitTests
{
    /// <summary>
    /// Encrypted .ppt input (Current User headerToken 0xF3D1C4DF) must throw EncryptedDocumentException,
    /// not NotSupportedException or InvalidStreamException (issue #156).
    /// </summary>
    [TestFixture]
    public class CurrentUserAtomTests
    {
        [OneTimeSetUp]
        public void SetUp() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        static string FilePath(string name) =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "files", name);

        [Test]
        public void Ppt_Encrypted_ThrowsEncryptedDocumentException()
        {
            for (int i = 0; i < 3; i++)
            {
                using (var reader = new StructuredStorageReader(FilePath("Password_Protected-hello.ppt")))
                {
                    var ex = Assert.Throws<EncryptedDocumentException>(() => new PowerpointDocument(reader));
                    Assert.That(ex, Is.Not.InstanceOf<NotSupportedException>());
                    StringAssert.Contains("password-protected", ex.Message);
                }
            }
        }

        [Test]
        public void Ppt_Plain_StillParses()
        {
            using (var reader = new StructuredStorageReader(FilePath("vertical.ppt")))
            {
                Assert.DoesNotThrow(() => new PowerpointDocument(reader));
            }
        }
    }
}
