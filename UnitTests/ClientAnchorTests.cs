using b2xtranslator.OfficeDrawing;
using NUnit.Framework;
using System.IO;

namespace UnitTests
{
    [TestFixture]
    public class ClientAnchorTests
    {
        private static ClientAnchor Parse(byte[] body)
        {
            var reader = new BinaryReader(new MemoryStream(body));
            return new ClientAnchor(reader, (uint)body.Length, 0xF010, 0, 0);
        }

        [Test]
        public void SixteenByteBody_ReadsRectAsInt32LeftTopRightBottom()
        {
            var anchor = Parse(new byte[] { 0x49, 0x08, 0x00, 0x00, 0xa9, 0x05, 0x00, 0x00, 0x37, 0x0e, 0x00, 0x00, 0x37, 0x0b, 0x00, 0x00 });
            Assert.That(anchor.Left, Is.EqualTo(2121));
            Assert.That(anchor.Top, Is.EqualTo(1449));
            Assert.That(anchor.Right, Is.EqualTo(3639));
            Assert.That(anchor.Bottom, Is.EqualTo(2871));
        }

        [Test]
        public void EightByteBody_ReadsSmallRectTopLeftRightBottom()
        {
            var anchor = Parse(new byte[] { 0xa9, 0x05, 0x49, 0x08, 0x37, 0x0e, 0x37, 0x0b });
            Assert.That(anchor.Top, Is.EqualTo(1449));
            Assert.That(anchor.Left, Is.EqualTo(2121));
            Assert.That(anchor.Right, Is.EqualTo(3639));
            Assert.That(anchor.Bottom, Is.EqualTo(2871));
        }
    }
}
