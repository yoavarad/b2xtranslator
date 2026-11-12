using System;
using NUnit.Framework;
using b2xtranslator.StructuredStorage.Common;

namespace UnitTests
{
    [TestFixture]
    public class ExceptionsTests
    {
        [Test]
        public void EncryptedDocumentException_IsExceptionWithMessage()
        {
            var ex = new EncryptedDocumentException("enc");
            Assert.That(ex, Is.InstanceOf<Exception>());
            Assert.That(ex.Message, Is.EqualTo("enc"));
        }
    }
}
